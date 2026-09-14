using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Backend.Data;
using Backend.Models;
using Backend.Services;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TokenService _tokenService;
        private readonly ApplicationDbContext _db;

        public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
            TokenService tokenService, ApplicationDbContext db)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _tokenService = tokenService;
            _db = db;
        }

        public record LoginRequest(string Username, string Password);
        public record RefreshRequest(string RefreshToken);

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _userManager.FindByNameAsync(request.Username);
            if (user == null)
                return Unauthorized(new { detail = "نام کاربری یا رمز عبور اشتباه است" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
                return Unauthorized(new { detail = "نام کاربری یا رمز عبور اشتباه است" });

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            _db.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            await _db.SaveChangesAsync();

            return Ok(new { access = accessToken, refresh = refreshToken });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshRequest request)
        {
            var stored = _db.RefreshTokens.FirstOrDefault(t => t.Token == request.RefreshToken);
            if (stored == null || stored.Revoked || stored.ExpiresAt < DateTime.UtcNow)
                return Unauthorized(new { detail = "توکن نامعتبر است" });

            var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
            if (user == null) return Unauthorized();

            var accessToken = _tokenService.GenerateAccessToken(user);
            return Ok(new { access = accessToken });
        }
    }
}
