using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }

        public int TripId { get; set; }

        [ForeignKey("TripId")]
        public Trip Trip { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public int PaidByUserId { get; set; }

        [ForeignKey("PaidByUserId")]
        public User PaidBy { get; set; }

        public DateTime ExpenseDate { get; set; }

        [StringLength(50)]
        public string Category { get; set; } // Food, Transport, Accommodation, etc.

        [StringLength(20)]
        public string SplitType { get; set; } = "Equal"; // Equal, Custom, Percentage

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<ExpenseSplit> Splits { get; set; }
    }
}
