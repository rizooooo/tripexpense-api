using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(500)]
        public string Avatar { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<TripMember> TripMemberships { get; set; }
        public ICollection<Expense> ExpensesPaid { get; set; }
        public ICollection<ExpenseSplit> ExpenseSplits { get; set; }
        public ICollection<Settlement> SettlementsFrom { get; set; }
        public ICollection<Settlement> SettlementsTo { get; set; }

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; }

        public DateTimeOffset? LastLoginAt { get; set; }

        public bool IsEmailVerified { get; set; } = false;
    }
}
