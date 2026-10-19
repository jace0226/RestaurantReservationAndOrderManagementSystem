using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<Customer> Customers { get; set; }

        public DbSet<Staff> Staffs { get; set; }

        public DbSet<Feedback> Feedbacks { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<Transaction> Transactions { get; set; }

        public DbSet<Food_Items> FoodItems { get; set; }

        public DbSet<Order_Items> OrderItems { get; set; }

        public DbSet<Booking> Bookings { get; set; }

        public DbSet<Table> Tables { get; set; }

        public DbSet<Promotion> Promotions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Staff)
                .WithMany()
                .HasForeignKey(o => o.StaffID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Booking)
                .WithMany()
                .HasForeignKey(o => o.BookingID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Table)
                .WithMany()
                .HasForeignKey(o => o.TableID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Promotion>()
                .HasOne(p => p.Food)
                .WithMany()
                .HasForeignKey(p => p.FoodID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Promotion>()
                .HasOne(p => p.Staff)
                .WithMany()
                .HasForeignKey(p => p.StaffID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Customer>()
                .HasOne(c => c.User)
                .WithOne()
                .HasForeignKey<Customer>(c => c.UserID);

            modelBuilder.Entity<Staff>()
                .HasOne(s => s.User)
                .WithOne()
                .HasForeignKey<Staff>(s => s.UserID);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Order)
                .WithOne()
                .HasForeignKey<Transaction>(t => t.OrderID);

            modelBuilder.Entity<Order_Items>()
                .HasOne(oi => oi.Order)
                .WithMany()
                .HasForeignKey(oi => oi.OrderID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Order_Items>()
                .HasOne(oi => oi.Food)
                .WithMany()
                .HasForeignKey(oi => oi.FoodID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Food_Items>()
                .HasOne(fi => fi.Staff)
                .WithMany()
                .HasForeignKey(fi => fi.StaffID) 
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Customer)
                .WithMany()
                .HasForeignKey(b => b.CustomerID);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Table)
                .WithMany()
                .HasForeignKey(b => b.TableID)
                .OnDelete(DeleteBehavior.NoAction);

        }
    }
}
