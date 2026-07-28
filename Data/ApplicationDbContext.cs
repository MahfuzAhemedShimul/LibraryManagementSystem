using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace LibraryManagementSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Author> Authors { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Borrowing> Borrowings { get; set; }
        public DbSet<Fine> Fines { get; set; }
        public DbSet<Purchase> Purchases { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Prevent accidental cascade delete chains between Borrowing's two user relationships
            builder.Entity<Borrowing>()
                .HasOne(b => b.Member)
                .WithMany()
                .HasForeignKey(b => b.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Borrowing>()
                .HasOne(b => b.IssuedByStaff)
                .WithMany()
                .HasForeignKey(b => b.IssuedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // Set precision for the fine amount to avoid silent truncation
            builder.Entity<Fine>()
                .Property(f => f.Amount)
                .HasPrecision(10, 2);

            // Set precision for the purchase price paid
            builder.Entity<Purchase>()
                .Property(p => p.PricePaid)
                .HasPrecision(10, 2);
        }
    }
}