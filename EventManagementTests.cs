using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Tests
{
    public class EventManagementTests
    {
        [Fact]
        public async Task Organiser_CanCreateEvent_Returns201CreatedWithAllRequiredFields()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Chief Organiser", "organiser1@test.com", "Organiser");
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(organiser);

            var controller = new EventsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new CreateEventDto
            {
                Name = "Two Oceans Marathon",
                Description = "The world's most beautiful marathon",
                Date = DateTime.Parse("2026-04-18"),
                Location = "Cape Town",
                Distance = 56.00m,
                Type = "Run"
            };

            // Act
            var result = await controller.CreateEvent(dto);

            // Assert
            var createdAtAction = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdAtAction.StatusCode.Should().Be(StatusCodes.Status201Created);

            var createdEvent = createdAtAction.Value.Should().BeOfType<EventResponseDto>().Subject;
            createdEvent.Name.Should().Be("Two Oceans Marathon");
            createdEvent.Location.Should().Be("Cape Town");
            createdEvent.Distance.Should().Be(56.00m);
            createdEvent.Type.Should().Be("Run");
            createdEvent.OrganiserID.Should().Be(organiser.UserID);
        }

        [Fact]
        public async Task Organiser_CanUpdateOwnEvent_Returns200OkWithUpdatedDetails()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Event Lead", "lead@test.com", "Organiser");
            var ev = TestHelper.CreateTestEvent(context, organiser, "Initial Event");
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(organiser);

            var controller = new EventsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var updateDto = new UpdateEventDto
            {
                Name = "Updated Epic Marathon",
                Description = "Updated description",
                Date = DateTime.Parse("2026-07-20"),
                Location = "Durban North",
                Distance = 42.20m,
                Type = "Run"
            };

            // Act
            var result = await controller.UpdateEvent(ev.EventID, updateDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

            var updated = okResult.Value.Should().BeOfType<EventResponseDto>().Subject;
            updated.Name.Should().Be("Updated Epic Marathon");
            updated.Location.Should().Be("Durban North");
            updated.Distance.Should().Be(42.20m);
        }

        [Fact]
        public async Task Organiser_CannotUpdateOtherOrganisersEvent_Returns403Forbidden()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var owner = TestHelper.CreateTestUser(context, hasher, "Event Owner", "owner@test.com", "Organiser");
            var anotherOrganiser = TestHelper.CreateTestUser(context, hasher, "Another Organiser", "other@test.com", "Organiser");
            var ev = TestHelper.CreateTestEvent(context, owner, "Owner's Event");

            // Authenticate as another organiser
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(anotherOrganiser);
            var controller = new EventsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var updateDto = new UpdateEventDto
            {
                Name = "Malicious Hijack Attempt",
                Date = DateTime.UtcNow.AddDays(10)
            };

            // Act
            var result = await controller.UpdateEvent(ev.EventID, updateDto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public async Task Organiser_CanDeleteOwnEvent_Returns204NoContent()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Deleter", "delete@test.com", "Organiser");
            var ev = TestHelper.CreateTestEvent(context, organiser, "Event To Delete");
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(organiser);

            var controller = new EventsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            // Act
            var result = await controller.DeleteEvent(ev.EventID);

            // Assert
            var noContent = result.Should().BeOfType<NoContentResult>().Subject;
            noContent.StatusCode.Should().Be(StatusCodes.Status204NoContent);

            context.Events.Find(ev.EventID).Should().BeNull();
        }

        [Fact]
        public async Task Organiser_CannotDeleteOtherOrganisersEvent_Returns403Forbidden()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var owner = TestHelper.CreateTestUser(context, hasher, "Event Owner", "owner2@test.com", "Organiser");
            var stranger = TestHelper.CreateTestUser(context, hasher, "Stranger Organiser", "stranger@test.com", "Organiser");
            var ev = TestHelper.CreateTestEvent(context, owner, "Protected Event");

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(stranger);
            var controller = new EventsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            // Act
            var result = await controller.DeleteEvent(ev.EventID);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            context.Events.Find(ev.EventID).Should().NotBeNull();
        }

        [Fact]
        public async Task GetAllEvents_PublicAccess_ReturnsAllEventsWithCategories()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Main Org", "mainorg@test.com", "Organiser");

            var ev1 = TestHelper.CreateTestEvent(context, organiser, "Event 1");
            var ev2 = TestHelper.CreateTestEvent(context, organiser, "Event 2");
            TestHelper.CreateTestCategory(context, ev1, "Senior 10km");

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(); // Public / unauthenticated
            var controller = new EventsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            // Act
            var result = await controller.GetAllEvents();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var list = okResult.Value.Should().BeAssignableTo<IEnumerable<EventResponseDto>>().Subject.ToList();
            list.Count.Should().Be(2);
            list.First(e => e.EventID == ev1.EventID).Categories.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetEventById_WhenNotFound_Returns404()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext();
            var controller = new EventsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            // Act
            var result = await controller.GetEventById(99999);

            // Assert
            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }
    }
}
