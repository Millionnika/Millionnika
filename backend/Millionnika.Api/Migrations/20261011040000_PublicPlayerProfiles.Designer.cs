using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261011040000_PublicPlayerProfiles")]
partial class PublicPlayerProfiles
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
        AdminMvp.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.User", entity =>
        {
            entity.Property<string>("Description").IsRequired().HasMaxLength(300)
                .HasColumnType("character varying(300)");
        });
    }
}
