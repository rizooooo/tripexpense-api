using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    public class ExpensesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;

        public ExpensesController(ApplicationDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetExpenses(
            [FromQuery] int? tripId
        )
        {
            var query = _context
                .Expenses.Include(e => e.PaidBy)
                .Include(e => e.Splits)
                .ThenInclude(s => s.User)
                .AsQueryable();

            if (tripId.HasValue)
            {
                query = query.Where(e => e.TripId == tripId.Value);
            }

            var expenses = await query
                .Select(e => new ExpenseDto
                {
                    Id = e.Id,
                    TripId = e.TripId,
                    Description = e.Description,
                    Amount = e.Amount,
                    PaidByUserId = e.PaidByUserId,
                    PaidByName = e.PaidBy.Name,
                    ExpenseDate = e.ExpenseDate,
                    Category = e.Category,
                    SplitType = e.SplitType,
                    SplitCount = e.Splits.Count,
                    CreatedAt = e.CreatedAt,
                    Splits = e
                        .Splits.Select(s => new ExpenseSplitDto
                        {
                            Id = s.Id,
                            UserId = s.UserId,
                            UserName = s.User.Name,
                            Amount = s.Amount,
                            Percentage = s.Percentage,
                            IsPaid = s.IsPaid,
                        })
                        .ToList(),
                })
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            return Ok(expenses);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ExpenseDto>> GetExpense(int id)
        {
            var expense = await _context
                .Expenses.Include(e => e.PaidBy)
                .Include(e => e.Splits)
                .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null)
                return NotFound();

            var expenseDto = new ExpenseDto
            {
                Id = expense.Id,
                TripId = expense.TripId,
                Description = expense.Description,
                Amount = expense.Amount,
                PaidByUserId = expense.PaidByUserId,
                PaidByName = expense.PaidBy.Name,
                ExpenseDate = expense.ExpenseDate,
                Category = expense.Category,
                SplitType = expense.SplitType,
                SplitCount = expense.Splits.Count,
                CreatedAt = expense.CreatedAt,
                Splits = expense
                    .Splits.Select(s => new ExpenseSplitDto
                    {
                        Id = s.Id,
                        UserId = s.UserId,
                        UserName = s.User.Name,
                        Amount = s.Amount,
                        Percentage = s.Percentage,
                        IsPaid = s.IsPaid,
                    })
                    .ToList(),
            };

            return Ok(expenseDto);
        }

        [HttpPost]
        public async Task<ActionResult<ExpenseDto>> CreateExpense(ExpenseCreateDto dto)
        {
            var userId = _authService.GetUserId();
            var trip = await _context
                .Trips.Include(t => t.Members)
                .FirstOrDefaultAsync(t => t.Id == dto.TripId);

            if (trip == null)
                return NotFound("Trip not found");

            if (!trip.Members.Any(m => m.UserId == userId && m.IsActive))
                return BadRequest("Payer is not a member of this trip");

            var expense = new Expense
            {
                TripId = dto.TripId,
                Description = dto.Description,
                Amount = dto.Amount,
                PaidByUserId = userId,
                ExpenseDate = dto.ExpenseDate,
                Category = dto.Category,
                SplitType = dto.SplitType,
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            // Create splits
            if (dto.SplitType == "Equal")
            {
                var activeMemberIds = trip
                    .Members.Where(m => m.IsActive)
                    .Select(m => m.UserId)
                    .ToList();
                var splitAmount = dto.Amount / activeMemberIds.Count;

                foreach (var memberId in activeMemberIds)
                {
                    _context.ExpenseSplits.Add(
                        new ExpenseSplit
                        {
                            ExpenseId = expense.Id,
                            UserId = memberId,
                            Amount = splitAmount,
                            IsPaid = memberId == userId
                        }
                    );
                }
            }
            else if (dto.SplitType == "Custom" && dto.Splits != null)
            {
                var totalSplitAmount = dto.Splits.Sum(s => s.Amount ?? 0);
                if (Math.Abs(totalSplitAmount - dto.Amount) > 0.01m)
                    return BadRequest("Split amounts must equal the total expense amount");

                foreach (var split in dto.Splits)
                {
                    if (!trip.Members.Any(m => m.UserId == split.UserId && m.IsActive))
                        return BadRequest($"User {split.UserId} is not a member of this trip");

                    _context.ExpenseSplits.Add(
                        new ExpenseSplit
                        {
                            ExpenseId = expense.Id,
                            UserId = split.UserId,
                            Amount = split.Amount ?? 0,
                            Percentage = split.Percentage,
                            IsPaid = split.UserId == userId,
                        }
                    );
                }
            }
            else if (dto.SplitType == "Percentage" && dto.Splits != null)
            {
                var totalPercentage = dto.Splits.Sum(s => s.Percentage ?? 0);
                if (Math.Abs(totalPercentage - 100) > 0.01m)
                    return BadRequest("Percentages must equal 100%");

                foreach (var split in dto.Splits)
                {
                    if (!trip.Members.Any(m => m.UserId == split.UserId && m.IsActive))
                        return BadRequest($"User {split.UserId} is not a member of this trip");

                    var splitAmount = dto.Amount * (split.Percentage ?? 0) / 100;
                    _context.ExpenseSplits.Add(
                        new ExpenseSplit
                        {
                            ExpenseId = expense.Id,
                            UserId = split.UserId,
                            Amount = splitAmount,
                            Percentage = split.Percentage,
                            IsPaid = split.UserId == userId
                        }
                    );
                }
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetExpense),
                new { id = expense.Id },
                await GetExpenseDto(expense.Id)
            );
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(int id, ExpenseCreateDto dto)
        {
            var userId = _authService.GetUserId();
            var expense = await _context
                .Expenses.Include(e => e.Splits)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null)
                return NotFound();

            expense.Description = dto.Description;
            expense.Amount = dto.Amount;
            expense.PaidByUserId = userId;
            expense.ExpenseDate = dto.ExpenseDate;
            expense.Category = dto.Category;
            expense.SplitType = dto.SplitType;
            expense.UpdatedAt = DateTime.UtcNow;

            // Remove existing splits
            _context.ExpenseSplits.RemoveRange(expense.Splits);

            // Recreate splits (same logic as create)
            var trip = await _context
                .Trips.Include(t => t.Members)
                .FirstOrDefaultAsync(t => t.Id == dto.TripId);

            if (dto.SplitType == "Equal")
            {
                var activeMemberIds = trip
                    .Members.Where(m => m.IsActive)
                    .Select(m => m.UserId)
                    .ToList();
                var splitAmount = dto.Amount / activeMemberIds.Count;

                foreach (var memberId in activeMemberIds)
                {
                    _context.ExpenseSplits.Add(
                        new ExpenseSplit
                        {
                            ExpenseId = expense.Id,
                            UserId = memberId,
                            Amount = splitAmount,
                            IsPaid = memberId == userId,
                        }
                    );
                }
            }
            else if (dto.SplitType == "Custom" && dto.Splits != null)
            {
                foreach (var split in dto.Splits)
                {
                    _context.ExpenseSplits.Add(
                        new ExpenseSplit
                        {
                            ExpenseId = expense.Id,
                            UserId = split.UserId,
                            Amount = split.Amount ?? 0,
                            Percentage = split.Percentage,
                            IsPaid = split.UserId == userId,
                        }
                    );
                }
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expense = await _context
                .Expenses.Include(e => e.Splits)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null)
                return NotFound();

            _context.ExpenseSplits.RemoveRange(expense.Splits);
            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ============================================
        // ExpensesController - UPDATE THIS METHOD
        // ============================================

        [HttpGet("member/{userId}/trip/{tripId}/breakdown")]
        public async Task<ActionResult<MemberExpenseBreakdownDto>> GetMemberExpenseBreakdown(
            int userId,
            int tripId
        )
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var trip = await _context
                .Trips.Include(t => t.Expenses)
                .ThenInclude(e => e.PaidBy)
                .Include(t => t.Expenses)
                .ThenInclude(e => e.Splits)
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip == null)
                return NotFound("Trip not found");

            // Get all expenses where user has a split
            var memberExpenses = trip
                .Expenses.Where(e => e.Splits.Any(s => s.UserId == userId))
                .Select(e => new MemberExpenseItemDto
                {
                    ExpenseId = e.Id,
                    Description = e.Description,
                    TotalAmount = e.Amount,
                    PaidByUserId = e.PaidByUserId,
                    PaidByName = e.PaidBy.Name,
                    UserOwes = e.Splits.First(s => s.UserId == userId).Amount,
                    UserPaid = e.PaidByUserId == userId ? e.Amount : 0,
                    NetAmount =
                        (e.PaidByUserId == userId ? e.Amount : 0)
                        - e.Splits.First(s => s.UserId == userId).Amount,
                    ExpenseDate = e.ExpenseDate,
                })
                .OrderBy(e => e.ExpenseDate)
                .ToList();

            var totalOwes = memberExpenses.Sum(e => e.UserOwes);
            var totalPaid = memberExpenses.Sum(e => e.UserPaid);
            var netBalance = totalPaid - totalOwes;

            // ⭐ NEW: Apply settlements to net balance
            var settlements = await _context
                .Settlements.Include(s => s.FromUser)
                .Include(s => s.ToUser)
                .Where(s => s.TripId == tripId && (s.FromUserId == userId || s.ToUserId == userId))
                .OrderBy(s => s.SettlementDate)
                .ToListAsync();

            // Create settlement items (shown as special expense items)
            var settlementItems = new List<MemberExpenseItemDto>();

            foreach (var settlement in settlements)
            {
                if (settlement.FromUserId == userId)
                {
                    // User made a payment
                    settlementItems.Add(
                        new MemberExpenseItemDto
                        {
                            ExpenseId = -settlement.Id, // Negative to distinguish from expenses
                            Description = $"Payment to {settlement.ToUser.Name}",
                            TotalAmount = settlement.Amount,
                            PaidByUserId = userId,
                            PaidByName = user.Name,
                            UserOwes = 0,
                            UserPaid = settlement.Amount,
                            NetAmount = settlement.Amount, // Positive = reduces debt
                            ExpenseDate = settlement.SettlementDate,
                            IsSettlement = true, // New field
                        }
                    );

                    netBalance += settlement.Amount; // Payment reduces debt
                }
                else if (settlement.ToUserId == userId)
                {
                    // User received a payment
                    settlementItems.Add(
                        new MemberExpenseItemDto
                        {
                            ExpenseId = -settlement.Id,
                            Description = $"Payment from {settlement.FromUser.Name}",
                            TotalAmount = settlement.Amount,
                            PaidByUserId = settlement.FromUserId,
                            PaidByName = settlement.FromUser.Name,
                            UserOwes = 0,
                            UserPaid = 0,
                            NetAmount = -settlement.Amount, // Negative = reduces amount owed to you
                            ExpenseDate = settlement.SettlementDate,
                            IsSettlement = true,
                        }
                    );

                    netBalance -= settlement.Amount; // Receiving payment reduces what's owed to you
                }
            }

            // Combine expenses and settlements, sorted by date
            var allItems = memberExpenses
                .Concat(settlementItems)
                .OrderBy(e => e.ExpenseDate)
                .ToList();

            var breakdown = new MemberExpenseBreakdownDto
            {
                UserId = userId,
                UserName = user.Name,
                UserAvatar = user.Avatar,
                TripId = tripId,
                TripName = trip.Name,
                TotalOwes = totalOwes,
                TotalPaid = totalPaid,
                NetBalance = netBalance, // Now includes settlements!
                Expenses = allItems, // Now includes both expenses and settlements!
            };

            return Ok(breakdown);
        }

        // ============================================
        // ExpensesController - Update participants
        // ============================================

        [HttpPatch("{id}/participants")]
        public async Task<IActionResult> UpdateExpenseParticipants(
            int id,
            [FromBody] List<int> participantUserIds
        )
        {
            var expense = await _context
                .Expenses.Include(e => e.Splits)
                .Include(e => e.Trip)
                .ThenInclude(t => t.Members)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null)
                return NotFound();

            var tripMemberIds = expense
                .Trip.Members.Where(m => m.IsActive)
                .Select(m => m.UserId)
                .ToList();

            foreach (var userId in participantUserIds)
            {
                if (!tripMemberIds.Contains(userId))
                    return BadRequest($"User {userId} is not a member of this trip");
            }

            if (participantUserIds.Count == 0)
                return BadRequest("At least one participant is required");

            _context.ExpenseSplits.RemoveRange(expense.Splits);

            var splitAmount = expense.Amount / participantUserIds.Count;

            foreach (var userId in participantUserIds)
            {
                _context.ExpenseSplits.Add(
                    new ExpenseSplit
                    {
                        ExpenseId = expense.Id,
                        UserId = userId,
                        Amount = splitAmount,
                        IsPaid = userId == expense.PaidByUserId,
                    }
                );
            }

            expense.SplitType =
                participantUserIds.Count == tripMemberIds.Count ? "Equal" : "Custom";
            expense.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id}/participants")]
        public async Task<ActionResult<List<int>>> GetExpenseParticipants(int id)
        {
            var expense = await _context
                .Expenses.Include(e => e.Splits)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null)
                return NotFound();

            var participantIds = expense.Splits.Select(s => s.UserId).ToList();
            return Ok(participantIds);
        }

        private async Task<ExpenseDto> GetExpenseDto(int id)
        {
            var expense = await _context
                .Expenses.Include(e => e.PaidBy)
                .Include(e => e.Splits)
                .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            return new ExpenseDto
            {
                Id = expense.Id,
                TripId = expense.TripId,
                Description = expense.Description,
                Amount = expense.Amount,
                PaidByUserId = expense.PaidByUserId,
                PaidByName = expense.PaidBy.Name,
                ExpenseDate = expense.ExpenseDate,
                Category = expense.Category,
                SplitType = expense.SplitType,
                SplitCount = expense.Splits.Count,
                CreatedAt = expense.CreatedAt,
                Splits = expense
                    .Splits.Select(s => new ExpenseSplitDto
                    {
                        Id = s.Id,
                        UserId = s.UserId,
                        UserName = s.User.Name,
                        Amount = s.Amount,
                        Percentage = s.Percentage,
                        IsPaid = s.IsPaid,
                    })
                    .ToList(),
            };
        }
    }
}
