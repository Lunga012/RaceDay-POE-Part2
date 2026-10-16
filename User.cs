using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RaceDay.Api.Models
{
    /// <summary>
    /// Represents a registered user (Organiser or Participant).
    /// </summary>
    [Table("User")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public int RoleID { get; set; }

        [ForeignKey("RoleID")]
        public Role? Role { get; set; }

        [JsonIgnore]
        public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();

        [JsonIgnore]
        public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    }
}
