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
    /// Manages race results (finish times and positions).
    /// Organisers can capture and update results; Participants can view their personal race history.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ResultsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly ISessionManager _sessionManager;

        public ResultsController(RaceDayDbContext context, ISessionManager sessionManager)
        {
            _context = context;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Captures a finish time and position for an enrolled participant. Organiser only.
        /// </summary>
        /// <param name="dto">Result data including EnrolmentID, finish time (hh:mm:ss), and position.</param>
        /// <returns>Created result record.</returns>
        /// <response code="201">Result recorded successfully.</response>
        /// <response code="400">Invalid time format or payload.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser.</response>
        /// <response code="404">Enrolment not found.</response>
        /// <response code="409">Result for this enrolment already exists.</response>
        [HttpPost]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(ResultResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CaptureResult([FromBody] CreateResultDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TimeSpan.TryParse(dto.FinishTime, out var parsedTime))
            {
                return BadRequest(new
                {
                    error = "InvalidTimeFormat",
                    message = "Finish time must be in valid format (e.g. '01:15:00' or '06:45:00')."
                });
            }

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Include(e => e.Participant)
                .FirstOrDefaultAsync(e => e.EnrolmentID == dto.EnrolmentID);

            if (enrolment == null)
            {
                return NotFound(new { error = "NotFound", message = $"Enrolment with ID {dto.EnrolmentID} not found." });
            }

            // Check if result already recorded for this enrolment
            var existingResult = await _context.Results
                .FirstOrDefaultAsync(r => r.EnrolmentID == dto.EnrolmentID);

            if (existingResult != null)
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    error = "Conflict",
                    message = $"A result has already been recorded for Enrolment ID {dto.EnrolmentID}. Use PUT to update."
                });
            }

            var result = new Result
            {
                EnrolmentID = dto.EnrolmentID,
                FinishTime = parsedTime,
                Position = dto.Position
            };

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            var response = new ResultResponseDto
            {
                ResultID = result.ResultID,
                EnrolmentID = result.EnrolmentID,
                EventName = enrolment.Event?.Name ?? string.Empty,
                EventDate = enrolment.Event?.Date ?? DateTime.MinValue,
                CategoryName = enrolment.Category?.Name ?? string.Empty,
                ParticipantID = enrolment.ParticipantID,
                ParticipantName = enrolment.Participant?.Name ?? string.Empty,
                FinishTime = parsedTime.ToString(@"hh\:mm\:ss"),
                Position = result.Position
            };

            return CreatedAtAction(nameof(GetById), new { id = result.ResultID }, response);
        }

        /// <summary>
        /// Updates a finish time and/or position for an existing result. Organiser only.
        /// </summary>
        /// <param name="id">Result ID to update.</param>
        /// <param name="dto">Updated finish time and position.</param>
        /// <returns>Updated result record.</returns>
        /// <response code="200">Result updated successfully.</response>
        /// <response code="400">Invalid payload or time format.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not an Organiser.</response>
        /// <response code="404">Result not found.</response>
        [HttpPut("{id:int}")]
        [SessionAuthorize("Organiser")]
        [ProducesResponseType(typeof(ResultResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateResult(int id, [FromBody] UpdateResultDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TimeSpan.TryParse(dto.FinishTime, out var parsedTime))
            {
                return BadRequest(new
                {
                    error = "InvalidTimeFormat",
                    message = "Finish time must be in valid format (e.g. '01:15:00' or '06:45:00')."
                });
            }

            var result = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Event)
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Category)
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Participant)
                .FirstOrDefaultAsync(r => r.ResultID == id);

            if (result == null)
            {
                return NotFound(new { error = "NotFound", message = $"Result with ID {id} not found." });
            }

            result.FinishTime = parsedTime;
            result.Position = dto.Position;

            await _context.SaveChangesAsync();

            var response = new ResultResponseDto
            {
                ResultID = result.ResultID,
                EnrolmentID = result.EnrolmentID,
                EventName = result.Enrolment?.Event?.Name ?? string.Empty,
                EventDate = result.Enrolment?.Event?.Date ?? DateTime.MinValue,
                CategoryName = result.Enrolment?.Category?.Name ?? string.Empty,
                ParticipantID = result.Enrolment?.ParticipantID ?? 0,
                ParticipantName = result.Enrolment?.Participant?.Name ?? string.Empty,
                FinishTime = parsedTime.ToString(@"hh\:mm\:ss"),
                Position = result.Position
            };

            return Ok(response);
        }

        /// <summary>
        /// Retrieves the personal race history and results for the currently logged-in Participant.
        /// </summary>
        /// <returns>List of results across completed events.</returns>
        /// <response code="200">Participant's race results history.</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Forbidden: user is not a Participant.</response>
        [HttpGet("my-results")]
        [SessionAuthorize("Participant")]
        [ProducesResponseType(typeof(IEnumerable<ResultResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyResults()
        {
            var session = _sessionManager.GetCurrentSession(HttpContext);
            if (session == null) return Unauthorized();

            var results = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Event)
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Category)
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Participant)
                .Where(r => r.Enrolment != null && r.Enrolment.ParticipantID == session.UserID)
                .OrderByDescending(r => r.Enrolment!.Event != null ? r.Enrolment.Event.Date : DateTime.MinValue)
                .Select(r => new ResultResponseDto
                {
                    ResultID = r.ResultID,
                    EnrolmentID = r.EnrolmentID,
                    EventName = r.Enrolment!.Event != null ? r.Enrolment.Event.Name : string.Empty,
                    EventDate = r.Enrolment!.Event != null ? r.Enrolment.Event.Date : DateTime.MinValue,
                    CategoryName = r.Enrolment!.Category != null ? r.Enrolment.Category.Name : string.Empty,
                    ParticipantID = r.Enrolment.ParticipantID,
                    ParticipantName = r.Enrolment.Participant != null ? r.Enrolment.Participant.Name : session.Name,
                    FinishTime = r.FinishTime.HasValue ? r.FinishTime.Value.ToString(@"hh\:mm\:ss") : string.Empty,
                    Position = r.Position
                })
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>
        /// Retrieves published results for a specific event. Publicly accessible.
        /// </summary>
        /// <param name="eventId">The event ID.</param>
        /// <returns>List of finishing results for the event ordered by position/time.</returns>
        /// <response code="200">List of race results.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("event/{eventId:int}")]
        [ProducesResponseType(typeof(IEnumerable<ResultResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null)
            {
                return NotFound(new { error = "NotFound", message = $"Event with ID {eventId} not found." });
            }

            var results = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Category)
                .Include(r => r.Enrolment)
                    .ThenInclude(e => e!.Participant)
                .Where(r => r.Enrolment != null && r.Enrolment.EventID == eventId)
                .OrderBy(r => r.Position.HasValue ? r.Position.Value : 999999)
                .ThenBy(r => r.FinishTime)
                .Select(r => new ResultResponseDto
                {
                    ResultID = r.ResultID,
                    EnrolmentID = r.EnrolmentID,
                    EventName = ev.Name,
                    EventDate = ev.Date,
                    CategoryName = r.Enrolment!.Category != null ? r.Enrolment.Category.Name : string.Empty,
                    ParticipantID = r.Enrolment.ParticipantID,
                    ParticipantName = r.Enrolment.Participant != null ? r.Enrolment.Participant.Name : string.Empty,
                    FinishTime = r.FinishTime.HasValue ? r.FinishTime.Value.ToString(@"hh\:mm\:ss") : string.Empty,
                    Position = r.Position
                })
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>
        /// Retrieves a single result by ID.
        /// </summary>
        /// <param name="id">Result ID.</param>
        /// <returns>Result details.</returns>
        /// <response code="200">Result details.</response>
        /// <response code="404">Result not found.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ResultResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var r = await _context.Results
                .Include(res => res.Enrolment)
                    .ThenInclude(e => e!.Event)
                .Include(res => res.Enrolment)
                    .ThenInclude(e => e!.Category)
                .Include(res => res.Enrolment)
                    .ThenInclude(e => e!.Participant)
                .FirstOrDefaultAsync(res => res.ResultID == id);

            if (r == null)
            {
                return NotFound(new { error = "NotFound", message = $"Result with ID {id} not found." });
            }

            var dto = new ResultResponseDto
            {
                ResultID = r.ResultID,
                EnrolmentID = r.EnrolmentID,
                EventName = r.Enrolment?.Event?.Name ?? string.Empty,
                EventDate = r.Enrolment?.Event?.Date ?? DateTime.MinValue,
                CategoryName = r.Enrolment?.Category?.Name ?? string.Empty,
                ParticipantID = r.Enrolment?.ParticipantID ?? 0,
                ParticipantName = r.Enrolment?.Participant?.Name ?? string.Empty,
                FinishTime = r.FinishTime.HasValue ? r.FinishTime.Value.ToString(@"hh\:mm\:ss") : string.Empty,
                Position = r.Position
            };

            return Ok(dto);
        }
    }
}
