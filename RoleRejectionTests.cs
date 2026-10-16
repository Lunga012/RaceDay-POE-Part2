using System;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Attributes;
using RaceDay.Api.Services;
using Xunit;

namespace RaceDay.Tests
{
    public class RoleRejectionTests
    {
        [Fact]
        public void UnauthenticatedUser_AccessingProtectedEndpoint_Returns401Unauthorized()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, _, _) = TestHelper.CreateTestContext(); // No user logged in

            var filter = new SessionAuthorizeAttribute(); // Any authenticated user
            var authContext = TestHelper.CreateAuthorizationContext(httpContext, filter.AllowedRoles);

            // Act
            filter.OnAuthorization(authContext);

            // Assert
            authContext.Result.Should().NotBeNull();
            var objectResult = authContext.Result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public void Participant_AccessingOrganiserOnlyEndpoint_Returns403Forbidden()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var participant = TestHelper.CreateTestUser(context, hasher, "Runner User", "runner@roles.com", "Participant");
            var (httpContext, _, _) = TestHelper.CreateTestContext(participant);

            var filter = new SessionAuthorizeAttribute("Organiser"); // Restrict to Organiser
            var authContext = TestHelper.CreateAuthorizationContext(httpContext, filter.AllowedRoles);

            // Act
            filter.OnAuthorization(authContext);

            // Assert
            authContext.Result.Should().NotBeNull();
            var objectResult = authContext.Result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public void Organiser_AccessingParticipantOnlyEndpoint_Returns403Forbidden()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Organiser User", "org@roles.com", "Organiser");
            var (httpContext, _, _) = TestHelper.CreateTestContext(organiser);

            var filter = new SessionAuthorizeAttribute("Participant"); // Restrict to Participant
            var authContext = TestHelper.CreateAuthorizationContext(httpContext, filter.AllowedRoles);

            // Act
            filter.OnAuthorization(authContext);

            // Assert
            authContext.Result.Should().NotBeNull();
            var objectResult = authContext.Result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public void Organiser_AccessingOrganiserEndpoint_IsPermitted()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var organiser = TestHelper.CreateTestUser(context, hasher, "Authorized Org", "auth_org@roles.com", "Organiser");
            var (httpContext, _, _) = TestHelper.CreateTestContext(organiser);

            var filter = new SessionAuthorizeAttribute("Organiser");
            var authContext = TestHelper.CreateAuthorizationContext(httpContext, filter.AllowedRoles);

            // Act
            filter.OnAuthorization(authContext);

            // Assert: No rejection result set
            authContext.Result.Should().BeNull();
        }

        [Fact]
        public void Participant_AccessingParticipantEndpoint_IsPermitted()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var participant = TestHelper.CreateTestUser(context, hasher, "Authorized Runner", "auth_run@roles.com", "Participant");
            var (httpContext, _, _) = TestHelper.CreateTestContext(participant);

            var filter = new SessionAuthorizeAttribute("Participant");
            var authContext = TestHelper.CreateAuthorizationContext(httpContext, filter.AllowedRoles);

            // Act
            filter.OnAuthorization(authContext);

            // Assert: No rejection result set
            authContext.Result.Should().BeNull();
        }
    }
}
