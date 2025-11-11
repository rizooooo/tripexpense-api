using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Helpers
{
    // ============================================
    // Helper Service - Currency Symbols
    // ============================================

    public static class CurrencyHelper
    {
        private static readonly Dictionary<string, string> CurrencySymbols = new()
        {
            { "PHP", "₱" },
            { "USD", "$" },
            { "EUR", "€" },
            { "GBP", "£" },
            { "JPY", "¥" },
            { "CNY", "¥" },
            { "KRW", "₩" },
            { "SGD", "S$" },
            { "MYR", "RM" },
            { "THB", "฿" },
            { "VND", "₫" },
            { "IDR", "Rp" },
            { "AUD", "A$" },
            { "CAD", "C$" },
            { "CHF", "Fr" },
            { "INR", "₹" },
            { "AED", "د.إ" },
            { "SAR", "﷼" },
            { "BRL", "R$" },
            { "MXN", "Mex$" },
            { "RUB", "₽" },
            { "ZAR", "R" },
            { "NZD", "NZ$" },
            { "HKD", "HK$" },
            { "TWD", "NT$" },
        };

        public static string GetSymbol(string currency)
        {
            return CurrencySymbols.TryGetValue(currency?.ToUpper() ?? "", out var symbol)
                ? symbol
                : currency ?? "₱";
        }
    }
}
