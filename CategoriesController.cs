using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers
{
    /// <summary>
    /// Manages event categories (e.g. Senior, Junior, 10km, 21km).
    /// Viewing categories is available to all; creating/modifying is restricted to Organisers.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CategoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly ISessionManager _sessionManager;

        public CategoriesController(RaceDayDbContext context, ISessionManager sessionManager)
        {
            _context = context;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Retrieves all categories across all events.
        /// </summary>
        /// <returns>List of categories.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CategoryResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _context.Categories
                .Include(c => c.Event)
                .Select(c => new CategoryResponseDto
                {
                    CategoryID = c.CategoryID,
                    EventID = c.EventID,
                    EventName = c.Event != null ? c.Event.Name : string.Empty,
                    Name = c.Name,
                    Description = c.Description
                })
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>
        /// Retrieves all categories belonging to a specific event.
        /// </summary>
        /// <param name="eventId">The event ID.</param>
        /// <returns>List of categories for the specified event.</returns>
        /// <response code="200">List of categories.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("event/{eventId:int}")]
        [ProducesResponseType(typeof(IEnumerable<CategoryResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null)
            {
                return NotFound(new { error = "NotFound", message = $"Event with ID {eventId} not found." });
            }

            var categories = await _context.Categories
                .Where(c => c.EventID == eventId)
                .Select(c => new CategoryResponseDto
                {
                    CategoryID = c.CategoryID,
                    EventID = c.EventID,
                    EventName = ev.Name,
                    Name = c.Name,
                    Description = c.Description
                })
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>
        /// Retrieves a single category by ID.
        /// </summary>
        /// <param name="id">Category ID.</param>
        /// <returns>Category details.</returns>
        /// <response code="200">Category details.</response>
        /// <response code="404">Category not found.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var c = await _context.Categories
                .Include(cat => cat.Event)
                .FirstOrDefaultAsync(cat => cat.CategoryID == id);

            if (c == null)
            {
                return NotFound(new { error = "NotFound", message = $"Category with ID {id} not found." });
            }

            var dto = new CategoryResponseDto
            {
                CategoryID = c.CategoryID,
                EventID = c.EventID,
                EventName = c.Event?.Name ?? string.Empty,
                Name = c.Name,
                Description = c.Description
            };

            return Ok(dto);
        }

        /// <summary>
        /// Creates a new category for an event. Organiser only.
        /// </summary>
        /// <param name="dto">Category creation details.</param>
        /// <returns>Created category.</returns>
        /// <response code="201">Category created successfully.</response>
        /// <response code="400">Invalid payload.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser or not the owner of the event.</response>
        /// <response code="404">Referenced event not found.</response>
        [HttpPost]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var ev = await _context.Events.FindAsync(dto.EventID);
            if (ev == null)
            {
                return NotFound(new { error = "NotFound", message = $"Event with ID {dto.EventID} not found." });
            }

            if (ev.OrganiserID != session.UserID)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "Forbidden",
                    message = "You can only add categories to events you own."
                });
            }

            var category = new Category
            {
                EventID = dto.EventID,
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim()
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var response = new CategoryResponseDto
            {
                CategoryID = category.CategoryID,
                EventID = category.EventID,
                EventName = ev.Name,
                Name = category.Name,
                Description = category.Description
            };

            return CreatedAtAction(nameof(GetById), new { id = category.CategoryID }, response);
        }

        /// <summary>
        /// Updates an existing category. Organiser only.
        /// </summary>
        /// <param name="id">Category ID to update.</param>
        /// <param name="dto">Updated category fields.</param>
        /// <returns>Updated category.</returns>
        /// <response code="200">Category updated successfully.</response>
        /// <response code="400">Invalid payload.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser or not owner of the event.</response>
        /// <response code="404">Category not found.</response>
        [HttpPut("{id:int}")]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
            {
                return NotFound(new { error = "NotFound", message = $"Category with ID {id} not found." });
            }

            if (category.Event != null && category.Event.OrganiserID != session.UserID)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "Forbidden",
                    message = "You can only update categories for events you own."
                });
            }

            category.Name = dto.Name.Trim();
            category.Description = dto.Description?.Trim();

            await _context.SaveChangesAsync();

            var response = new CategoryResponseDto
            {
                CategoryID = category.CategoryID,
                EventID = category.EventID,
                EventName = category.Event?.Name ?? string.Empty,
                Name = category.Name,
                Description = category.Description
            };

            return Ok(response);
        }

        /// <summary>
        /// Deletes a category. Organiser only.
        /// </summary>
        /// <param name="id">Category ID to delete.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Category deleted.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser or not owner of the event.</response>
        /// <response code="404">Category not found.</response>
        [HttpDelete("{id:int}")]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
            {
                return NotFound(new { error = "NotFound", message = $"Category with ID {id} not found." });
            }

            if (category.Event != null && category.Event.OrganiserID != session.UserID)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "Forbidden",
                    message = "You can only delete categories for events you own."
                });
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
