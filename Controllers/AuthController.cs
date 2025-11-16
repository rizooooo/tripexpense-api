using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TripExpenseApi.Config;
using TripExpenseApi.Models;
using TripExpenseApi.Models.Dtos;
using TripExpenseApi.Services;

namespace TripExpenseApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;

        public AuthController(ApplicationDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
        {
            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { message = "Email already registered" });
            }

            // Create user
            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Avatar = dto.Name.Substring(0, 1).ToUpper(),
                PasswordHash = _authService.HashPassword(dto.Password),
                CreatedAt = DateTimeOffset.UtcNow,
                IsEmailVerified = false,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Generate JWT token and refresh token
            var token = _authService.GenerateJwtToken(user.Id, user.Email, user.Name);
            var refreshToken = _authService.GenerateRefreshToken();

            // Store refresh token
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            var response = new AuthResponseDto
            {
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                Avatar = user.Avatar,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };

            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            // Find user by email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            // Verify password
            if (!_authService.VerifyPassword(dto.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            // Update last login
            user.LastLoginAt = DateTimeOffset.UtcNow;

            // Generate JWT token and refresh token
            var token = _authService.GenerateJwtToken(user.Id, user.Email, user.Name);
            var refreshToken = _authService.GenerateRefreshToken();

            // Store refresh token
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            var response = new AuthResponseDto
            {
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                Avatar = user.Avatar,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };

            return Ok(response);
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(
            [FromQuery] int userId,
            ChangePasswordDto dto
        )
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Verify current password
            if (!_authService.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Current password is incorrect" });
            }

            // Update password
            user.PasswordHash = _authService.HashPassword(dto.NewPassword);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password changed successfully" });
        }

        [HttpPost("verify-token")]
        public async Task<ActionResult<UserDto>> VerifyToken(
            [FromHeader(Name = "Authorization")] string authHeader
        )
        {
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized(new { message = "No token provided" });
            }

            var token = authHeader.Substring("Bearer ".Length);

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub
                );
                if (userIdClaim == null)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var userId = int.Parse(userIdClaim.Value);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return Unauthorized(new { message = "User not found" });
                }

                return Ok(
                    new UserDto
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        Avatar = user.Avatar,
                        CreatedAt = user.CreatedAt,
                    }
                );
            }
            catch
            {
                return Unauthorized(new { message = "Invalid token" });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> RefreshToken(RefreshTokenDto dto)
        {
            // Find user by refresh token
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == dto.RefreshToken
            );

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid refresh token" });
            }

            // Check if refresh token is expired
            if (user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTimeOffset.UtcNow)
            {
                return Unauthorized(new { message = "Refresh token expired" });
            }

            // Generate new JWT token and refresh token
            var token = _authService.GenerateJwtToken(user.Id, user.Email, user.Name);
            var newRefreshToken = _authService.GenerateRefreshToken();

            // Update refresh token in database
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(7);
            user.LastLoginAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            var response = new AuthResponseDto
            {
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                Avatar = user.Avatar,
                Token = token,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };

            return Ok(response);
        }
    }
}
