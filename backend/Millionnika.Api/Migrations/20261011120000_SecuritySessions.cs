using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261011120000_SecuritySessions")]
public sealed class SecuritySessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("user_sessions", table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_user_sessions", value => value.Id);
            table.ForeignKey("FK_user_sessions_users_UserId", value => value.UserId, "users", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_user_sessions_UserId", "user_sessions", "UserId");
        migrationBuilder.CreateIndex("IX_user_sessions_ExpiresAtUtc", "user_sessions", "ExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("user_sessions");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        Friends.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.UserSession", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("uuid");
            entity.Property<Guid>("UserId").HasColumnType("uuid");
            entity.Property<DateTimeOffset>("ExpiresAtUtc").HasColumnType("timestamp with time zone");
            entity.HasKey("Id");
            entity.HasIndex("UserId");
            entity.HasIndex("ExpiresAtUtc");
            entity.ToTable("user_sessions");
            entity.HasOne("Millionnika.Api.Entities.User", null).WithMany().HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
        });
    }
}
