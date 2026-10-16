using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public class CreateCategoryDto
    {
        [Required]
        public int EventID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Description { get; set; }
    }

    public class UpdateCategoryDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Description { get; set; }
    }

    public class CategoryResponseDto
    {
        public int CategoryID { get; set; }
        public int EventID { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
