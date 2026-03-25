using System.ComponentModel.DataAnnotations;

namespace AirlineSeatReservationSystem.Models;

public class PassengerDetailsViewModel
{
    public int FlightId { get; set; }

    // Flight display fields (no validation — populated by controller)
    public string? FlightFrom { get; set; }
    public string? FlightTo { get; set; }
    public string? FlightDepart { get; set; }
    public string? FlightTime { get; set; }

    [Required]
    public string? PassengerName { get; set; }

    [Required]
    public string? PassengerDob { get; set; }

    [Required]
    public string? PassengerPassportId { get; set; }

    [Required]
    public string? PassengerPhone { get; set; }
}
