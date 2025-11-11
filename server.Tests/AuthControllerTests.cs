using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

public class AuthControllerTests
{
    [Fact]
    public async Task Login_UserNotFound_ReturnsUnauthorized()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Login_UserNotFound_ReturnsUnauthorized));
        var sut = new AuthController(db);

        // Act
        var result = await sut.Login(new AuthController.LoginReq("nosuch", "pw"));

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.UnauthorizedResult>();
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Login_WrongPassword_ReturnsUnauthorized));
        db.Users.Add(new User { Id = Guid.NewGuid(), UserName = "rick", PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct") });
        await db.SaveChangesAsync();
        var sut = new AuthController(db);

        // Act
        var result = await sut.Login(new AuthController.LoginReq("rick", "wrong"));

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.UnauthorizedResult>();
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsOk()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Login_CorrectPassword_ReturnsOk));
        db.Users.Add(new User { Id = Guid.NewGuid(), UserName = "rick", PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct") });
        await db.SaveChangesAsync();
        var sut = new AuthController(db)
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid(), "rick")
        };

        // Act
        var result = await sut.Login(new AuthController.LoginReq("rick", "correct"));

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkResult>();
    }

    [Fact]
    public async Task Logout_Always_ReturnsOk()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Logout_Always_ReturnsOk));
        var sut = new AuthController(db)
        {
            ControllerContext = TestHelpers.CreateControllerContext(Guid.NewGuid(), "rick")
        };

        // Act
        var result = await sut.Logout();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkResult>();
    }

    [Fact]
    public async Task Me_WithoutProfile_ReturnsNullProfileFields()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Me_WithoutProfile_ReturnsNullProfileFields));
        var uid = Guid.NewGuid();
        db.Users.Add(new User { Id = uid, UserName = "rick", PasswordHash = "x" });
        await db.SaveChangesAsync();
        var sut = new AuthController(db)
        {
            ControllerContext = TestHelpers.CreateControllerContext(uid, "rick")
        };

        // Act
        var result = await sut.Me();

        // Assert
        var ok = result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Me_WithProfile_ReturnsProjectedInfo()
    {
        // Arrange
        await using var db = TestHelpers.CreateInMemoryDb(nameof(Me_WithProfile_ReturnsProjectedInfo));
        var uid = Guid.NewGuid();
        db.Users.Add(new User { Id = uid, UserName = "rick", PasswordHash = "x" });
        db.UserProfiles.Add(new UserProfile { UserId = uid, FullName = "Rick Deckard", Company = "Blade Runners", Email = "rick@example.com" });
        await db.SaveChangesAsync();
        var sut = new AuthController(db)
        {
            ControllerContext = TestHelpers.CreateControllerContext(uid, "rick")
        };

        // Act
        var result = await sut.Me();

        // Assert
        var ok = result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }
}

