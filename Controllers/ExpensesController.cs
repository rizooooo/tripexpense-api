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
                    Currency = e.Trip.Currency,
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

            var hasSettlements = await _context.Settlements.AnyAsync(s =>
                s.TripId == expense.TripId && s.CreatedAt >= expense.CreatedAt
            );

            // var expenseDto = new ExpenseDto
            // {
            //     Id = expense.Id,
            //     Currency = expense.Trip.Currency,
            //     TripId = expense.TripId,
            //     Description =
            //         expense.SplitType == "PaidFor"
            //             ? $"{expense.Description} (Paid for others)"
            //             : expense.Description,
            //     Amount = expense.Amount,
            //     PaidByUserId = expense.PaidByUserId,
            //     PaidByName = expense.PaidBy.Name,
            //     ExpenseDate = expense.ExpenseDate,
            //     Category = expense.Category,
            //     SplitType = expense.SplitType,
            //     SplitCount = expense.Splits.Count,
            //     CreatedAt = expense.CreatedAt,
            //     Splits =
            //         expense
            //             .Splits.Select(s => new ExpenseSplitDto
            //             {
            //                 Id = s.Id,
            //                 UserId = s.UserId,
            //                 UserName = s.User.Name,
            //                 Amount = s.Amount,
            //                 Percentage = s.Percentage,
            //                 IsPaid = s.IsPaid,
            //             })
            //             .ToList() ?? new List<ExpenseSplitDto>(),
            // };

            var expenseDto = new ExpenseDto
            {
                Id = expense.Id,
                // Use ?. for navigation properties that might be null
                Currency = expense.Trip?.Currency,
                TripId = expense.TripId,
                Description =
                    expense.SplitType == "PaidFor"
                        ? $"{expense.Description} (Paid for others)"
                        : expense.Description,
                Amount = expense.Amount,
                PaidByUserId = expense.PaidByUserId,
                // Use ?. for navigation properties that might be null
                PaidByName = expense.PaidBy?.Name,
                ExpenseDate = expense.ExpenseDate,
                Category = expense.Category,
                SplitType = expense.SplitType,
                // Use ?.Count and ?? 0 for a potentially null collection
                SplitCount = expense.Splits?.Count ?? 0,
                CreatedAt = expense.CreatedAt,
                HasSettlements = hasSettlements,
                // Use ?.Select and ?? new List<ExpenseSplitDto>() for the collection itself
                Splits =
                    expense
                        .Splits?.Select(s => new ExpenseSplitDto
                        {
                            Id = s.Id,
                            UserId = s.UserId,
                            // Use ?. for navigation properties in the loop
                            UserName = s.User?.Name,
                            Amount = s.Amount,
                            Percentage = s.Percentage,
                            IsPaid = s.IsPaid,
                        })
                        .ToList() ?? new List<ExpenseSplitDto>(), // Ensures Splits is never null
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
                PaidByUserId = dto.PaidByUserId,
                ExpenseDate = DateTimeOffset.UtcNow,
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
                            IsPaid = memberId == dto.PaidByUserId,
                        }
                    );
                }
            }
            else if (dto.SplitType == "PaidFor" && dto.Splits != null)
            {
                // NEW: "Paid For" mode - Payer covers expense for specific people
                var paidForUserIds = dto
                    .Splits.Where(s => s.Amount > 0)
                    .Select(s => s.UserId)
                    .ToList();

                if (!paidForUserIds.Any())
                    return BadRequest("Must specify at least one person this was paid for");

                var splitAmount = dto.Amount / paidForUserIds.Count;

                foreach (var paidForUserId in paidForUserIds)
                {
                    if (!trip.Members.Any(m => m.UserId == paidForUserId && m.IsActive))
                        return BadRequest($"User {paidForUserId} is not a member of this trip");

                    _context.ExpenseSplits.Add(
                        new ExpenseSplit
                        {
                            ExpenseId = expense.Id,
                            UserId = paidForUserId,
                            Amount = splitAmount,
                            IsPaid = paidForUserId == dto.PaidByUserId, // Usually false since payer is covering for them
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
                            IsPaid = split.UserId == dto.PaidByUserId,
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
                            IsPaid = split.UserId == dto.PaidByUserId,
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
            expense.Category = dto.Category;
            expense.SplitType = dto.SplitType;
            expense.UpdatedAt = DateTimeOffset.UtcNow;

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
            else if (dto.SplitType == "PaidFor" && dto.Splits != null)
            {
                var paidForUserIds = dto
                    .Splits.Where(s => s.Amount > 0)
                    .Select(s => s.UserId)
                    .ToList();

                if (!paidForUserIds.Any())
                    return BadRequest("Must specify at least one person this was paid for");

                var splitAmount = dto.Amount / paidForUserIds.Count;

                foreach (var paidForUserId in paidForUserIds)
                {
                    _context.ExpenseSplits.Add(
                        new ExpenseSplit
                        {
                            ExpenseId = expense.Id,
                            UserId = paidForUserId,
                            Amount = splitAmount,
                            IsPaid = paidForUserId == dto.PaidByUserId,
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
        // ExpensesController - FIX GetMemberExpenseBreakdown
        // ============================================

        // [HttpGet("member/{userId}/trip/{tripId}/breakdown")]
        // public async Task<ActionResult<MemberExpenseBreakdownDto>> GetMemberExpenseBreakdown(
        //     int userId,
        //     int tripId
        // )
        // {
        //     var user = await _context.Users.FindAsync(userId);
        //     if (user == null)
        //         return NotFound("User not found");

        //     var trip = await _context
        //         .Trips.Include(t => t.Expenses)
        //         .ThenInclude(e => e.PaidBy)
        //         .Include(t => t.Expenses)
        //         .ThenInclude(e => e.Splits)
        //         .FirstOrDefaultAsync(t => t.Id == tripId);

        //     if (trip == null)
        //         return NotFound("Trip not found");

        //     // Get all settlements for this user in this trip
        //     var settlements = await _context
        //         .Settlements.Include(s => s.FromUser)
        //         .Include(s => s.ToUser)
        //         .Where(s => s.TripId == tripId && (s.FromUserId == userId || s.ToUserId == userId))
        //         .OrderBy(s => s.SettlementDate)
        //         .ToListAsync();

        //     // ⭐ FIX: Only count settlements where THIS USER PAID (fromUserId)
        //     decimal totalSettlementsPaid = settlements
        //         .Where(s => s.FromUserId == userId)
        //         .Sum(s => s.Amount);

        //     // Build expense list
        //     var memberExpenses = trip
        //         .Expenses.Where(e => e.Splits.Any(s => s.UserId == userId))
        //         .Select(e =>
        //         {
        //             var userOwes = e.Splits.First(s => s.UserId == userId).Amount;
        //             var userPaid = e.PaidByUserId == userId ? e.Amount : 0;
        //             var netAmount = userPaid - userOwes;

        //             return new MemberExpenseItemDto
        //             {
        //                 ExpenseId = e.Id,
        //                 Description = e.Description,
        //                 TotalAmount = e.Amount,
        //                 PaidByUserId = e.PaidByUserId,
        //                 PaidByName = e.PaidBy.Name,
        //                 UserOwes = userOwes,
        //                 UserPaid = userPaid,
        //                 NetAmount = netAmount,
        //                 ExpenseDate = e.ExpenseDate,
        //                 IsSettled = false, // Will be calculated below
        //             };
        //         })
        //         .OrderBy(e => e.ExpenseDate)
        //         .ToList();

        //     var totalOwes = memberExpenses.Sum(e => e.UserOwes);
        //     var totalPaid = memberExpenses.Sum(e => e.UserPaid);
        //     var netBalanceFromExpenses = totalPaid - totalOwes;

        //     // ⭐ FIX: Mark expenses as settled ONLY if user owes AND has paid settlements
        //     if (totalSettlementsPaid > 0)
        //     {
        //         var remainingSettlements = totalSettlementsPaid;

        //         // Only mark expenses where user OWES money (netAmount < 0)
        //         var owedExpenses = memberExpenses
        //             .Where(e => e.NetAmount < 0)
        //             .OrderBy(e => e.ExpenseDate)
        //             .ToList();

        //         foreach (var expense in owedExpenses)
        //         {
        //             var amountOwed = Math.Abs(expense.NetAmount);

        //             if (remainingSettlements >= amountOwed)
        //             {
        //                 expense.IsSettled = true;
        //                 remainingSettlements -= amountOwed;
        //             }
        //             else if (remainingSettlements > 0)
        //             {
        //                 // Partially settled - mark as not settled
        //                 expense.IsSettled = false;
        //                 remainingSettlements = 0;
        //             }
        //         }
        //     }

        //     // ⭐ FIX: DO NOT auto-mark positive netAmount as settled
        //     // Those are expenses where user paid MORE than they owe
        //     // They should only be marked settled when OTHERS pay them back

        //     // Calculate settlements received (when user is ToUserId)
        //     decimal totalSettlementsReceived = settlements
        //         .Where(s => s.ToUserId == userId)
        //         .Sum(s => s.Amount);

        //     // Mark expenses where user paid MORE as settled if they received payments
        //     if (totalSettlementsReceived > 0)
        //     {
        //         var remainingReceived = totalSettlementsReceived;

        //         var paidExpenses = memberExpenses
        //             .Where(e => e.NetAmount > 0)
        //             .OrderBy(e => e.ExpenseDate)
        //             .ToList();

        //         foreach (var expense in paidExpenses)
        //         {
        //             var amountToReceive = Math.Abs(expense.NetAmount);

        //             if (remainingReceived >= amountToReceive)
        //             {
        //                 expense.IsSettled = true;
        //                 remainingReceived -= amountToReceive;
        //             }
        //         }
        //     }

        //     // Calculate final net balance
        //     var finalNetBalance =
        //         netBalanceFromExpenses + totalSettlementsPaid - totalSettlementsReceived;

        //     var settlementItems = settlements
        //         .Select(s => new MemberSettlementItemDto
        //         {
        //             SettlementId = s.Id,
        //             Amount = s.Amount,
        //             SettlementDate = s.SettlementDate,
        //             FromUserName = s.FromUser.Name,
        //             ToUserName = s.ToUser.Name,
        //             Notes = s.Notes,
        //             IsUserPaying = s.FromUserId == userId,
        //         })
        //         .ToList();

        //     var breakdown = new MemberExpenseBreakdownDto
        //     {
        //         UserId = userId,
        //         UserName = user.Name,
        //         UserAvatar = user.Avatar,
        //         TripId = tripId,
        //         TripName = trip.Name,
        //         TotalOwes = totalOwes,
        //         TotalPaid = totalPaid,
        //         NetBalance = finalNetBalance,
        //         Expenses = memberExpenses,
        //         Settlements = settlementItems,
        //     };

        //     return Ok(breakdown);
        // }
        // ============================================
        // ExpensesController - ENHANCED GetMemberExpenseBreakdown
        // ============================================

        // ============================================
        // FIX: GetMemberExpenseBreakdown - Include ALL relevant expenses
        // ============================================

        [HttpGet("member/{userId}/trip/{tripId}/breakdown")]
        public async Task<ActionResult<MemberExpenseBreakdownDto>> GetMemberExpenseBreakdown(
            int userId,
            int tripId,
            [FromQuery] bool myExpensesOnly = false
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
                .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip == null)
                return NotFound("Trip not found");

            var settlements = await _context
                .Settlements.Include(s => s.FromUser)
                .Include(s => s.ToUser)
                .Where(s => s.TripId == tripId && (s.FromUserId == userId || s.ToUserId == userId))
                .ToListAsync();

            var allTransactions = new List<RunningTransactionItemDto>();

            // Filter expenses based on myExpensesOnly parameter
            var relevantExpenses = myExpensesOnly
                ? trip.Expenses.Where(e => e.PaidByUserId == userId).ToList()
                : trip
                    .Expenses.Where(e =>
                        e.Splits.Any(s => s.UserId == userId) || e.PaidByUserId == userId
                    )
                    .ToList();

            foreach (var expense in relevantExpenses)
            {
                var userSplit = expense.Splits.FirstOrDefault(s => s.UserId == userId);
                var userOwes = userSplit?.Amount ?? 0;
                var userPaid = expense.PaidByUserId == userId ? expense.Amount : 0;
                var netAmount = myExpensesOnly ? expense.Amount : (userPaid - userOwes);

                string description = expense.Description;
                string additionalInfo = "";

                if (expense.SplitType == "PaidFor")
                {
                    if (expense.PaidByUserId == userId)
                    {
                        var paidForUsers = expense.Splits.Select(s => s.User.Name).ToList();
                        if (paidForUsers.Any())
                        {
                            additionalInfo = $"Paid for: {string.Join(", ", paidForUsers)}";
                        }
                    }
                    else
                    {
                        additionalInfo = $"{expense.PaidBy.Name} paid for you";
                    }
                }

                var transaction = new RunningTransactionItemDto
                {
                    Date = expense.CreatedAt,
                    Description = expense.Description,
                    Type = "Expense",
                    Amount = netAmount,
                    TransactionId = expense.Id,
                    ExpenseId = expense.Id,
                    PaidByName = expense.PaidBy.Name,
                    TotalExpenseAmount = expense.Amount,
                    IsUserPayer = expense.PaidByUserId == userId,
                    Notes = additionalInfo,
                    SettlementId = null,
                    FromUserName = null,
                    ToUserName = null,
                };

                allTransactions.Add(transaction);
            }

            // Include settlements - for myExpensesOnly, only include payments FROM the user
            var relevantSettlements = myExpensesOnly
                ? settlements.Where(s => s.FromUserId == userId).ToList()
                : settlements;

            foreach (var settlement in relevantSettlements)
            {
                bool isUserPaying = settlement.FromUserId == userId;
                var amount = isUserPaying ? settlement.Amount : -settlement.Amount;

                var transaction = new RunningTransactionItemDto
                {
                    Date = settlement.SettlementDate,
                    Description = isUserPaying
                        ? $"Payment to {settlement.ToUser.Name}"
                        : $"Payment from {settlement.FromUser.Name}",
                    Type = isUserPaying ? "Payment" : "Receipt",
                    Amount = myExpensesOnly ? settlement.Amount : amount,
                    TransactionId = settlement.Id,
                    SettlementId = settlement.Id,
                    FromUserName = settlement.FromUser.Name,
                    ToUserName = settlement.ToUser.Name,
                    Notes = settlement.Notes,
                    ExpenseId = null,
                    PaidByName = null,
                    TotalExpenseAmount = settlement.Amount,
                    IsUserPayer = isUserPaying,
                };

                allTransactions.Add(transaction);
            }

            var sortedTransactions = allTransactions
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t =>
                {
                    if (t.Type == "Payment" || t.Type == "Receipt")
                        return (t.SettlementId ?? 0) + 1000000;
                    else
                        return t.ExpenseId ?? 0;
                })
                .ToList();

            var transactionsOldestFirst = sortedTransactions.AsEnumerable().Reverse().ToList();
            decimal runningBalance = 0;

            foreach (var transaction in transactionsOldestFirst)
            {
                runningBalance += transaction.Amount;
                transaction.RunningBalance = runningBalance;
            }

            var finalNetBalance = runningBalance;

            var breakdown = new MemberExpenseBreakdownDto
            {
                UserId = userId,
                UserName = user.Name,
                UserAvatar = user.Avatar,
                TripId = tripId,
                TripName = trip.Name,
                IsArchived = trip.IsArchived,
                NetBalance = finalNetBalance,
                Transactions = sortedTransactions,
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
            expense.UpdatedAt = DateTimeOffset.UtcNow;

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
                Currency = expense.Trip.Currency,
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
