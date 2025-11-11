using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public static class TestHelpers
{
    public static AppDb CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDb>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDb(options);
    }

    public static ControllerContext CreateControllerContext(Guid userId, string userName = "testuser")
    {
        var httpCtx = new DefaultHttpContext();
        // Minimal DI to satisfy HttpContext.SignInAsync/SignOutAsync used by controllers
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService, TestAuthenticationService>();
        httpCtx.RequestServices = services.BuildServiceProvider();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, userName)
        };
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "cookie"));
        return new ControllerContext { HttpContext = httpCtx };
    }

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }
}
