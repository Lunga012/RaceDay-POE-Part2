using System;
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
    /// Manages sports events (creation, retrieval, update, deletion).
    /// Viewing events is available to all users; managing events is restricted to Organisers.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly ISessionManager _sessionManager;

        public EventsController(RaceDayDbContext context, ISessionManager sessionManager)
        {
            _context = context;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Retrieves all events with categories and total enrolments.
        /// </summary>
        /// <param name="type">Optional filter by event type (Run, Walk, Cycle).</param>
        /// <returns>List of events.</returns>
        /// <response code="200">List of events.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EventResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllEvents([FromQuery] string? type = null)
        {
            var query = _context.Events
                .Include(e => e.Organiser)
                .Include(e => e.Categories)
                .Include(e => e.Enrolments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(e => e.Type != null && e.Type.ToLower() == type.Trim().ToLower());
            }

            var events = await query
                .OrderBy(e => e.Date)
                .Select(e => new EventResponseDto
                {
                    EventID = e.EventID,
                    Name = e.Name,
                    Description = e.Description,
                    Date = e.Date,
                    Location = e.Location,
                    Distance = e.Distance,
                    Type = e.Type,
                    OrganiserID = e.OrganiserID,
                    OrganiserName = e.Organiser != null ? e.Organiser.Name : string.Empty,
                    TotalEnrolments = e.Enrolments.Count,
                    Categories = e.Categories.Select(c => new CategoryResponseDto
                    {
                        CategoryID = c.CategoryID,
                        EventID = c.EventID,
                        EventName = e.Name,
                        Name = c.Name,
                        Description = c.Description
                    }).ToList()
                })
                .ToListAsync();

            return Ok(events);
        }

        /// <summary>
        /// Retrieves a single event by ID including its categories.
        /// </summary>
        /// <param name="id">The event ID.</param>
        /// <returns>Event details.</returns>
        /// <response code="200">Event details.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventById(int id)
        {
            var e = await _context.Events
                .Include(ev => ev.Organiser)
                .Include(ev => ev.Categories)
                .Include(ev => ev.Enrolments)
                .FirstOrDefaultAsync(ev => ev.EventID == id);

            if (e == null)
            {
                return NotFound(new { error = "NotFound", message = $"Event with ID {id} not found." });
            }

            var dto = new EventResponseDto
            {
                EventID = e.EventID,
                Name = e.Name,
                Description = e.Description,
                Date = e.Date,
                Location = e.Location,
                Distance = e.Distance,
                Type = e.Type,
                OrganiserID = e.OrganiserID,
                OrganiserName = e.Organiser?.Name ?? string.Empty,
                TotalEnrolments = e.Enrolments.Count,
                Categories = e.Categories.Select(c => new CategoryResponseDto
                {
                    CategoryID = c.CategoryID,
                    EventID = c.EventID,
                    EventName = e.Name,
                    Name = c.Name,
                    Description = c.Description
                }).ToList()
            };

            return Ok(dto);
        }

        /// <summary>
        /// Retrieves all events managed by the currently logged-in Organiser.
        /// </summary>
        /// <returns>List of organiser's events.</returns>
        /// <response code="200">Organiser's events.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser.</response>
        [HttpGet("my-events")]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(IEnumerable<EventResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyEvents()
        {
            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var events = await _context.Events
                .Where(e => e.OrganiserID == session.UserID)
                .Include(e => e.Organiser)
                .Include(e => e.Categories)
                .Include(e => e.Enrolments)
                .OrderBy(e => e.Date)
                .Select(e => new EventResponseDto
                {
                    EventID = e.EventID,
                    Name = e.Name,
                    Description = e.Description,
                    Date = e.Date,
                    Location = e.Location,
                    Distance = e.Distance,
                    Type = e.Type,
                    OrganiserID = e.OrganiserID,
                    OrganiserName = e.Organiser != null ? e.Organiser.Name : session.Name,
                    TotalEnrolments = e.Enrolments.Count,
                    Categories = e.Categories.Select(c => new CategoryResponseDto
                    {
                        CategoryID = c.CategoryID,
                        EventID = c.EventID,
                        EventName = e.Name,
                        Name = c.Name,
                        Description = c.Description
                    }).ToList()
                })
                .ToListAsync();

            return Ok(events);
        }

        /// <summary>
        /// Creates a new sports event. Only accessible to Organisers.
        /// </summary>
        /// <param name="dto">Event creation details.</param>
        /// <returns>Created event.</returns>
        /// <response code="201">Event created successfully.</response>
        /// <response code="400">Invalid event data.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser.</response>
        [HttpPost]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var newEvent = new Event
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                Date = dto.Date,
                Location = dto.Location?.Trim(),
                Distance = dto.Distance,
                Type = dto.Type?.Trim(),
                OrganiserID = session.UserID
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            var response = new EventResponseDto
            {
                EventID = newEvent.EventID,
                Name = newEvent.Name,
                Description = newEvent.Description,
                Date = newEvent.Date,
                Location = newEvent.Location,
                Distance = newEvent.Distance,
                Type = newEvent.Type,
                OrganiserID = newEvent.OrganiserID,
                OrganiserName = session.Name,
                TotalEnrolments = 0,
                Categories = new List<CategoryResponseDto>()
            };

            return CreatedAtAction(nameof(GetEventById), new { id = newEvent.EventID }, response);
        }

        /// <summary>
        /// Updates an existing event. Only the Organiser who owns the event can update it.
        /// </summary>
        /// <param name="id">Event ID to update.</param>
        /// <param name="dto">Updated event fields.</param>
        /// <returns>Updated event details.</returns>
        /// <response code="200">Event updated successfully.</response>
        /// <response code="400">Invalid payload.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not the owner of this event or not an Organiser.</response>
        /// <response code="404">Event not found.</response>
        [HttpPut("{id:int}")]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] UpdateEventDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var ev = await _context.Events
                .Include(e => e.Organiser)
                .Include(e => e.Categories)
                .Include(e => e.Enrolments)
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (ev == null)
            {
                return NotFound(new { error = "NotFound", message = $"Event with ID {id} not found." });
            }

            // Verify event ownership
            if (ev.OrganiserID != session.UserID)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "Forbidden",
                    message = "You can only update events you have created."
                });
            }

            ev.Name = dto.Name.Trim();
            ev.Description = dto.Description?.Trim();
            ev.Date = dto.Date;
            ev.Location = dto.Location?.Trim();
            ev.Distance = dto.Distance;
            ev.Type = dto.Type?.Trim();

            await _context.SaveChangesAsync();

            var response = new EventResponseDto
            {
                EventID = ev.EventID,
                Name = ev.Name,
                Description = ev.Description,
                Date = ev.Date,
                Location = ev.Location,
                Distance = ev.Distance,
                Type = ev.Type,
                OrganiserID = ev.OrganiserID,
                OrganiserName = ev.Organiser?.Name ?? session.Name,
                TotalEnrolments = ev.Enrolments.Count,
                Categories = ev.Categories.Select(c => new CategoryResponseDto
                {
                    CategoryID = c.CategoryID,
                    EventID = c.EventID,
                    EventName = ev.Name,
                    Name = c.Name,
                    Description = c.Description
                }).ToList()
            };

            return Ok(response);
        }

        /// <summary>
        /// Deletes an existing event. Only the Organiser who owns the event can delete it.
        /// </summary>
        /// <param name="id">Event ID to delete.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Event deleted successfully.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not the owner or not an Organiser.</response>
        /// <response code="404">Event not found.</response>
        [HttpDelete("{id:int}")]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var ev = await _context.Events
                .Include(e => e.Enrolments)
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (ev == null)
            {
                return NotFound(new { error = "NotFound", message = $"Event with ID {id} not found." });
            }

            if (ev.OrganiserID != session.UserID)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "Forbidden",
                    message = "You can only delete events you have created."
                });
            }

            // Remove associated categories and enrolments
            var categories = await _context.Categories.Where(c => c.EventID == id).ToListAsync();
            _context.Categories.RemoveRange(categories);

            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
