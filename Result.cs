using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.Api.Models
{
    /// <summary>
    /// Represents race finish results captured for an enrolled participant.
    /// </summary>
    [Table("Result")]
    public class Result
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ResultID { get; set; }

        [Required]
        public int EnrolmentID { get; set; }

        [ForeignKey("EnrolmentID")]
        public Enrolment? Enrolment { get; set; }

        public TimeSpan? FinishTime { get; set; }

        public int? Position { get; set; }
    }
}
