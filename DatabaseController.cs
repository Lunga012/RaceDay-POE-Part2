using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers
{
    /// <summary>
    /// Utility endpoints to seed or check the status of the database matching the Part 1 specification.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class DatabaseController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IPasswordHasher _passwordHasher;

        public DatabaseController(RaceDayDbContext context, IPasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Seeds the database with the official Part 1 sample data (Organisers, Participants, Events, Categories, Enrolments, Results).
        /// </summary>
        /// <returns>Summary of seeding operation.</returns>
        [HttpPost("seed")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SeedDatabase()
        {
            await DbInitializer.SeedAsync(_context, _passwordHasher);

            var rolesCount = await _context.Roles.CountAsync();
            var usersCount = await _context.Users.CountAsync();
            var eventsCount = await _context.Events.CountAsync();
            var categoriesCount = await _context.Categories.CountAsync();
            var enrolmentsCount = await _context.Enrolments.CountAsync();
            var resultsCount = await _context.Results.CountAsync();

            return Ok(new
            {
                message = "Database seeded successfully with Part 1 data.",
                counts = new
                {
                    Roles = rolesCount,
                    Users = usersCount,
                    Events = eventsCount,
                    Categories = categoriesCount,
                    Enrolments = enrolmentsCount,
                    Results = resultsCount
                }
            });
        }

        /// <summary>
        /// Returns current database connectivity status and entity row counts.
        /// </summary>
        /// <returns>Database status.</returns>
        [HttpGet("status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatus()
        {
            var canConnect = await _context.Database.CanConnectAsync();

            return Ok(new
            {
                canConnect,
                databaseProvider = _context.Database.ProviderName,
                roles = canConnect ? await _context.Roles.CountAsync() : 0,
                users = canConnect ? await _context.Users.CountAsync() : 0,
                events = canConnect ? await _context.Events.CountAsync() : 0,
                categories = canConnect ? await _context.Categories.CountAsync() : 0,
                enrolments = canConnect ? await _context.Enrolments.CountAsync() : 0,
                results = canConnect ? await _context.Results.CountAsync() : 0
            });
        }
    }
}
