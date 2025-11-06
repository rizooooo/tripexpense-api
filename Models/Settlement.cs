using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models
{
    public class Settlement
    {
        [Key]
        public int Id { get; set; }

        public int TripId { get; set; }

        [ForeignKey("TripId")]
        public Trip Trip { get; set; }

        public int FromUserId { get; set; }

        [ForeignKey("FromUserId")]
        public User FromUser { get; set; }

        public int ToUserId { get; set; }

        [ForeignKey("ToUserId")]
        public User ToUser { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTimeOffset SettlementDate { get; set; } = DateTimeOffset.UtcNow;

        [StringLength(500)]
        public string Notes { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
