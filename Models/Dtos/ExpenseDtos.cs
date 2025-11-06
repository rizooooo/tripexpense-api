using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models.Dtos
{
    // Expense DTOs
    public class ExpenseCreateDto
    {
        [Required]
        public int TripId { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public int PaidByUserId { get; set; }

        public string Category { get; set; }

        [Required]
        public string SplitType { get; set; } = "Equal";

        public List<ExpenseSplitCreateDto>? Splits { get; set; }
    }

    public class ExpenseSplitCreateDto
    {
        [Required]
        public int UserId { get; set; }

        public decimal? Amount { get; set; }
        public decimal? Percentage { get; set; }
    }

    public class ExpenseDto
    {
        public int Id { get; set; }
        public int TripId { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public int PaidByUserId { get; set; }
        public string PaidByName { get; set; }
        public DateTimeOffset ExpenseDate { get; set; }
        public string Category { get; set; }
        public string SplitType { get; set; }
        public int SplitCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<ExpenseSplitDto> Splits { get; set; }
    }

    public class ExpenseSplitDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public decimal Amount { get; set; }
        public decimal? Percentage { get; set; }
        public bool IsPaid { get; set; }
    }

    public class MemberExpenseBreakdownDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserAvatar { get; set; }
        public int TripId { get; set; }
        public string TripName { get; set; }
        public decimal NetBalance { get; set; }
        public List<RunningTransactionItemDto> Transactions { get; set; }
    }

    public class MemberExpenseItemDto
    {
        public int ExpenseId { get; set; }
        public string Description { get; set; }
        public decimal TotalAmount { get; set; }
        public int PaidByUserId { get; set; }
        public string PaidByName { get; set; }
        public decimal UserOwes { get; set; }
        public decimal UserPaid { get; set; }
        public decimal NetAmount { get; set; }
        public DateTimeOffset ExpenseDate { get; set; }
        public bool IsSettled { get; set; }
    }

    public class MemberSettlementItemDto
    {
        public int SettlementId { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset SettlementDate { get; set; }
        public string FromUserName { get; set; }
        public string ToUserName { get; set; }
        public string Notes { get; set; }
        public bool IsUserPaying { get; set; } // true if user is paying, false if receiving
    }

    public class RunningTransactionItemDto
    {
        public DateTimeOffset Date { get; set; }
        public string Description { get; set; }
        public string Type { get; set; } // "Expense", "Payment", "Receipt"
        public decimal Amount { get; set; }
        public decimal RunningBalance { get; set; }
        public int TransactionId { get; set; }

        // Expense-specific fields (null for settlements)
        public int? ExpenseId { get; set; }
        public string PaidByName { get; set; }
        public decimal? TotalExpenseAmount { get; set; }
        public bool IsUserPayer { get; set; }

        // Settlement-specific fields (null for expenses)
        public int? SettlementId { get; set; }
        public string FromUserName { get; set; }
        public string ToUserName { get; set; }
        public string Notes { get; set; }
    }
}
