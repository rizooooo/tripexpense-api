using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models.Dtos
{
    // Trip DTOs
    public class TripCreateDto
    {
        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        public string Currency { get; set; }

        public DateTimeOffset? StartDate { get; set; }

        public DateTimeOffset? EndDate { get; set; }

        public List<int> MemberIds { get; set; } = new List<int>();
    }

    public class TripDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Currency { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public int MemberCount { get; set; }
        public decimal TotalExpenses { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<TripMemberDto> Members { get; set; }
    }

    public class CurrencyBalanceDto
    {
        public string Currency { get; set; }
        public string CurrencySymbol { get; set; }
        public decimal Balance { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalOwed { get; set; }
        public int TripCount { get; set; }
    }

    public class UserDashboardDto
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Avatar { get; set; }

        // ⭐ NEW: Per-currency balances
        public List<CurrencyBalanceDto> CurrencyBalances { get; set; }

        // Keep for backward compatibility (can be removed later)
        [Obsolete("Use CurrencyBalances instead")]
        public decimal OverallBalance { get; set; }
        public int TotalTrips { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalOwed { get; set; }
        public List<TripSummaryWithBalanceDto> RecentTrips { get; set; }
    }

    public class TripMemberDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
        public string Role { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
    }

    public class TripSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Currency { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public int MemberCount { get; set; }
        public decimal TotalExpenses { get; set; }
    }

    // ============================================
    // DTOs - New DTOs for dashboard and invites
    // ============================================

    public class TripDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Currency { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public int MemberCount { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal YourShare { get; set; }
        public decimal YourBalance { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<TripMemberDto> Members { get; set; }
    }

    public class TripSummaryWithBalanceDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public int MemberCount { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal YourBalance { get; set; }

        public string Currency { get; set; } // ⭐ NEW
        public string CurrencySymbol { get; set; } // ⭐ NEW
    }

    public class TripInviteDto
    {
        public int TripId { get; set; }
        public string TripName { get; set; }
        public string InviteLink { get; set; }
        public string InviteToken { get; set; }
        public DateTimeOffset? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class JoinTripDto
    {
        [Required]
        public string InviteToken { get; set; }
    }

    public class JoinTripResponse
    {
        public string Message { get; set; } = default!;
        public int TripId { get; set; }
    }

    public class TripInviteInfoDto
    {
        public int TripId { get; set; }
        public string TripName { get; set; }
        public string Description { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public int MemberCount { get; set; }
        public string CreatedByName { get; set; }
        public bool IsValid { get; set; }
        public string Message { get; set; }
    }
}
