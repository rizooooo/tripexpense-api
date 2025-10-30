using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TripExpenseApi.Config;
using TripExpenseApi.Models;
using TripExpenseApi.Models.Dtos;
using TripExpenseApi.Services;

namespace TripExpenseApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TripsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;

        public TripsController(ApplicationDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TripSummaryDto>>> GetTrips()
        {
            var query = _context
                .Trips.Include(t => t.Members)
                .Include(t => t.Expenses)
                .AsQueryable();

            query = query.Where(t =>
                t.Members.Any(m => m.UserId == _authService.GetUserId() && m.IsActive)
            );

            var trips = await query
                .Select(t => new TripSummaryDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    MemberCount = t.Members.Count(m => m.IsActive),
                    TotalExpenses = t.Expenses.Sum(e => e.Amount),
                })
                .ToListAsync();

            return Ok(trips);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TripDto>> GetTrip(int id)
        {
            var trip = await _context
                .Trips.Include(t => t.Members)
                .ThenInclude(m => m.User)
                .Include(t => t.Expenses)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
                return NotFound();

            var tripDto = new TripDto
            {
                Id = trip.Id,
                Name = trip.Name,
                Description = trip.Description,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                MemberCount = trip.Members.Count(m => m.IsActive),
                TotalExpenses = trip.Expenses.Sum(e => e.Amount),
                CreatedAt = trip.CreatedAt,
                Members = trip
                    .Members.Where(m => m.IsActive)
                    .Select(m => new TripMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        Name = m.User.Name,
                        Avatar = m.User.Avatar,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt,
                    })
                    .ToList(),
            };

            return Ok(tripDto);
        }

        [HttpPost]
        public async Task<ActionResult<TripDto>> CreateTrip(TripCreateDto dto)
        {
            var userId = _authService.GetUserId();
            var creator = await _context.Users.FindAsync(userId);
            if (creator == null)
                return BadRequest("Creator user not found");

            var trip = new Trip
            {
                Name = dto.Name,
                Description = dto.Description,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                CreatedByUserId = userId,
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            // Add creator as admin
            var creatorMember = new TripMember
            {
                TripId = trip.Id,
                UserId = userId,
                Role = "Admin",
            };
            _context.TripMembers.Add(creatorMember);

            // Add other members
            foreach (var memberId in dto.MemberIds.Where(id => id != userId))
            {
                if (await _context.Users.AnyAsync(u => u.Id == memberId))
                {
                    _context.TripMembers.Add(
                        new TripMember
                        {
                            TripId = trip.Id,
                            UserId = memberId,
                            Role = "Member",
                        }
                    );
                }
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetTrip),
                new { id = trip.Id },
                await GetTripDto(trip.Id)
            );
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTrip(int id, TripCreateDto dto)
        {
            var trip = await _context.Trips.FindAsync(id);

            if (trip == null)
                return NotFound();

            trip.Name = dto.Name;
            trip.Description = dto.Description;
            trip.StartDate = dto.StartDate;
            trip.EndDate = dto.EndDate;
            trip.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> AddMember(int id, [FromBody] int userId)
        {
            var trip = await _context.Trips.FindAsync(id);
            if (trip == null)
                return NotFound("Trip not found");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            if (await _context.TripMembers.AnyAsync(tm => tm.TripId == id && tm.UserId == userId))
                return BadRequest("User is already a member");

            var member = new TripMember
            {
                TripId = id,
                UserId = userId,
                Role = "Member",
            };

            _context.TripMembers.Add(member);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(int id, int userId)
        {
            var member = await _context.TripMembers.FirstOrDefaultAsync(tm =>
                tm.TripId == id && tm.UserId == userId
            );

            if (member == null)
                return NotFound();

            member.IsActive = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id}/balances")]
        public async Task<ActionResult<IEnumerable<UserBalanceDto>>> GetBalances(int id)
        {
            var trip = await _context
                .Trips.Include(t => t.Members)
                .ThenInclude(m => m.User)
                .Include(t => t.Expenses)
                .ThenInclude(e => e.Splits)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
                return NotFound();

            var balances = new Dictionary<int, decimal>();

            // Initialize balances for all members
            foreach (var member in trip.Members.Where(m => m.IsActive))
            {
                balances[member.UserId] = 0;
            }

            // Calculate balances from expenses
            foreach (var expense in trip.Expenses)
            {
                balances[expense.PaidByUserId] += expense.Amount;
                foreach (var split in expense.Splits)
                {
                    balances[split.UserId] -= split.Amount;
                }
            }

            // ⭐ NEW: Apply settlements
            var settlements = await _context.Settlements.Where(s => s.TripId == id).ToListAsync();

            foreach (var settlement in settlements)
            {
                balances[settlement.FromUserId] += settlement.Amount; // Paid, reduces debt
                balances[settlement.ToUserId] -= settlement.Amount; // Received, reduces what's owed to them
            }

            var result = balances
                .Select(kvp => new UserBalanceDto
                {
                    UserId = kvp.Key,
                    Name = trip.Members.First(m => m.UserId == kvp.Key).User.Name,
                    Avatar = trip.Members.First(m => m.UserId == kvp.Key).User.Avatar,
                    Balance = kvp.Value,
                })
                .ToList();

            return Ok(result);
        }

        // ============================================
        // TripsController - New endpoints
        // ============================================

        [HttpGet("{id}/details")]
        public async Task<ActionResult<TripDetailDto>> GetTripDetails(int id)
        {
            var userId = _authService.GetUserId();

            var trip = await _context
                .Trips.Include(t => t.Members)
                .ThenInclude(m => m.User)
                .Include(t => t.Expenses)
                .ThenInclude(e => e.Splits)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
                return NotFound();

            var totalSpent = trip.Expenses.Sum(e => e.Amount);
            var yourShare = trip
                .Expenses.SelectMany(e => e.Splits)
                .Where(s => s.UserId == userId)
                .Sum(s => s.Amount);

            var amountPaid = trip.Expenses.Where(e => e.PaidByUserId == userId).Sum(e => e.Amount);

            var yourBalance = amountPaid - yourShare;

            // ⭐ NEW: Apply settlements to user's balance
            var settlements = await _context
                .Settlements.Where(s =>
                    s.TripId == id && (s.FromUserId == userId || s.ToUserId == userId)
                )
                .ToListAsync();

            foreach (var settlement in settlements)
            {
                if (settlement.FromUserId == userId)
                {
                    yourBalance += settlement.Amount; // You paid someone
                }
                else if (settlement.ToUserId == userId)
                {
                    yourBalance -= settlement.Amount; // Someone paid you
                }
            }

            var tripDetail = new TripDetailDto
            {
                Id = trip.Id,
                Name = trip.Name,
                Description = trip.Description,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                MemberCount = trip.Members.Count(m => m.IsActive),
                TotalSpent = totalSpent,
                YourShare = yourShare,
                YourBalance = yourBalance, // Now includes settlements!
                CreatedAt = trip.CreatedAt,
                Members = trip
                    .Members.Where(m => m.IsActive)
                    .Select(m => new TripMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        Name = m.User.Name,
                        Avatar = m.User.Avatar,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt,
                    })
                    .ToList(),
            };

            return Ok(tripDetail);
        }

        [HttpGet("user/{userId}/dashboard")]
        public async Task<ActionResult<UserDashboardDto>> GetUserDashboard()
        {
            var userId = _authService.GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var userTrips = await _context
                .Trips.Include(t => t.Members)
                .Include(t => t.Expenses)
                .ThenInclude(e => e.Splits)
                .Where(t => t.Members.Any(m => m.UserId == userId && m.IsActive))
                .ToListAsync();

            decimal overallBalance = 0;
            decimal totalSpent = 0;
            decimal totalOwed = 0;

            var recentTrips = new List<TripSummaryWithBalanceDto>();

            foreach (var trip in userTrips)
            {
                var amountPaid = trip
                    .Expenses.Where(e => e.PaidByUserId == userId)
                    .Sum(e => e.Amount);

                var amountOwed = trip
                    .Expenses.SelectMany(e => e.Splits)
                    .Where(s => s.UserId == userId)
                    .Sum(s => s.Amount);

                var tripBalance = amountPaid - amountOwed;

                // ⭐ NEW: Apply settlements for this trip
                var settlements = await _context
                    .Settlements.Where(s =>
                        s.TripId == trip.Id && (s.FromUserId == userId || s.ToUserId == userId)
                    )
                    .ToListAsync();

                foreach (var settlement in settlements)
                {
                    if (settlement.FromUserId == userId)
                    {
                        tripBalance += settlement.Amount;
                    }
                    else if (settlement.ToUserId == userId)
                    {
                        tripBalance -= settlement.Amount;
                    }
                }

                overallBalance += tripBalance;
                totalSpent += amountPaid;

                if (tripBalance < 0)
                {
                    totalOwed += Math.Abs(tripBalance);
                }

                recentTrips.Add(
                    new TripSummaryWithBalanceDto
                    {
                        Id = trip.Id,
                        Name = trip.Name,
                        StartDate = trip.StartDate,
                        EndDate = trip.EndDate,
                        MemberCount = trip.Members.Count(m => m.IsActive),
                        TotalExpenses = trip.Expenses.Sum(e => e.Amount),
                        YourBalance = tripBalance, // Now includes settlements!
                    }
                );
            }

            var dashboard = new UserDashboardDto
            {
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                Avatar = user.Avatar,
                OverallBalance = overallBalance, // Now includes settlements!
                TotalTrips = userTrips.Count,
                TotalSpent = totalSpent,
                TotalOwed = totalOwed,
                RecentTrips = recentTrips.OrderByDescending(t => t.StartDate).Take(10).ToList(),
            };

            return Ok(dashboard);
        }

        [HttpPost("{id}/invite")]
        public async Task<ActionResult<TripInviteDto>> GenerateInviteLink(
            int id,
            [FromQuery] int? expiryDays = null
        )
        {
            var trip = await _context.Trips.FindAsync(id);
            if (trip == null)
                return NotFound("Trip not found");

            trip.InviteToken = Guid.NewGuid().ToString("N");

            if (expiryDays.HasValue && expiryDays.Value > 0)
            {
                trip.InviteTokenExpiry = DateTime.UtcNow.AddDays(expiryDays.Value);
            }
            else
            {
                trip.InviteTokenExpiry = DateTime.UtcNow.AddDays(7);
            }

            trip.IsInviteLinkActive = true;
            trip.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var inviteDto = new TripInviteDto
            {
                TripId = trip.Id,
                TripName = trip.Name,
                InviteToken = trip.InviteToken,
                InviteLink = $"https://yourapp.com/invite/{trip.InviteToken}",
                ExpiryDate = trip.InviteTokenExpiry,
                IsActive = trip.IsInviteLinkActive,
            };

            return Ok(inviteDto);
        }

        [HttpGet("invite/{token}")]
        public async Task<ActionResult<TripInviteInfoDto>> GetInviteInfo(string token)
        {
            var trip = await _context
                .Trips.Include(t => t.CreatedBy)
                .Include(t => t.Members)
                .FirstOrDefaultAsync(t => t.InviteToken == token);

            if (trip == null)
            {
                return Ok(
                    new TripInviteInfoDto { IsValid = false, Message = "Invalid invite link" }
                );
            }

            if (!trip.IsInviteLinkActive)
            {
                return Ok(
                    new TripInviteInfoDto
                    {
                        TripId = trip.Id,
                        TripName = trip.Name,
                        IsValid = false,
                        Message = "This invite link has been deactivated",
                    }
                );
            }

            if (trip.InviteTokenExpiry.HasValue && trip.InviteTokenExpiry.Value < DateTime.UtcNow)
            {
                return Ok(
                    new TripInviteInfoDto
                    {
                        TripId = trip.Id,
                        TripName = trip.Name,
                        IsValid = false,
                        Message = "This invite link has expired",
                    }
                );
            }

            return Ok(
                new TripInviteInfoDto
                {
                    TripId = trip.Id,
                    TripName = trip.Name,
                    Description = trip.Description,
                    StartDate = trip.StartDate,
                    EndDate = trip.EndDate,
                    MemberCount = trip.Members.Count(m => m.IsActive),
                    CreatedByName = trip.CreatedBy.Name,
                    IsValid = true,
                    Message = "Valid invite link",
                }
            );
        }

        [HttpPost("join")]
        public async Task<ActionResult<JoinTripResponse>> JoinTripViaInvite(
            [FromBody] JoinTripDto dto
        )
        {
            var userId = _authService.GetUserId();
            var trip = await _context
                .Trips.Include(t => t.Members)
                .FirstOrDefaultAsync(t => t.InviteToken == dto.InviteToken);

            if (trip == null)
                return NotFound("Invalid invite link");

            if (!trip.IsInviteLinkActive)
                return BadRequest("This invite link has been deactivated");

            if (trip.InviteTokenExpiry.HasValue && trip.InviteTokenExpiry.Value < DateTime.UtcNow)
                return BadRequest("This invite link has expired");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var existingMember = await _context.TripMembers.FirstOrDefaultAsync(tm =>
                tm.TripId == trip.Id && tm.UserId == userId
            );

            if (existingMember != null)
            {
                if (existingMember.IsActive)
                {
                    return BadRequest("You are already a member of this trip");
                }
                else
                {
                    existingMember.IsActive = true;
                    existingMember.JoinedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Successfully rejoined the trip" });
                }
            }

            var newMember = new TripMember
            {
                TripId = trip.Id,
                UserId = userId,
                Role = "Member",
                JoinedAt = DateTime.UtcNow,
                IsActive = true,
            };

            _context.TripMembers.Add(newMember);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully joined the trip", tripId = trip.Id });
        }

        [HttpDelete("{id}/invite")]
        public async Task<IActionResult> DeactivateInviteLink(int id)
        {
            var trip = await _context.Trips.FindAsync(id);
            if (trip == null)
                return NotFound("Trip not found");

            trip.IsInviteLinkActive = false;
            trip.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Invite link deactivated" });
        }

        [HttpGet("{id}/invite/status")]
        public async Task<ActionResult<TripInviteDto>> GetInviteStatus(int id)
        {
            var trip = await _context.Trips.FindAsync(id);
            if (trip == null)
                return NotFound("Trip not found");

            if (string.IsNullOrEmpty(trip.InviteToken))
                return NotFound("No invite link has been generated for this trip");

            var inviteDto = new TripInviteDto
            {
                TripId = trip.Id,
                TripName = trip.Name,
                InviteToken = trip.InviteToken,
                InviteLink =
                    $"https://jolly-glacier-054b5d21e-preview.westus2.3.azurestaticapps.net/join/{trip.InviteToken}",
                ExpiryDate = trip.InviteTokenExpiry,
                IsActive = trip.IsInviteLinkActive,
            };

            return Ok(inviteDto);
        }

        private async Task<TripDto> GetTripDto(int id)
        {
            var trip = await _context
                .Trips.Include(t => t.Members)
                .ThenInclude(m => m.User)
                .Include(t => t.Expenses)
                .FirstOrDefaultAsync(t => t.Id == id);

            return new TripDto
            {
                Id = trip.Id,
                Name = trip.Name,
                Description = trip.Description,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                MemberCount = trip.Members.Count(m => m.IsActive),
                TotalExpenses = trip.Expenses.Sum(e => e.Amount),
                CreatedAt = trip.CreatedAt,
                Members = trip
                    .Members.Where(m => m.IsActive)
                    .Select(m => new TripMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        Name = m.User.Name,
                        Avatar = m.User.Avatar,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt,
                    })
                    .ToList(),
            };
        }
    }
}
