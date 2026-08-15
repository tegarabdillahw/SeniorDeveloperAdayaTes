using Microsoft.EntityFrameworkCore;
using static SeniorDeveloperAdayaTes.Models.OrderModels;

namespace SeniorDeveloperAdayaTes.Data
{
    public class AppDbContexts : DbContext
    {
        public AppDbContexts(DbContextOptions<AppDbContexts> options) : base(options) { }
        public DbSet<Product> products { get; set; }
        public DbSet<Order> orders { get; set; }
        public DbSet<OrderItem> orderItems { get; set; }
        public DbSet<IdempotencyKey> IdempotencyKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(x => x.Id).UseIdentityColumn();
                entity.Property(x => x.Name).HasMaxLength(200);
                entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(x => x.Id).ValueGeneratedNever();
                entity.Property(x => x.ShippingAddress).HasMaxLength(500);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(x => x.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.Property(x => x.ProductName).HasMaxLength(200);
                entity.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");

                entity.HasOne(x => x.Order)
                    .WithMany(x => x.Items)
                    .HasForeignKey(x => x.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IdempotencyKey>(entity =>
            {
                entity.HasKey(x => x.Key);
                entity.Property(x => x.Key).HasMaxLength(64);
            });
        }

    }
}
