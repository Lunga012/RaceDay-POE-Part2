using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(RaceDayDbContext context, IPasswordHasher passwordHasher)
        {
            await context.Database.EnsureCreatedAsync();

            // Check if already seeded
            if (await context.Roles.AnyAsync())
            {
                return;
            }

            // 1. Seed Roles
            var organiserRole = new Role { RoleName = "Organiser" };
            var participantRole = new Role { RoleName = "Participant" };

            context.Roles.AddRange(organiserRole, participantRole);
            await context.SaveChangesAsync();

            // 2. Seed Users (with hashed passwords)
            var users = new List<User>
            {
                new User
                {
                    Name = "Lunga Organiser",
                    Email = "khumalolungsta121@gmail.com",
                    PasswordHash = passwordHasher.HashPassword("hashed123"),
                    RoleID = organiserRole.RoleID
                },
                new User
                {
                    Name = "Njabulo Organiser",
                    Email = "st10483560rcconnect.edu.za",
                    PasswordHash = passwordHasher.HashPassword("hashed456"),
                    RoleID = organiserRole.RoleID
                },
                new User
                {
                    Name = "Sphe Participant",
                    Email = "sihles622@gmail.com",
                    PasswordHash = passwordHasher.HashPassword("hashed789"),
                    RoleID = participantRole.RoleID
                },
                new User
                {
                    Name = "Sihle Participant",
                    Email = "lungi@walk.com",
                    PasswordHash = passwordHasher.HashPassword("hashed321"),
                    RoleID = participantRole.RoleID
                }
            };

            context.Users.AddRange(users);
            await context.SaveChangesAsync();

            var lunga = users[0];
            var njabulo = users[1];
            var sphe = users[2];
            var sihle = users[3];

            // 3. Seed Events
            var comrades = new Event
            {
                Name = "Comrades Marathon",
                Description = "Ultra run",
                Date = DateTime.Parse("2026-06-01"),
                Location = "Durban",
                Distance = 89.00m,
                Type = "Run",
                OrganiserID = lunga.UserID
            };

            var capeTownCycle = new Event
            {
                Name = "Cape Town Cycle Tour",
                Description = "Cycling event",
                Date = DateTime.Parse("2026-03-10"),
                Location = "Cape Town",
                Distance = 109.00m,
                Type = "Cycle",
                OrganiserID = njabulo.UserID
            };

            var sowetoMarathon = new Event
            {
                Name = "Soweto Marathon",
                Description = "Community run",
                Date = DateTime.Parse("2026-11-05"),
                Location = "Soweto",
                Distance = 42.20m,
                Type = "Run",
                OrganiserID = lunga.UserID
            };

            context.Events.AddRange(comrades, capeTownCycle, sowetoMarathon);
            await context.SaveChangesAsync();

            // 4. Seed Categories
            var categories = new List<Category>
            {
                new Category { EventID = comrades.EventID, Name = "Senior", Description = "Over 20 years" },
                new Category { EventID = comrades.EventID, Name = "Junior", Description = "Under 20 years" },
                new Category { EventID = capeTownCycle.EventID, Name = "Elite", Description = "Professional cyclists" },
                new Category { EventID = capeTownCycle.EventID, Name = "Open", Description = "All participants" },
                new Category { EventID = sowetoMarathon.EventID, Name = "10km", Description = "Short distance" },
                new Category { EventID = sowetoMarathon.EventID, Name = "21km", Description = "Half marathon" }
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();

            // 5. Seed Enrolments
            var enrolments = new List<Enrolment>
            {
                new Enrolment
                {
                    EventID = comrades.EventID,
                    CategoryID = categories[0].CategoryID, // Senior
                    ParticipantID = sphe.UserID,
                    Status = "Confirmed"
                },
                new Enrolment
                {
                    EventID = capeTownCycle.EventID,
                    CategoryID = categories[2].CategoryID, // Elite
                    ParticipantID = sihle.UserID,
                    Status = "Pending"
                },
                new Enrolment
                {
                    EventID = sowetoMarathon.EventID,
                    CategoryID = categories[4].CategoryID, // 10km
                    ParticipantID = sphe.UserID,
                    Status = "Confirmed"
                }
            };

            context.Enrolments.AddRange(enrolments);
            await context.SaveChangesAsync();

            // 6. Seed Results
            var results = new List<Result>
            {
                new Result
                {
                    EnrolmentID = enrolments[0].EnrolmentID,
                    FinishTime = TimeSpan.Parse("06:45:00"),
                    Position = 47
                },
                new Result
                {
                    EnrolmentID = enrolments[2].EnrolmentID,
                    FinishTime = TimeSpan.Parse("01:15:00"),
                    Position = 12
                }
            };

            context.Results.AddRange(results);
            await context.SaveChangesAsync();
        }
    }
}
