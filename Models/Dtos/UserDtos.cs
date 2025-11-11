using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TripExpenseApi.Models.Dtos
{
    // User DTOs
    public class UserCreateDto
    {
        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string PhoneNumber { get; set; }
        public string Avatar { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Avatar { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class UserBalanceDto
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Currency { get; set; }
        public string Avatar { get; set; }
        public decimal Balance { get; set; }
    }
}
