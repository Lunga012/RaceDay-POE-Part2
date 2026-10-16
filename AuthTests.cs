using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.DTOs;
using RaceDay.Api.Services;
using Xunit;

namespace RaceDay.Tests
{
    public class AuthTests
    {
        [Fact]
        public async Task Register_WithValidOrganiserDetails_Returns201CreatedAndSession()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, sessionManager, passwordHasher) = TestHelper.CreateTestContext();
            var controller = new AuthController(context, passwordHasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new RegisterDto
            {
                Name = "Lunga Organiser",
                Email = "lunga@test.com",
                Password = "SecurePassword123!",
                Role = "Organiser"
            };

            // Act
            var result = await controller.Register(dto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

            var response = objectResult.Value.Should().BeOfType<AuthResponseDto>().Subject;
            response.Name.Should().Be("Lunga Organiser");
            response.Email.Should().Be("lunga@test.com");
            response.RoleName.Should().Be("Organiser");
            response.SessionToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Register_WithValidParticipantDetails_Returns201CreatedAndSession()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, sessionManager, passwordHasher) = TestHelper.CreateTestContext();
            var controller = new AuthController(context, passwordHasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new RegisterDto
            {
                Name = "Sphe Participant",
                Email = "sphe@test.com",
                Password = "RunnerPassword123!",
                Role = "Participant"
            };

            // Act
            var result = await controller.Register(dto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

            var response = objectResult.Value.Should().BeOfType<AuthResponseDto>().Subject;
            response.Name.Should().Be("Sphe Participant");
            response.Email.Should().Be("sphe@test.com");
            response.RoleName.Should().Be("Participant");
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_Returns409Conflict()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, sessionManager, passwordHasher) = TestHelper.CreateTestContext();
            TestHelper.CreateTestUser(context, passwordHasher, "Existing User", "duplicate@test.com", "Participant");

            var controller = new AuthController(context, passwordHasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new RegisterDto
            {
                Name = "New User",
                Email = "duplicate@test.com",
                Password = "Password123!",
                Role = "Participant"
            };

            // Act
            var result = await controller.Register(dto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        }

        [Fact]
        public async Task Register_WithInvalidRole_Returns400BadRequest()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, sessionManager, passwordHasher) = TestHelper.CreateTestContext();
            var controller = new AuthController(context, passwordHasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new RegisterDto
            {
                Name = "Invalid Role User",
                Email = "invalidrole@test.com",
                Password = "Password123!",
                Role = "SuperAdmin" // Not allowed
            };

            // Act
            var result = await controller.Register(dto);

            // Assert
            var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        }

        [Fact]
        public async Task Login_WithValidCredentials_Returns200OkAndSessionToken()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, sessionManager, passwordHasher) = TestHelper.CreateTestContext();
            var user = TestHelper.CreateTestUser(context, passwordHasher, "Valid Runner", "runner@test.com", "Participant");

            var controller = new AuthController(context, passwordHasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new LoginDto
            {
                Email = "runner@test.com",
                Password = "Password123!"
            };

            // Act
            var result = await controller.Login(dto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

            var response = okResult.Value.Should().BeOfType<AuthResponseDto>().Subject;
            response.UserID.Should().Be(user.UserID);
            response.Email.Should().Be(user.Email);
            response.RoleName.Should().Be("Participant");
            response.SessionToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Login_WithIncorrectPassword_Returns401Unauthorized()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, sessionManager, passwordHasher) = TestHelper.CreateTestContext();
            TestHelper.CreateTestUser(context, passwordHasher, "Valid Runner", "runner2@test.com", "Participant");

            var controller = new AuthController(context, passwordHasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new LoginDto
            {
                Email = "runner2@test.com",
                Password = "WrongPassword999!"
            };

            // Act
            var result = await controller.Login(dto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public async Task Login_WithNonExistentEmail_Returns401Unauthorized()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var (httpContext, sessionManager, passwordHasher) = TestHelper.CreateTestContext();

            var controller = new AuthController(context, passwordHasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var dto = new LoginDto
            {
                Email = "ghost@nonexistent.com",
                Password = "Password123!"
            };

            // Act
            var result = await controller.Login(dto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Fact]
        public void Logout_WhenAuthenticated_ClearsSessionAndReturns200Ok()
        {
            // Arrange
            var context = TestHelper.CreateInMemoryDbContext();
            var hasher = new PasswordHasher();
            var user = TestHelper.CreateTestUser(context, hasher, "Logged In User", "logout@test.com", "Participant");
            var (httpContext, sessionManager, _) = TestHelper.CreateTestContext(user);

            var controller = new AuthController(context, hasher, sessionManager)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            // Verify session exists before logout
            sessionManager.GetCurrentSession(httpContext).Should().NotBeNull();

            // Act
            var result = controller.Logout();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

            // Verify session is cleared
            sessionManager.GetCurrentSession(httpContext).Should().BeNull();
        }

        [Fact]
        public void Password_IsHashedWithBCrypt_AndNeverStoredInPlainText()
        {
            // Arrange
            var hasher = new PasswordHasher();
            var plainPassword = "MySecretPassword123!";

            // Act
            var hash = hasher.HashPassword(plainPassword);

            // Assert
            hash.Should().NotBe(plainPassword);
            hash.Should().StartWith("$2"); // BCrypt salt prefix
            hasher.VerifyPassword(plainPassword, hash).Should().BeTrue();
            hasher.VerifyPassword("WrongPass", hash).Should().BeFalse();
        }
    }
}
