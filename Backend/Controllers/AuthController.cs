using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        private readonly int _refreshTokenExpirationDays;

        public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
            TokenService tokenService, ApplicationDbContext db, IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _tokenService = tokenService;
            _db = db;

            _refreshTokenExpirationDays = configuration.GetValue<int?>("Jwt:RefreshTokenExpirationDays")
                ?? throw new InvalidOperationException("Jwt:RefreshTokenExpirationDays must be configured.");
            if (_refreshTokenExpirationDays <= 0)
                throw new InvalidOperationException("Jwt:RefreshTokenExpirationDays must be a positive number.");
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
                ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays)
            });
            await _db.SaveChangesAsync();

            return Ok(new { access = accessToken, refresh = refreshToken });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return Unauthorized(new { detail = "توکن نامعتبر است" });

            var now = DateTime.UtcNow;
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var stored = await _db.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);
            if (stored == null || stored.Revoked || stored.ExpiresAt <= now)
                return Unauthorized(new { detail = "توکن نامعتبر است" });

            // Conditional update makes refresh tokens single-use, including when
            // two requests attempt to rotate the same token concurrently.
            var revokedCount = await _db.RefreshTokens
                .Where(t => t.Id == stored.Id && !t.Revoked && t.ExpiresAt > now)
                .ExecuteUpdateAsync(update => update.SetProperty(t => t.Revoked, true));
            if (revokedCount != 1)
                return Unauthorized(new { detail = "توکن نامعتبر است" });

            var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
            if (user == null) return Unauthorized(new { detail = "توکن نامعتبر است" });

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            _db.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = now.AddDays(_refreshTokenExpirationDays)
            });
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { access = accessToken, refresh = refreshToken });
        }
    }
}
