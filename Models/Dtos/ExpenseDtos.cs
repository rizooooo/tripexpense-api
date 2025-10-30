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

        [Required]
        public DateTime ExpenseDate { get; set; }

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
        public DateTime ExpenseDate { get; set; }
        public string Category { get; set; }
        public string SplitType { get; set; }
        public int SplitCount { get; set; }
        public DateTime CreatedAt { get; set; }
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
        public decimal TotalOwes { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal NetBalance { get; set; }
        public List<MemberExpenseItemDto> Expenses { get; set; }
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
        public decimal NetAmount { get; set; } // Positive if user gets back, negative if owes
        public DateTime ExpenseDate { get; set; }
        public bool IsSettlement { get; set; } = false; // ⭐ NEW: Flag for settlements
    }
}
