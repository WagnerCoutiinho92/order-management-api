using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Data.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.PreviousStatus);
        builder.Property(h => h.NewStatus).IsRequired();
        builder.Property(h => h.ChangedAt).IsRequired();

        builder.Property(h => h.Reason)
            .HasMaxLength(500);
    }
}
