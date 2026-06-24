using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(c => c.Document)
            .IsRequired()
            .HasMaxLength(14); // CPF=11, CNPJ=14

        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        // Unique index: active customers per email
        builder.HasIndex(c => new { c.Email, c.IsActive })
            .HasFilter("[IsActive] = 1")
            .IsUnique()
            .HasDatabaseName("UX_Customers_Email_Active");

        // Unique index: active customers per document
        builder.HasIndex(c => new { c.Document, c.IsActive })
            .HasFilter("[IsActive] = 1")
            .IsUnique()
            .HasDatabaseName("UX_Customers_Document_Active");
    }
}
