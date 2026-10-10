using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261011010000_AdminMvp")]
partial class AdminMvp
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
#pragma warning restore 612, 618
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        NpcShopInventory.BuildModel(modelBuilder);

        modelBuilder.Entity("Millionnika.Api.Entities.User", entity =>
        {
            entity.Property<bool>("IsBlocked").HasColumnType("boolean");
            entity.Property<string>("Role").IsRequired().HasMaxLength(16)
                .HasColumnType("character varying(16)");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.AdminAuditLog", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property<string>("Action").IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            entity.Property<Guid>("AdminUserId").HasColumnType("uuid");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            entity.Property<string>("NewValue").IsRequired().HasColumnType("jsonb");
            entity.Property<string>("OldValue").IsRequired().HasColumnType("jsonb");
            entity.Property<string>("Reason").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)");
            entity.Property<string>("TargetId").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<string>("TargetType").IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            entity.HasKey("Id");
            entity.HasIndex("AdminUserId");
            entity.HasIndex("CreatedAtUtc");
            entity.ToTable("admin_audit_logs");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.AdminAuditLog", entity =>
        {
            entity.HasOne("Millionnika.Api.Entities.User", "AdminUser")
                .WithMany("AdminAuditLogs").HasForeignKey("AdminUserId")
                .OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.Navigation("AdminUser");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.User", entity => entity.Navigation("AdminAuditLogs"));
    }
}
