using System;
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public class CreateResultDto
    {
        [Required]
        public int EnrolmentID { get; set; }

        /// <summary>
        /// Finish time formatted as hh:mm:ss (e.g. 01:15:00 or 06:45:00).
        /// </summary>
        [Required]
        public string FinishTime { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Position must be greater than 0.")]
        public int? Position { get; set; }
    }

    public class UpdateResultDto
    {
        /// <summary>
        /// Finish time formatted as hh:mm:ss (e.g. 01:15:00 or 06:45:00).
        /// </summary>
        [Required]
        public string FinishTime { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Position must be greater than 0.")]
        public int? Position { get; set; }
    }

    public class ResultResponseDto
    {
        public int ResultID { get; set; }
        public int EnrolmentID { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int ParticipantID { get; set; }
        public string ParticipantName { get; set; } = string.Empty;
        public string FinishTime { get; set; } = string.Empty;
        public int? Position { get; set; }
    }
}
