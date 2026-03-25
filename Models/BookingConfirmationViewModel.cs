namespace AirlineSeatReservationSystem.Models;

public class BookingConfirmationViewModel
{
    public int FlightId { get; set; }
    public string? FlightFrom { get; set; }
    public string? FlightTo { get; set; }
    public string? FlightDepart { get; set; }
    public string? FlightTime { get; set; }

    public string? PassengerName { get; set; }
    public string? PassengerDob { get; set; }
    public string? PassengerPassportId { get; set; }
    public string? PassengerPhone { get; set; }

    public int SeatId { get; set; }
    public string? SeatNumber { get; set; }

    public decimal Price { get; set; } = 150m;
}
