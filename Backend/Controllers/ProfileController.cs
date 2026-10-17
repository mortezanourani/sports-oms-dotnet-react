using System.Security.Claims;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TokenService _tokenService;

        public ProfileController(UserManager<ApplicationUser> userManager, TokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { detail = "کاربر احراز هویت‌شده یافت نشد" });

            return Ok(new ProfileOutDto(user.UserName ?? "", user.FirstName ?? "", user.LastName ?? ""));
        }

        [HttpPut]
        public async Task<IActionResult> Update(ProfileUpdateDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName))
                return BadRequest(new { detail = "نام و نام خانوادگی الزامی است" });

            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { detail = "کاربر احراز هویت‌شده یافت نشد" });

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { detail = string.Join(" / ", result.Errors.Select(error => error.Description)) });

            return Ok(new
            {
                firstName = user.FirstName,
                lastName = user.LastName,
                accessToken = _tokenService.GenerateAccessToken(user)
            });
        }

        [HttpPost("password")]
        public async Task<IActionResult> ChangePassword(PasswordChangeDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.CurrentPassword) ||
                string.IsNullOrEmpty(request.NewPassword))
                return BadRequest(new { detail = "رمز عبور فعلی و رمز عبور جدید الزامی هستند" });

            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { detail = "کاربر احراز هویت‌شده یافت نشد" });

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(error => error.Code == "PasswordMismatch"))
                    return BadRequest(new { detail = "رمز عبور فعلی اشتباه است" });

                return BadRequest(new { detail = string.Join(" / ", result.Errors.Select(error => error.Description)) });
            }

            return Ok(new { detail = "رمز عبور با موفقیت تغییر کرد" });
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out var id))
                return null;

            return await _userManager.FindByIdAsync(id.ToString());
        }
    }
}
