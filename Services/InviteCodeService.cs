using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ============================================
// Services - Invite Code Generator
// ============================================

namespace TripExpenseApi.Services
{
    public interface IInviteCodeService
    {
        string GenerateInviteCode();
        string GenerateWordCode();
    }

    public class InviteCodeService : IInviteCodeService
    {
        private static readonly Random _random = new Random();

        // ⭐ RECOMMENDED: 6-digit code (easy to type, good balance)
        public string GenerateInviteCode()
        {
            return _random.Next(100000, 999999).ToString();
        }

        // Alternative: 4-letter code (even easier, but less unique)
        public string Generate4LetterCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No 0/O, 1/I confusion
            return new string(
                Enumerable.Range(0, 4).Select(_ => chars[_random.Next(chars.Length)]).ToArray()
            );
        }

        // Alternative: 6-character alphanumeric (best balance)
        public string Generate6CharCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            return new string(
                Enumerable.Range(0, 6).Select(_ => chars[_random.Next(chars.Length)]).ToArray()
            );
        }

        // Alternative: Word-based (most memorable)
        public string GenerateWordCode()
        {
            var words = new[]
            {
                "BEACH",
                "SUNNY",
                "TRIP",
                "FUN",
                "PARTY",
                "CHILL",
                "MOON",
                "STAR",
                "WAVE",
                "COOL",
                "JAPAN",
                "OSAKA",
                "SAPPORO",
                "TOKYO",
                "CEBU",
            };
            var numbers = _random.Next(10, 99);
            return $"{words[_random.Next(words.Length)]}{numbers}";
        }
    }
}
