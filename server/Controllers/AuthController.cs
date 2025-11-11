using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController, Route("api/auth")]
public sealed class AuthController : ControllerBase
{
  private readonly AppDb _db;
  public AuthController(AppDb db) => _db = db;

  public record LoginReq(string UserName, string Password);
  [HttpPost("login")]
  public async Task<IActionResult> Login([FromBody] LoginReq req)
  {
    var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == req.UserName);
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
      return Unauthorized();

    var claims = new[] {
      new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new Claim(ClaimTypes.Name, user.UserName),
    };
    var id = new ClaimsIdentity(claims, "cookie");
    await HttpContext.SignInAsync("cookie", new ClaimsPrincipal(id));

    return Ok();
  }

  [HttpPost("logout")]
  public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync("cookie"); return Ok(); }

  [Authorize, HttpGet("me")]
  public async Task<IActionResult> Me()
  {
    var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var p = await _db.UserProfiles.FindAsync(uid);
    return Ok(new { userId = uid, userName = User.Identity!.Name, fullName = p?.FullName, company = p?.Company, email = p?.Email });
  }
}
