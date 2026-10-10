using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261011100000_Friends")]
partial class Friends
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
        PublicPlayerProfiles.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.FriendRelation", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uuid");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            entity.Property<Guid>("RecipientId").HasColumnType("uuid");
            entity.Property<Guid>("SenderId").HasColumnType("uuid");
            entity.Property<string>("Status").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            entity.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
            entity.Property<Guid>("UserHighId").HasColumnType("uuid");
            entity.Property<Guid>("UserLowId").HasColumnType("uuid");
            entity.HasKey("Id");
            entity.HasIndex("RecipientId");
            entity.HasIndex("SenderId");
            entity.HasIndex("UserHighId");
            entity.HasIndex("UserLowId", "UserHighId").IsUnique();
            entity.ToTable("friend_relations", table =>
            {
                table.HasCheckConstraint("CK_friend_relations_DifferentUsers", "\"UserLowId\" <> \"UserHighId\"");
                table.HasCheckConstraint("CK_friend_relations_Status", "\"Status\" IN ('Pending', 'Accepted')");
            });
        });

        modelBuilder.Entity("Millionnika.Api.Entities.FriendRelation", entity =>
        {
            entity.HasOne("Millionnika.Api.Entities.User", null).WithMany().HasForeignKey("RecipientId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.HasOne("Millionnika.Api.Entities.User", null).WithMany().HasForeignKey("SenderId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.HasOne("Millionnika.Api.Entities.User", "UserHigh").WithMany().HasForeignKey("UserHighId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.HasOne("Millionnika.Api.Entities.User", "UserLow").WithMany().HasForeignKey("UserLowId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            entity.Navigation("UserHigh");
            entity.Navigation("UserLow");
        });
    }
}
