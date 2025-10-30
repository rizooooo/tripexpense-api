using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models
{
    public class Trip
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public int CreatedByUserId { get; set; }

        [ForeignKey("CreatedByUserId")]
        public User CreatedBy { get; set; }

        // Navigation properties
        public ICollection<TripMember> Members { get; set; }
        public ICollection<Expense> Expenses { get; set; }

        [StringLength(100)]
        public string? InviteToken { get; set; }

        public DateTime? InviteTokenExpiry { get; set; }

        public bool IsInviteLinkActive { get; set; } = true;
    }
}
