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
    /// Manages participant event enrolments.
    /// Participants can enrol in events and view their personal enrolments.
    /// Organisers can view enrolments for their events and update enrolment status.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class EnrolmentsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly ISessionManager _sessionManager;

        public EnrolmentsController(RaceDayDbContext context, ISessionManager sessionManager)
        {
            _context = context;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Allows an authenticated Participant to enrol in an event under a chosen category.
        /// </summary>
        /// <param name="dto">The enrolment request containing eventId and categoryId.</param>
        /// <returns>Created enrolment record.</returns>
        /// <response code="201">Enrolment created successfully.</response>
        /// <response code="400">Invalid payload or category does not belong to specified event.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not a Participant.</response>
        /// <response code="404">Event or category not found.</response>
        /// <response code="409">Participant is already enrolled in this event.</response>
        [HttpPost]
        [SessionAuthorize("Participant")]
        [ProducesResponseType(typeof(EnrolmentResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Enrol([FromBody] CreateEnrolmentDto dto)
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

            var category = await _context.Categories.FindAsync(dto.CategoryID);
            if (category == null)
            {
                return NotFound(new { error = "NotFound", message = $"Category with ID {dto.CategoryID} not found." });
            }

            // Verify category belongs to this event
            if (category.EventID != dto.EventID)
            {
                return BadRequest(new
                {
                    error = "BadRequest",
                    message = $"Category ID {dto.CategoryID} does not belong to Event ID {dto.EventID}."
                });
            }

            // Verify participant not already enrolled
            var alreadyEnrolled = await _context.Enrolments
                .AnyAsync(e => e.EventID == dto.EventID && e.ParticipantID == session.UserID);

            if (alreadyEnrolled)
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    error = "Conflict",
                    message = "You are already enrolled in this event."
                });
            }

            var enrolment = new Enrolment
            {
                EventID = dto.EventID,
                CategoryID = dto.CategoryID,
                ParticipantID = session.UserID,
                Status = "Confirmed"
            };

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            var response = new EnrolmentResponseDto
            {
                EnrolmentID = enrolment.EnrolmentID,
                EventID = ev.EventID,
                EventName = ev.Name,
                EventDate = ev.Date,
                EventLocation = ev.Location,
                CategoryID = category.CategoryID,
                CategoryName = category.Name,
                ParticipantID = session.UserID,
                ParticipantName = session.Name,
                ParticipantEmail = session.Email,
                Status = enrolment.Status,
                Result = null
            };

            return CreatedAtAction(nameof(GetById), new { id = enrolment.EnrolmentID }, response);
        }

        /// <summary>
        /// Retrieves the personal enrolments of the logged-in Participant.
        /// </summary>
        /// <returns>List of enrolments.</returns>
        /// <response code="200">Participant's enrolments.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not a Participant.</response>
        [HttpGet("my-enrolments")]
        [SessionAuthorize("Participant")]
        [ProducesResponseType(typeof(IEnumerable<EnrolmentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var enrolments = await _context.Enrolments
                .Where(e => e.ParticipantID == session.UserID)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Participant)
                .Include(e => e.Results)
                .OrderByDescending(e => e.Event != null ? e.Event.Date : DateTime.MinValue)
                .Select(e => new EnrolmentResponseDto
                {
                    EnrolmentID = e.EnrolmentID,
                    EventID = e.EventID,
                    EventName = e.Event != null ? e.Event.Name : string.Empty,
                    EventDate = e.Event != null ? e.Event.Date : DateTime.MinValue,
                    EventLocation = e.Event != null ? e.Event.Location : string.Empty,
                    CategoryID = e.CategoryID,
                    CategoryName = e.Category != null ? e.Category.Name : string.Empty,
                    ParticipantID = e.ParticipantID,
                    ParticipantName = e.Participant != null ? e.Participant.Name : session.Name,
                    ParticipantEmail = e.Participant != null ? e.Participant.Email : session.Email,
                    Status = e.Status,
                    Result = e.Results.Select(r => new ResultResponseDto
                    {
                        ResultID = r.ResultID,
                        EnrolmentID = r.EnrolmentID,
                        EventName = e.Event != null ? e.Event.Name : string.Empty,
                        CategoryName = e.Category != null ? e.Category.Name : string.Empty,
                        ParticipantName = e.Participant != null ? e.Participant.Name : string.Empty,
                        FinishTime = r.FinishTime.HasValue ? r.FinishTime.Value.ToString(@"hh\:mm\:ss") : string.Empty,
                        Position = r.Position
                    }).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        /// <summary>
        /// Retrieves all enrolments for a specific event. Only accessible to Organisers.
        /// </summary>
        /// <param name="eventId">Event ID.</param>
        /// <returns>List of enrolments for the event.</returns>
        /// <response code="200">List of enrolments.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("event/{eventId:int}")]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(IEnumerable<EnrolmentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventEnrolments(int eventId)
        {
            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null)
            {
                return NotFound(new { error = "NotFound", message = $"Event with ID {eventId} not found." });
            }

            var enrolments = await _context.Enrolments
                .Where(e => e.EventID == eventId)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Participant)
                .Include(e => e.Results)
                .Select(e => new EnrolmentResponseDto
                {
                    EnrolmentID = e.EnrolmentID,
                    EventID = e.EventID,
                    EventName = ev.Name,
                    EventDate = ev.Date,
                    EventLocation = ev.Location,
                    CategoryID = e.CategoryID,
                    CategoryName = e.Category != null ? e.Category.Name : string.Empty,
                    ParticipantID = e.ParticipantID,
                    ParticipantName = e.Participant != null ? e.Participant.Name : string.Empty,
                    ParticipantEmail = e.Participant != null ? e.Participant.Email : string.Empty,
                    Status = e.Status,
                    Result = e.Results.Select(r => new ResultResponseDto
                    {
                        ResultID = r.ResultID,
                        EnrolmentID = r.EnrolmentID,
                        EventName = ev.Name,
                        CategoryName = e.Category != null ? e.Category.Name : string.Empty,
                        ParticipantName = e.Participant != null ? e.Participant.Name : string.Empty,
                        FinishTime = r.FinishTime.HasValue ? r.FinishTime.Value.ToString(@"hh\:mm\:ss") : string.Empty,
                        Position = r.Position
                    }).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(enrolments);
        }

        /// <summary>
        /// Retrieves a single enrolment by ID.
        /// </summary>
        /// <param name="id">Enrolment ID.</param>
        /// <returns>Enrolment details.</returns>
        /// <response code="200">Enrolment details.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="404">Enrolment not found.</response>
        [HttpGet("{id:int}")]
        [SessionAuthorize]
        [ProducesResponseType(typeof(EnrolmentResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var e = await _context.Enrolments
                .Include(en => en.Event)
                .Include(en => en.Category)
                .Include(en => en.Participant)
                .Include(en => en.Results)
                .FirstOrDefaultAsync(en => en.EnrolmentID == id);

            if (e == null)
            {
                return NotFound(new { error = "NotFound", message = $"Enrolment with ID {id} not found." });
            }

            var dto = new EnrolmentResponseDto
            {
                EnrolmentID = e.EnrolmentID,
                EventID = e.EventID,
                EventName = e.Event?.Name ?? string.Empty,
                EventDate = e.Event?.Date ?? DateTime.MinValue,
                EventLocation = e.Event?.Location,
                CategoryID = e.CategoryID,
                CategoryName = e.Category?.Name ?? string.Empty,
                ParticipantID = e.ParticipantID,
                ParticipantName = e.Participant?.Name ?? string.Empty,
                ParticipantEmail = e.Participant?.Email ?? string.Empty,
                Status = e.Status,
                Result = e.Results.Select(r => new ResultResponseDto
                {
                    ResultID = r.ResultID,
                    EnrolmentID = r.EnrolmentID,
                    EventName = e.Event?.Name ?? string.Empty,
                    CategoryName = e.Category?.Name ?? string.Empty,
                    ParticipantName = e.Participant?.Name ?? string.Empty,
                    FinishTime = r.FinishTime.HasValue ? r.FinishTime.Value.ToString(@"hh\:mm\:ss") : string.Empty,
                    Position = r.Position
                }).FirstOrDefault()
            };

            return Ok(dto);
        }

        /// <summary>
        /// Updates the status of an enrolment (Pending, Confirmed, Cancelled). Organisers only.
        /// </summary>
        /// <param name="id">Enrolment ID.</param>
        /// <param name="dto">Status update payload.</param>
        /// <returns>Updated enrolment.</returns>
        /// <response code="200">Status updated.</response>
        /// <response code="400">Invalid status.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser.</response>
        /// <response code="404">Enrolment not found.</response>
        [HttpPut("{id:int}/status")]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(EnrolmentResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateEnrolmentStatusDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var enrolment = await _context.Enrolments
                .Include(en => en.Event)
                .Include(en => en.Category)
                .Include(en => en.Participant)
                .FirstOrDefaultAsync(en => en.EnrolmentID == id);

            if (enrolment == null)
            {
                return NotFound(new { error = "NotFound", message = $"Enrolment with ID {id} not found." });
            }

            enrolment.Status = dto.Status.Trim();
            await _context.SaveChangesAsync();

            var response = new EnrolmentResponseDto
            {
                EnrolmentID = enrolment.EnrolmentID,
                EventID = enrolment.EventID,
                EventName = enrolment.Event?.Name ?? string.Empty,
                EventDate = enrolment.Event?.Date ?? DateTime.MinValue,
                EventLocation = enrolment.Event?.Location,
                CategoryID = enrolment.CategoryID,
                CategoryName = enrolment.Category?.Name ?? string.Empty,
                ParticipantID = enrolment.ParticipantID,
                ParticipantName = enrolment.Participant?.Name ?? string.Empty,
                ParticipantEmail = enrolment.Participant?.Email ?? string.Empty,
                Status = enrolment.Status
            };

            return Ok(response);
        }
    }
}
