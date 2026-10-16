using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public class CreateEventDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Description { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [MaxLength(100)]
        public string? Location { get; set; }

        [Range(0.1, 999.99)]
        public decimal? Distance { get; set; }

        [MaxLength(20)]
        public string? Type { get; set; } // Run, Walk, Cycle
    }

    public class UpdateEventDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Description { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [MaxLength(100)]
        public string? Location { get; set; }

        [Range(0.1, 999.99)]
        public decimal? Distance { get; set; }

        [MaxLength(20)]
        public string? Type { get; set; }
    }

    public class EventResponseDto
    {
        public int EventID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Date { get; set; }
        public string? Location { get; set; }
        public decimal? Distance { get; set; }
        public string? Type { get; set; }
        public int OrganiserID { get; set; }
        public string OrganiserName { get; set; } = string.Empty;
        public int TotalEnrolments { get; set; }
        public List<CategoryResponseDto> Categories { get; set; } = new();
    }
}
