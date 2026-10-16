using System;
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
    /// Handles user authentication, registration, session management, and logout.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ISessionManager _sessionManager;

        public AuthController(
            RaceDayDbContext context,
            IPasswordHasher passwordHasher,
            ISessionManager sessionManager)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Registers a new user account with a specified role (Organiser or Participant).
        /// </summary>
        /// <param name="dto">The registration payload.</param>
        /// <returns>The created user details and active session token.</returns>
        /// <response code="201">User registered and authenticated successfully.</response>
        /// <response code="400">Invalid registration data or role.</response>
        /// <response code="409">A user with this email address already exists.</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var emailNormalized = dto.Email.Trim().ToLowerInvariant();

            // Check duplicate email
            var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == emailNormalized);
            if (exists)
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    error = "Conflict",
                    message = $"A user with email '{dto.Email}' already exists."
                });
            }

            // Find role
            var role = await _context.Roles.FirstOrDefaultAsync(r =>
                r.RoleName.ToLower() == dto.Role.Trim().ToLower());

            if (role == null)
            {
                return BadRequest(new
                {
                    error = "InvalidRole",
                    message = $"Role '{dto.Role}' is invalid. Allowed roles are: 'Organiser', 'Participant'."
                });
            }

            // Create user
            var user = new User
            {
                Name = dto.Name.Trim(),
                Email = emailNormalized,
                PasswordHash = _passwordHasher.HashPassword(dto.Password),
                RoleID = role.RoleID,
                Role = role
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Create session
            var session = _sessionManager.CreateSession(user, HttpContext);

            var response = new AuthResponseDto
            {
                UserID = user.UserID,
                Name = user.Name,
                Email = user.Email,
                RoleID = user.RoleID,
                RoleName = role.RoleName,
                SessionToken = session.SessionToken,
                Message = $"Registration successful as {role.RoleName}."
            };

            return StatusCode(StatusCodes.Status201Created, response);
        }

        /// <summary>
        /// Authenticates a user and starts an authenticated session.
        /// </summary>
        /// <param name="dto">The login credentials.</param>
        /// <returns>Authenticated user profile and session token.</returns>
        /// <response code="200">Login successful; session established.</response>
        /// <response code="400">Missing or malformed credentials.</response>
        /// <response code="401">Invalid email or password.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var emailNormalized = dto.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNormalized);

            if (user == null || !_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new
                {
                    error = "Unauthorized",
                    message = "Invalid email or password."
                });
            }

            // Create session
            var session = _sessionManager.CreateSession(user, HttpContext);

            var response = new AuthResponseDto
            {
                UserID = user.UserID,
                Name = user.Name,
                Email = user.Email,
                RoleID = user.RoleID,
                RoleName = user.Role?.RoleName ?? "Unknown",
                SessionToken = session.SessionToken,
                Message = $"Welcome back, {user.Name}!"
            };

            return Ok(response);
        }

        /// <summary>
        /// Terminates the current authenticated session.
        /// </summary>
        /// <returns>Confirmation message.</returns>
        /// <response code="200">Logged out successfully.</response>
        /// <response code="401">User is not logged in.</response>
        [HttpPost("logout")]
        [SessionAuthorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Logout()
        {
            _sessionManager.EndSession(HttpContext);
            return Ok(new { message = "Logged out successfully. Session cleared." });
        }

        /// <summary>
        /// Gets current authenticated user details from active session.
        /// </summary>
        /// <returns>Session info.</returns>
        /// <response code="200">Current session details.</response>
        /// <response code="401">User is not logged in.</response>
        [HttpGet("me")]
        [SessionAuthorize]
        [ProducesResponseType(typeof(UserSessionInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Me()
        {
            var session = _sessionManager.GetCurrentSession(HttpContext);
            return Ok(session);
        }
    }
}
