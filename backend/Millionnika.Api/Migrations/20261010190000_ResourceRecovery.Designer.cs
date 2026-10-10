using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261010190000_ResourceRecovery")]
partial class ResourceRecovery
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
        GameplayActions.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.GameCharacter", entity =>
        {
            entity.Property<DateTimeOffset>("LastEnergyRecoveryAtUtc")
                .HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset>("LastNerveRecoveryAtUtc")
                .HasColumnType("timestamp with time zone");
        });
    }
}
