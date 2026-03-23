using System.ComponentModel.DataAnnotations;

namespace AirlineSeatReservationSystem.Services
{
    public class AdminSettings
    {
        public const string ConfigurationSectionName = "Admin";
        public const string SeedPhone = "0000000000";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string NormalizedEmail => Email.Trim();

        public bool IsAdminEmail(string? email)
        {
            return !string.IsNullOrWhiteSpace(email)
                && string.Equals(email.Trim(), NormalizedEmail, StringComparison.OrdinalIgnoreCase);
        }
    }
}
