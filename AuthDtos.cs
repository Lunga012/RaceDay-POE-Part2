using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public class RegisterDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(4, ErrorMessage = "Password must be at least 4 characters long.")]
        [MaxLength(50)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty; // "Organiser" or "Participant"
    }

    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public int UserID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class UserProfileDto
    {
        public int UserID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class UpdateProfileDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? NewPassword { get; set; }
    }
}
