using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models
{
    public class TripMember
    {
        [Key]
        public int Id { get; set; }

        public int TripId { get; set; }

        [ForeignKey("TripId")]
        public Trip Trip { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [StringLength(50)]
        public string Role { get; set; } = "Member"; // Admin, Member

        public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

        public bool IsActive { get; set; } = true;
    }
}
