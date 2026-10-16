using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RaceDay.Api.Models
{
    /// <summary>
    /// Represents an event enrolment linking a participant to an event and category.
    /// </summary>
    [Table("Enrolment")]
    public class Enrolment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EnrolmentID { get; set; }

        [Required]
        public int EventID { get; set; }

        [ForeignKey("EventID")]
        public Event? Event { get; set; }

        [Required]
        public int CategoryID { get; set; }

        [ForeignKey("CategoryID")]
        public Category? Category { get; set; }

        [Required]
        public int ParticipantID { get; set; }

        [ForeignKey("ParticipantID")]
        public User? Participant { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled

        public ICollection<Result> Results { get; set; } = new List<Result>();
    }
}
