using System;
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public class CreateEnrolmentDto
    {
        [Required]
        public int EventID { get; set; }

        [Required]
        public int CategoryID { get; set; }
    }

    public class UpdateEnrolmentStatusDto
    {
        [Required]
        [RegularExpression("^(Pending|Confirmed|Cancelled)$", ErrorMessage = "Status must be 'Pending', 'Confirmed', or 'Cancelled'.")]
        public string Status { get; set; } = "Pending";
    }

    public class EnrolmentResponseDto
    {
        public int EnrolmentID { get; set; }
        public int EventID { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string? EventLocation { get; set; }
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int ParticipantID { get; set; }
        public string ParticipantName { get; set; } = string.Empty;
        public string ParticipantEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public ResultResponseDto? Result { get; set; }
    }
}
