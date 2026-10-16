using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Tests
{
    public class ProfileTests
    {
        [Fact]
        public async Task AuthenticatedUser_CanViewProfile_Returns200Ok()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var user = TestHelper.CreateTestUser(context, hasher, "Profile User", "user@profile.com", "Participant");
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(user);

            var controller = new ProfileController(context, hasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            // Act
            var result = await controller.GetProfile();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

            var profile = okResult.Value.Should().BeOfType<UserProfileDto>().Subject;
            profile.UserID.Should().Be(user.UserID);
            profile.Name.Should().Be("Profile User");
            profile.Email.Should().Be("user@profile.com");
            profile.RoleName.Should().Be("Participant");
        }

        [Fact]
        public async Task AuthenticatedUser_CanUpdateProfile_Returns200Ok()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var user = TestHelper.CreateTestUser(context, hasher, "Old Name", "old@profile.com", "Organiser");
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(user);

            var controller = new ProfileController(context, hasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var updateDto = new UpdateProfileDto
            {
                Name = "New Name",
                Email = "new@profile.com",
                NewPassword = "BrandNewPassword123!"
            };

            // Act
            var result = await controller.UpdateProfile(updateDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var updated = okResult.Value.Should().BeOfType<UserProfileDto>().Subject;
            updated.Name.Should().Be("New Name");
            updated.Email.Should().Be("new@profile.com");

            hasher.VerifyPassword("BrandNewPassword123!", context.Users.Find(user.UserID)!.PasswordHash).Should().BeTrue();
        }

        [Fact]
        public async Task AuthenticatedUser_UpdatingToExistingEmail_Returns409Conflict()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (_, _, hasher) = TestHelper.CreateTestContext();
            var user1 = TestHelper.CreateTestUser(context, hasher, "User One", "user1@profile.com", "Participant");
            var user2 = TestHelper.CreateTestUser(context, hasher, "User Two", "user2@profile.com", "Participant");

            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(user1);
            var controller = new ProfileController(context, hasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var duplicateDto = new UpdateProfileDto
            {
                Name = "User One",
                Email = "user2@profile.com" // Already belongs to user2
            };

            // Act
            var result = await controller.UpdateProfile(duplicateDto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        }
    }
}
