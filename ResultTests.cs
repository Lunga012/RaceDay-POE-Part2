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
    public class ResultTests
    {
        [Fact]
        public async Task Organiser_CanCaptureParticipantResult_Returns201Created()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Chief Judge", "judge@results.com", "Organiser");
            var participant = TestHelper.CreateTestUser(context, hasher, "Fast Runner", "runner@results.com", "Participant");

            var ev = TestHelper.CreateTestEvent(context, organiser, "Comrades");
            var cat = TestHelper.CreateTestCategory(context, ev, "Open");
            var enrolment = TestHelper.CreateTestEnrolment(context, ev, cat, participant);

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(organiser);
            var controller = new ResultsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new CreateResultDto
            {
                EnrolmentID = enrolment.EnrolmentID,
                FinishTime = "05:42:30",
                Position = 25
            };

            // Act
            var result = await controller.CaptureResult(dto);

            // Assert
            var createdAtAction = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdAtAction.StatusCode.Should().Be(StatusCodes.Status201Created);

            var res = createdAtAction.Value.Should().BeOfType<ResultResponseDto>().Subject;
            res.EnrolmentID.Should().Be(enrolment.EnrolmentID);
            res.FinishTime.Should().Be("05:42:30");
            res.Position.Should().Be(25);
            res.ParticipantName.Should().Be("Fast Runner");
            res.EventName.Should().Be("Comrades");
        }

        [Fact]
        public async Task Organiser_CapturingResultWithInvalidTimeFormat_Returns400BadRequest()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Judge", "judge2@results.com", "Organiser");
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(organiser);

            var controller = new ResultsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new CreateResultDto
            {
                EnrolmentID = 1,
                FinishTime = "not-a-valid-time-format",
                Position = 1
            };

            // Act
            var result = await controller.CaptureResult(dto);

            // Assert
            var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        }

        [Fact]
        public async Task Organiser_CapturingResultForNonExistentEnrolment_Returns404NotFound()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Judge", "judge3@results.com", "Organiser");
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(organiser);

            var controller = new ResultsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new CreateResultDto
            {
                EnrolmentID = 99999,
                FinishTime = "01:00:00",
                Position = 1
            };

            // Act
            var result = await controller.CaptureResult(dto);

            // Assert
            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task Organiser_CanUpdateExistingResult_Returns200Ok()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Judge", "judge4@results.com", "Organiser");
            var participant = TestHelper.CreateTestUser(context, hasher, "Runner", "runner4@results.com", "Participant");

            var ev = TestHelper.CreateTestEvent(context, organiser, "Cycle Tour");
            var cat = TestHelper.CreateTestCategory(context, ev, "Elite");
            var enrolment = TestHelper.CreateTestEnrolment(context, ev, cat, participant);

            var existingResult = new Result
            {
                EnrolmentID = enrolment.EnrolmentID,
                FinishTime = TimeSpan.Parse("03:15:00"),
                Position = 50
            };
            context.Results.Add(existingResult);
            context.SaveChanges();

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(organiser);
            var controller = new ResultsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var updateDto = new UpdateResultDto
            {
                FinishTime = "03:12:45",
                Position = 48
            };

            // Act
            var result = await controller.UpdateResult(existingResult.ResultID, updateDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ResultResponseDto>().Subject;
            response.FinishTime.Should().Be("03:12:45");
            response.Position.Should().Be(48);
        }

        [Fact]
        public async Task Participant_CanViewPersonalResultsHistory_ReturnsOnlyOwnResults()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Org", "org5@results.com", "Organiser");
            var participant = TestHelper.CreateTestUser(context, hasher, "Sphe", "sphe@results.com", "Participant");

            var ev = TestHelper.CreateTestEvent(context, organiser, "Soweto Marathon");
            var cat = TestHelper.CreateTestCategory(context, ev, "21km");
            var enrolment = TestHelper.CreateTestEnrolment(context, ev, cat, participant);

            var res = new Result
            {
                EnrolmentID = enrolment.EnrolmentID,
                FinishTime = TimeSpan.Parse("01:25:00"),
                Position = 12
            };
            context.Results.Add(res);
            context.SaveChanges();

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(participant);
            var controller = new ResultsController(context, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            // Act
            var result = await controller.GetMyResults();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var list = okResult.Value.Should().BeAssignableTo<IEnumerable<ResultResponseDto>>().Subject.ToList();
            list.Should().HaveCount(1);
            list[0].ParticipantID.Should().Be(participant.UserID);
            list[0].FinishTime.Should().Be("01:25:00");
            list[0].Position.Should().Be(12);
            list[0].EventName.Should().Be("Soweto Marathon");
        }
    }
}
