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
    public class EnrolmentTests
    {
        [Fact]
        public async Task Participant_CanEnrolInEventWithCategory_Returns201CreatedAndRecordsLink()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Org", "org@enrol.com", "Organiser");
            var participant = TestHelper.CreateTestUser(context, hasher, "Sphe Runner", "sphe@enrol.com", "Participant");

            var ev = TestHelper.CreateTestEvent(context, organiser, "Soweto 10km");
            var category = TestHelper.CreateTestCategory(context, ev, "Open 10km");

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(participant);
            var controller = new EnrolmentsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new CreateEnrolmentDto
            {
                EventID = ev.EventID,
                CategoryID = category.CategoryID
            };

            // Act
            var result = await controller.Enrol(dto);

            // Assert
            var createdAtAction = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdAtAction.StatusCode.Should().Be(StatusCodes.Status201Created);

            var enrolmentResponse = createdAtAction.Value.Should().BeOfType<EnrolmentResponseDto>().Subject;
            enrolmentResponse.EventID.Should().Be(ev.EventID);
            enrolmentResponse.CategoryID.Should().Be(category.CategoryID);
            enrolmentResponse.ParticipantID.Should().Be(participant.UserID);
            enrolmentResponse.Status.Should().Be("Confirmed");

            // Verify in database
            var dbRecord = context.Enrolments.FirstOrDefault(e => e.EnrolmentID == enrolmentResponse.EnrolmentID);
            dbRecord.Should().NotBeNull();
            dbRecord!.EventID.Should().Be(ev.EventID);
            dbRecord.CategoryID.Should().Be(category.CategoryID);
            dbRecord.ParticipantID.Should().Be(participant.UserID);
        }

        [Fact]
        public async Task Participant_EnrollingInSameEventTwice_Returns409Conflict()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Org", "org2@enrol.com", "Organiser");
            var participant = TestHelper.CreateTestUser(context, hasher, "Runner", "runner2@enrol.com", "Participant");

            var ev = TestHelper.CreateTestEvent(context, organiser, "Comrades");
            var cat1 = TestHelper.CreateTestCategory(context, ev, "Senior");
            var cat2 = TestHelper.CreateTestCategory(context, ev, "Junior");

            // First enrolment
            TestHelper.CreateTestEnrolment(context, ev, cat1, participant);

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(participant);
            var controller = new EnrolmentsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var secondAttemptDto = new CreateEnrolmentDto
            {
                EventID = ev.EventID,
                CategoryID = cat2.CategoryID
            };

            // Act
            var result = await controller.Enrol(secondAttemptDto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        }

        [Fact]
        public async Task Participant_EnrollingInNonExistentEvent_Returns404NotFound()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var participant = TestHelper.CreateTestUser(context, hasher, "Runner", "runner3@enrol.com", "Participant");

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(participant);
            var controller = new EnrolmentsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new CreateEnrolmentDto
            {
                EventID = 99999,
                CategoryID = 1
            };

            // Act
            var result = await controller.Enrol(dto);

            // Assert
            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task Participant_EnrollingWithCategoryFromDifferentEvent_Returns400BadRequest()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Org", "org4@enrol.com", "Organiser");
            var participant = TestHelper.CreateTestUser(context, hasher, "Runner", "runner4@enrol.com", "Participant");

            var ev1 = TestHelper.CreateTestEvent(context, organiser, "Event 1");
            var ev2 = TestHelper.CreateTestEvent(context, organiser, "Event 2");
            var catOfEv2 = TestHelper.CreateTestCategory(context, ev2, "Event 2 Category");

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(participant);
            var controller = new EnrolmentsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var mismatchDto = new CreateEnrolmentDto
            {
                EventID = ev1.EventID,
                CategoryID = catOfEv2.CategoryID // Mismatched category!
            };

            // Act
            var result = await controller.Enrol(mismatchDto);

            // Assert
            var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        }

        [Fact]
        public async Task Participant_CanViewMyEnrolments_ReturnsOnlyOwnEnrolments()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Org", "org5@enrol.com", "Organiser");
            var participant1 = TestHelper.CreateTestUser(context, hasher, "Participant 1", "p1@enrol.com", "Participant");
            var participant2 = TestHelper.CreateTestUser(context, hasher, "Participant 2", "p2@enrol.com", "Participant");

            var ev = TestHelper.CreateTestEvent(context, organiser, "Event 1");
            var cat = TestHelper.CreateTestCategory(context, ev, "Category 1");

            TestHelper.CreateTestEnrolment(context, ev, cat, participant1);
            TestHelper.CreateTestEnrolment(context, ev, cat, participant2);

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(participant1);
            var controller = new EnrolmentsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            // Act
            var result = await controller.GetMyEnrolments();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var list = okResult.Value.Should().BeAssignableTo<IEnumerable<EnrolmentResponseDto>>().Subject.ToList();
            list.Should().HaveCount(1);
            list[0].ParticipantID.Should().Be(participant1.UserID);
        }

        [Fact]
        public async Task Organiser_CanUpdateEnrolmentStatus_Returns200OkWithNewStatus()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Org", "org6@enrol.com", "Organiser");
            var participant = TestHelper.CreateTestUser(context, hasher, "Runner", "runner6@enrol.com", "Participant");

            var ev = TestHelper.CreateTestEvent(context, organiser, "Cape Town Cycle");
            var cat = TestHelper.CreateTestCategory(context, ev, "Elite");
            var enrolment = TestHelper.CreateTestEnrolment(context, ev, cat, participant, "Pending");

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(organiser);
            var controller = new EnrolmentsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var updateDto = new UpdateEnrolmentStatusDto { Status = "Confirmed" };

            // Act
            var result = await controller.UpdateStatus(enrolment.EnrolmentID, updateDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<EnrolmentResponseDto>().Subject;
            response.Status.Should().Be("Confirmed");

            context.Enrolments.Find(enrolment.EnrolmentID)!.Status.Should().Be("Confirmed");
        }
    }
}
