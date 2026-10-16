using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Tests
{
    public static class TestHelper
    {
        public static RaceDayDbContext CreateInMemoryDbContext(string dbName = "")
        {
            if (string.IsNullOrEmpty(dbName))
            {
                dbName = Guid.NewGuid().ToString();
            }

            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new RaceDayDbContext(options);
            context.Database.EnsureCreated();

            // Seed basic roles
            if (!context.Roles.Any())
            {
                context.Roles.AddRange(
                    new Role { RoleID = 1, RoleName = "Organiser" },
                    new Role { RoleID = 2, RoleName = "Participant" }
                );
                context.SaveChanges();
            }

            return context;
        }

        public static (DefaultHttpContext HttpContext, ISessionManager SessionManager, IPasswordHasher PasswordHasher)
            CreateTestContext(User? authenticatedUser = null)
        {
            var services = new ServiceCollection();
            services.AddDistributedMemoryCache();
            services.AddSession();

            var sessionManager = new SessionManager();
            var passwordHasher = new PasswordHasher();

            services.AddSingleton<ISessionManager>(sessionManager);
            services.AddSingleton<IPasswordHasher>(passwordHasher);

            var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            };

            // Set up a mock session implementation
            var session = new TestSession();
            httpContext.Session = session;

            if (authenticatedUser != null)
            {
                var sessionInfo = sessionManager.CreateSession(authenticatedUser, httpContext);
                httpContext.Request.Headers[SessionManager.HeaderTokenKey] = sessionInfo.SessionToken;
                httpContext.Items["UserSession"] = sessionInfo;
            }

            return (httpContext, sessionManager, passwordHasher);
        }

        public static AuthorizationFilterContext CreateAuthorizationContext(
            HttpContext httpContext,
            string[] allowedRoles)
        {
            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor()
            );

            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        public static User CreateTestUser(
            RaceDayDbContext context,
            IPasswordHasher hasher,
            string name,
            string email,
            string roleName)
        {
            var role = context.Roles.First(r => r.RoleName == roleName);
            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = hasher.HashPassword("Password123!"),
                RoleID = role.RoleID,
                Role = role
            };
            context.Users.Add(user);
            context.SaveChanges();
            return user;
        }

        public static Event CreateTestEvent(
            RaceDayDbContext context,
            User organiser,
            string name = "Standard 10km Run")
        {
            var ev = new Event
            {
                Name = name,
                Description = "A fun test event for runners",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Johannesburg",
                Distance = 10.0m,
                Type = "Run",
                OrganiserID = organiser.UserID,
                Organiser = organiser
            };
            context.Events.Add(ev);
            context.SaveChanges();
            return ev;
        }

        public static Category CreateTestCategory(
            RaceDayDbContext context,
            Event ev,
            string name = "Senior")
        {
            var cat = new Category
            {
                EventID = ev.EventID,
                Name = name,
                Description = "Senior participants over 20"
            };
            context.Categories.Add(cat);
            context.SaveChanges();
            return cat;
        }

        public static Enrolment CreateTestEnrolment(
            RaceDayDbContext context,
            Event ev,
            Category cat,
            User participant,
            string status = "Confirmed")
        {
            var enrolment = new Enrolment
            {
                EventID = ev.EventID,
                CategoryID = cat.CategoryID,
                ParticipantID = participant.UserID,
                Status = status
            };
            context.Enrolments.Add(enrolment);
            context.SaveChanges();
            return enrolment;
        }
    }

    /// <summary>
    /// In-memory implementation of ISession for unit tests.
    /// </summary>
    public class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value)
        {
            var result = _store.TryGetValue(key, out var val);
            value = val!;
            return result;
        }
    }
}
