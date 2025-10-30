using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models.Dtos
{
    // Settlement DTOs
    public class SettlementCreateDto
    {
        [Required]
        public int TripId { get; set; }

        [Required]
        public int FromUserId { get; set; }

        [Required]
        public int ToUserId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        public string Notes { get; set; }
    }

    public class SettlementDto
    {
        public int Id { get; set; }
        public int TripId { get; set; }
        public int FromUserId { get; set; }
        public string FromUserName { get; set; }
        public int ToUserId { get; set; }
        public string ToUserName { get; set; }
        public decimal Amount { get; set; }
        public DateTime SettlementDate { get; set; }
        public string Notes { get; set; }
    }

    public class SettlementSuggestionDto
    {
        public int FromUserId { get; set; }
        public string FromUserName { get; set; }
        public int ToUserId { get; set; }
        public string ToUserName { get; set; }
        public decimal Amount { get; set; }
    }
}
