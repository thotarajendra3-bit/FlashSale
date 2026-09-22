using Microsoft.EntityFrameworkCore;
using FlashSale.Api.Models;

namespace FlashSale.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "iPhone",
                    Stock = 5
                },
                new Product
                {
                    Id = 2,
                    Name = "Laptop",
                    Stock = 10
                }
            );
        }

    }
}
