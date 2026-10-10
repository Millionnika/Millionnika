using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261010160000_GameplayActions")]
partial class GameplayActions
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
        InitialAccounts.BuildModel(modelBuilder);

        modelBuilder.Entity("Millionnika.Api.Entities.Contract", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property<int>("CooldownSeconds").HasColumnType("integer");
            entity.Property<int>("CostNerve").HasColumnType("integer");
            entity.Property<bool>("IsActive").HasColumnType("boolean");
            entity.Property<string>("Name").IsRequired().HasMaxLength(120).HasColumnType("character varying(120)");
            entity.Property<long>("RewardCredits").HasColumnType("bigint");
            entity.Property<long>("RewardExperience").HasColumnType("bigint");
            entity.Property<decimal>("SuccessChance").HasPrecision(5, 4).HasColumnType("numeric(5,4)");
            entity.HasKey("Id");
            entity.ToTable("contracts");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.ActionLog", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            entity.Property<string>("ActionType").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            entity.Property<long>("CreditsAwarded").HasColumnType("bigint");
            entity.Property<int>("EnergySpent").HasColumnType("integer");
            entity.Property<string>("ErrorCode").HasMaxLength(40).HasColumnType("character varying(40)");
            entity.Property<long>("ExperienceAwarded").HasColumnType("bigint");
            entity.Property<string>("Message").IsRequired().HasMaxLength(240).HasColumnType("character varying(240)");
            entity.Property<int>("NerveSpent").HasColumnType("integer");
            entity.Property<Guid>("RequestId").HasColumnType("uuid");
            entity.Property<int>("StatGain").HasColumnType("integer");
            entity.Property<string>("StatName").HasMaxLength(16).HasColumnType("character varying(16)");
            entity.Property<bool>("Succeeded").HasColumnType("boolean");
            entity.Property<Guid>("UserId").HasColumnType("uuid");
            entity.HasKey("Id");
            entity.HasIndex("UserId", "RequestId").IsUnique();
            entity.ToTable("action_logs");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.ActionLog", entity =>
        {
            entity.HasOne("Millionnika.Api.Entities.User", "User")
                .WithMany("ActionLogs")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            entity.Navigation("User");
        });

        modelBuilder.Entity("Millionnika.Api.Entities.User", entity =>
        {
            entity.Navigation("ActionLogs");
        });
    }
}
