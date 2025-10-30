using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TripExpenseApi.Models;

namespace TripExpenseApi.Config
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<TripMember> TripMembers { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseSplit> ExpenseSplits { get; set; }
        public DbSet<Settlement> Settlements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User unique constraints
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            // Trip relationships
            modelBuilder
                .Entity<Trip>()
                .HasOne(t => t.CreatedBy)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // TripMember unique constraint
            modelBuilder
                .Entity<TripMember>()
                .HasIndex(tm => new { tm.TripId, tm.UserId })
                .IsUnique();

            // Expense relationships
            modelBuilder
                .Entity<Expense>()
                .HasOne(e => e.PaidBy)
                .WithMany(u => u.ExpensesPaid)
                .HasForeignKey(e => e.PaidByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Settlement relationships
            modelBuilder
                .Entity<Settlement>()
                .HasOne(s => s.FromUser)
                .WithMany(u => u.SettlementsFrom)
                .HasForeignKey(s => s.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder
                .Entity<Settlement>()
                .HasOne(s => s.ToUser)
                .WithMany(u => u.SettlementsTo)
                .HasForeignKey(s => s.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
