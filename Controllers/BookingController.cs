using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AirlineSeatReservationSystem.Data.Abstract;
using AirlineSeatReservationSystem.Data.Concrete.Efcore;
using AirlineSeatReservationSystem.Entity;
using AirlineSeatReservationSystem.Models;
using AirlineSeatReservationSystem.Services;

namespace AirlineSeatReservationSystem.Controllers;

public class BookingController : Controller
{
    private readonly IBookingRepository _repository;
    private readonly IFlightRepository _flightRepository;
    private readonly DataContext _context;
    private readonly ILogger<BookingController> _logger;
    private readonly LanguageService _localization;

    public BookingController(
        IBookingRepository repository,
        IFlightRepository flightRepository,
        DataContext context,
        ILogger<BookingController> logger,
        LanguageService localization)
    {
        _repository = repository;
        _flightRepository = flightRepository;
        _context = context;
        _logger = logger;
        _localization = localization;
    }

    public IActionResult Index()
    {
        ViewBag.From = _localization.Getkey("From").Value;
        ViewBag.To = _localization.Getkey("To").Value;
        ViewBag.DepartureTime = _localization.Getkey("Departure Time").Value;
        ViewBag.DepartureDate = _localization.Getkey("Departure Date").Value;
        ViewBag.ReturnDate = _localization.Getkey("Return Date").Value;
        ViewBag.SeatNumber = _localization.Getkey("Seat").Value;
        return View();
    }

    public IActionResult ChangeLanguage(string culture)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
        return Redirect(Request.Headers["Referer"].ToString());
    }

    [Authorize]
    public IActionResult MyBookings()
    {
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return RedirectToAction("Index", "Flight");

        var userId = int.Parse(userIdClaim.Value);

        var bookingsList = _repository.GetBookingsByUserId(userId)
            .Include(b => b.Flight)
            .Include(b => b.Seat)
            .ToList();

        var viewModel = new MyBookingsViewModel { Bookings = bookingsList };
        return View(viewModel);
    }

    // ── Step 1 of booking flow: Passenger Details ──────────────────────────────

    [Authorize]
    public IActionResult PassengerDetails(int flightId)
    {
        var flight = _context.Flights.FirstOrDefault(f => f.FlightId == flightId);
        if (flight == null) return NotFound();

        var model = new PassengerDetailsViewModel
        {
            FlightId   = flightId,
            FlightFrom = flight.From,
            FlightTo   = flight.To,
            FlightDepart = flight.Depart,
            FlightTime = flight.Time
        };

        SetPassengerDetailsViewBag();
        return View(model);
    }

    [Authorize]
    [HttpPost]
    public IActionResult PassengerDetails(PassengerDetailsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var flight = _context.Flights.FirstOrDefault(f => f.FlightId == model.FlightId);
            if (flight != null)
            {
                model.FlightFrom   = flight.From;
                model.FlightTo     = flight.To;
                model.FlightDepart = flight.Depart;
                model.FlightTime   = flight.Time;
            }
            SetPassengerDetailsViewBag();
            return View(model);
        }

        TempData["PassengerDetails"] = JsonSerializer.Serialize(model);
        return RedirectToAction("ChooseSeats", "Seat", new { flightId = model.FlightId });
    }

    // ── Step 3 of booking flow: Confirm & Pay ──────────────────────────────────

    [Authorize]
    public IActionResult Confirm()
    {
        var passengerJson = TempData["PassengerDetails"] as string;
        var seatIdStr     = TempData["SelectedSeatId"] as string;

        TempData.Keep("PassengerDetails");
        TempData.Keep("SelectedSeatId");

        if (string.IsNullOrEmpty(passengerJson) || string.IsNullOrEmpty(seatIdStr))
            return RedirectToAction("Index", "Flight");

        PassengerDetailsViewModel? passenger;
        try
        {
            passenger = JsonSerializer.Deserialize<PassengerDetailsViewModel>(passengerJson);
        }
        catch
        {
            return RedirectToAction("Index", "Flight");
        }
        if (passenger == null || !int.TryParse(seatIdStr, out int seatId))
            return RedirectToAction("Index", "Flight");

        var seat = _context.Seats.FirstOrDefault(s => s.SeatId == seatId);
        if (seat == null)
            return RedirectToAction("Index", "Flight");

        var vm = new BookingConfirmationViewModel
        {
            FlightId            = passenger.FlightId,
            FlightFrom          = passenger.FlightFrom,
            FlightTo            = passenger.FlightTo,
            FlightDepart        = passenger.FlightDepart,
            FlightTime          = passenger.FlightTime,
            PassengerName       = passenger.PassengerName,
            PassengerDob        = passenger.PassengerDob,
            PassengerPassportId = passenger.PassengerPassportId,
            PassengerPhone      = passenger.PassengerPhone,
            SeatId              = seatId,
            SeatNumber          = seat.SeatNumber,
            Price               = 150m
        };

        SetConfirmViewBag();
        return View(vm);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Confirm(BookingConfirmationViewModel model)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return RedirectToAction("SignIn", "Users");

        var seat = _context.Seats.FirstOrDefault(s => s.SeatId == model.SeatId);
        if (seat == null || seat.IsOccupied)
        {
            // Seat taken between steps — send back to seat selection
            TempData["ErrorMessage"] = _localization.Getkey("Selected seat is no longer available.").Value;
            var passengerRedo = new PassengerDetailsViewModel
            {
                FlightId            = model.FlightId,
                PassengerName       = model.PassengerName,
                PassengerDob        = model.PassengerDob,
                PassengerPassportId = model.PassengerPassportId,
                PassengerPhone      = model.PassengerPhone
            };
            TempData["PassengerDetails"] = JsonSerializer.Serialize(passengerRedo);
            return RedirectToAction("ChooseSeats", "Seat", new { flightId = model.FlightId });
        }

        seat.IsOccupied = true;
        _context.Update(seat);

        var booking = new Booking
        {
            UserNo              = int.Parse(userIdClaim.Value),
            FlightId            = model.FlightId,
            SeatId              = model.SeatId,
            PassengerName       = model.PassengerName,
            PassengerDob        = model.PassengerDob,
            PassengerPassportId = model.PassengerPassportId,
            PassengerPhone      = model.PassengerPhone
        };

        _repository.Add(booking);
        _repository.SaveChanges();
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = _localization.Getkey("Your booking has been created successfully.").Value;
        return RedirectToAction("MyBookings");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SetPassengerDetailsViewBag()
    {
        ViewBag.PassengerDetails = _localization.Getkey("Passenger Details").Value;
        ViewBag.FullName         = _localization.Getkey("Full Name").Value;
        ViewBag.DateOfBirth      = _localization.Getkey("Date of Birth").Value;
        ViewBag.PassportId       = _localization.Getkey("Passport / ID Number").Value;
        ViewBag.Phone            = _localization.Getkey("Phone").Value;
        ViewBag.Continue         = _localization.Getkey("Continue to Seat Selection").Value;
        ViewBag.FlightSummary    = _localization.Getkey("Flight Summary").Value;
        ViewBag.DepartureDate    = _localization.Getkey("Departure Date").Value;
    }

    private void SetConfirmViewBag()
    {
        ViewBag.BookingSummary = _localization.Getkey("Booking Summary").Value;
        ViewBag.ConfirmPay     = _localization.Getkey("Confirm & Pay").Value;
        ViewBag.TotalPrice     = _localization.Getkey("Total Price").Value;
        ViewBag.FlightSummary  = _localization.Getkey("Flight Summary").Value;
        ViewBag.PassengerInfo  = _localization.Getkey("Passenger Information").Value;
        ViewBag.SeatInfo       = _localization.Getkey("Seat Information").Value;
        ViewBag.FullName       = _localization.Getkey("Full Name").Value;
        ViewBag.DateOfBirth    = _localization.Getkey("Date of Birth").Value;
        ViewBag.PassportId     = _localization.Getkey("Passport / ID Number").Value;
        ViewBag.Phone          = _localization.Getkey("Phone").Value;
        ViewBag.SeatNumber     = _localization.Getkey("Seat Number").Value;
        ViewBag.DepartureDate  = _localization.Getkey("Departure Date").Value;
    }
}
