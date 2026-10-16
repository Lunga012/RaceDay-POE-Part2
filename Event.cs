using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RaceDay.Api.Models
{
    /// <summary>
    /// Represents a sports event (e.g. running, walking, cycling).
    /// </summary>
    [Table("Event")]
    public class Event
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EventID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime Date { get; set; }

        [MaxLength(100)]
        public string? Location { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Distance { get; set; }

        [MaxLength(20)]
        public string? Type { get; set; } // Run, Walk, Cycle

        [Required]
        public int OrganiserID { get; set; }

        [ForeignKey("OrganiserID")]
        public User? Organiser { get; set; }

        public ICollection<Category> Categories { get; set; } = new List<Category>();

        [JsonIgnore]
        public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    }
}
