using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class SettlementsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;

        public SettlementsController(ApplicationDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SettlementDto>>> GetSettlements(
            [FromQuery] int? tripId
        )
        {
            var query = _context
                .Settlements.Include(s => s.FromUser)
                .Include(s => s.ToUser)
                .AsQueryable();

            if (tripId.HasValue)
            {
                query = query.Where(s => s.TripId == tripId.Value);
            }

            var settlements = await query
                .Select(s => new SettlementDto
                {
                    Id = s.Id,
                    TripId = s.TripId,
                    FromUserId = s.FromUserId,
                    FromUserName = s.FromUser.Name,
                    ToUserId = s.ToUserId,
                    ToUserName = s.ToUser.Name,
                    Amount = s.Amount,
                    SettlementDate = s.SettlementDate,
                    Notes = s.Notes,
                })
                .OrderByDescending(s => s.SettlementDate)
                .ToListAsync();

            return Ok(settlements);
        }

        [HttpPost]
        public async Task<ActionResult<SettlementDto>> CreateSettlement(SettlementCreateDto dto)
        {
            var trip = await _context.Trips.FindAsync(dto.TripId);
            if (trip == null)
                return NotFound("Trip not found");

            var fromUser = await _context.Users.FindAsync(dto.FromUserId);
            var toUser = await _context.Users.FindAsync(dto.ToUserId);

            if (fromUser == null || toUser == null)
                return NotFound("User not found");

            if (dto.FromUserId == dto.ToUserId)
                return BadRequest("Cannot settle with yourself");

            var settlement = new Settlement
            {
                TripId = dto.TripId,
                FromUserId = dto.FromUserId,
                ToUserId = dto.ToUserId,
                Amount = dto.Amount,
                Notes = dto.Notes,
            };

            _context.Settlements.Add(settlement);
            await _context.SaveChangesAsync();

            var settlementDto = new SettlementDto
            {
                Id = settlement.Id,
                TripId = settlement.TripId,
                FromUserId = settlement.FromUserId,
                FromUserName = fromUser.Name,
                ToUserId = settlement.ToUserId,
                ToUserName = toUser.Name,
                Amount = settlement.Amount,
                SettlementDate = settlement.SettlementDate,
                Notes = settlement.Notes,
            };

            return CreatedAtAction(
                nameof(GetSettlements),
                new { tripId = dto.TripId },
                settlementDto
            );
        }

        [HttpGet("suggestions/{tripId}")]
        public async Task<
            ActionResult<IEnumerable<SettlementSuggestionDto>>
        > GetSettlementSuggestions(int tripId)
        {
            var trip = await _context
                .Trips.Include(t => t.Members)
                .ThenInclude(m => m.User)
                .Include(t => t.Expenses)
                .ThenInclude(e => e.Splits)
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip == null)
                return NotFound();

            // Calculate balances
            var balances = new Dictionary<int, decimal>();
            foreach (var member in trip.Members.Where(m => m.IsActive))
            {
                balances[member.UserId] = 0;
            }

            foreach (var expense in trip.Expenses)
            {
                balances[expense.PaidByUserId] += expense.Amount;
                foreach (var split in expense.Splits)
                {
                    balances[split.UserId] -= split.Amount;
                }
            }

            // Apply existing settlements
            var settlements = await _context
                .Settlements.Where(s => s.TripId == tripId)
                .ToListAsync();

            foreach (var settlement in settlements)
            {
                balances[settlement.FromUserId] += settlement.Amount;
                balances[settlement.ToUserId] -= settlement.Amount;
            }

            // Generate suggestions using simplified debt algorithm
            var suggestions = new List<SettlementSuggestionDto>();
            var debtors = balances
                .Where(b => b.Value < -0.01m)
                .OrderBy(b => b.Value)
                .Select(b => new { UserId = b.Key, Amount = -b.Value })
                .ToList();

            var creditors = balances
                .Where(b => b.Value > 0.01m)
                .OrderByDescending(b => b.Value)
                .Select(b => new { UserId = b.Key, Amount = b.Value })
                .ToList();

            int i = 0,
                j = 0;
            while (i < debtors.Count && j < creditors.Count)
            {
                var debtor = debtors[i];
                var creditor = creditors[j];
                var amount = Math.Min(debtor.Amount, creditor.Amount);

                suggestions.Add(
                    new SettlementSuggestionDto
                    {
                        FromUserId = debtor.UserId,
                        FromUserName = trip.Members.First(m => m.UserId == debtor.UserId).User.Name,
                        ToUserId = creditor.UserId,
                        ToUserName = trip.Members.First(m => m.UserId == creditor.UserId).User.Name,
                        Amount = Math.Round(amount, 2),
                    }
                );

                debtors[i] = new { UserId = debtor.UserId, Amount = debtor.Amount - amount };
                creditors[j] = new { UserId = creditor.UserId, Amount = creditor.Amount - amount };

                if (debtors[i].Amount < 0.01m)
                    i++;
                if (creditors[j].Amount < 0.01m)
                    j++;
            }

            return Ok(suggestions);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSettlement(int id)
        {
            var settlement = await _context.Settlements.FindAsync(id);

            if (settlement == null)
                return NotFound();

            _context.Settlements.Remove(settlement);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
