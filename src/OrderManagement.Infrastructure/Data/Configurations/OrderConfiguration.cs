using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status).IsRequired();

        builder.Property(o => o.TotalValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.CreatedAt).IsRequired();

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.StatusHistory)
            .WithOne(h => h.Order)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Private backing fields for collections
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
