using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261010130000_InitialAccounts")]
partial class InitialAccounts
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
#pragma warning restore 612, 618
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("Millionnika.Api.Entities.GameCharacter", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property<long>("Credits").HasColumnType("bigint");
            entity.Property<int>("Defense").HasColumnType("integer");
            entity.Property<int>("Dexterity").HasColumnType("integer");
            entity.Property<int>("Energy").HasColumnType("integer");
            entity.Property<long>("Experience").HasColumnType("bigint");
            entity.Property<int>("Health").HasColumnType("integer");
            entity.Property<int>("Level").HasColumnType("integer");
            entity.Property<int>("MaxEnergy").HasColumnType("integer");
            entity.Property<int>("MaxHealth").HasColumnType("integer");
            entity.Property<int>("MaxNerve").HasColumnType("integer");
            entity.Property<int>("Nerve").HasColumnType("integer");
            entity.Property<int>("Speed").HasColumnType("integer");
            entity.Property<int>("Strength").HasColumnType("integer");
            entity.Property<Guid>("UserId").HasColumnType("uuid");
            entity.HasKey("Id");
            entity.HasIndex("UserId").IsUnique();
            entity.ToTable("characters");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.User", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            entity.Property<string>("Email").IsRequired().HasMaxLength(254).HasColumnType("character varying(254)");
            entity.Property<string>("NormalizedEmail").IsRequired().HasMaxLength(254).HasColumnType("character varying(254)");
            entity.Property<string>("NormalizedUsername").IsRequired().HasMaxLength(24).HasColumnType("character varying(24)");
            entity.Property<string>("PasswordHash").IsRequired().HasMaxLength(512).HasColumnType("character varying(512)");
            entity.Property<string>("Username").IsRequired().HasMaxLength(24).HasColumnType("character varying(24)");
            entity.HasKey("Id");
            entity.HasIndex("NormalizedEmail").IsUnique();
            entity.HasIndex("NormalizedUsername").IsUnique();
            entity.ToTable("users");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.GameCharacter", entity =>
        {
            entity.HasOne("Millionnika.Api.Entities.User", "User")
                .WithOne("Character")
                .HasForeignKey("Millionnika.Api.Entities.GameCharacter", "UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            entity.Navigation("User");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.User", entity =>
        {
            entity.Navigation("Character").IsRequired();
        });
    }
}
