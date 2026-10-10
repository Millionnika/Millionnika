using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Millionnika.Api.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Millionnika.Api.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261011200000_ContractCatalog")]
public sealed class ContractCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "contracts",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE contracts
            SET "Description" = 'Рекламный дрон слишком громко обещает скидки. Пора устроить ему внеплановый режим тишины.'
            WHERE "Id" = '1c92d38c-6aca-4d33-9044-a4a92f6a6101' AND "Description" = '';

            INSERT INTO contracts ("Id", "Name", "Description", "CostNerve", "SuccessChance", "RewardCredits", "RewardExperience", "CooldownSeconds", "IsActive")
            VALUES
              ('1c92d38c-6aca-4d33-9044-a4a92f6a6102', 'Взлом автомата с лапшой',
               'Автомат уверен, что соус — премиум-подписка. Убедите его пересмотреть бизнес-модель.', 1, 0.9000, 15, 4, 0, true),
              ('1c92d38c-6aca-4d33-9044-a4a92f6a6103', 'Подмена корпоративной рекламы',
               'Замените бодрый лозунг корпорации на честное «мы тоже не знаем, что происходит».', 3, 0.7000, 80, 15, 0, true),
              ('1c92d38c-6aca-4d33-9044-a4a92f6a6104', 'Доставка подозрительно вежливого дрона',
               'Курьерский дрон говорит «пожалуйста» чаще, чем люди. Доставьте его и не задавайте лишних вопросов.', 4, 0.6000, 120, 20, 0, true)
            ON CONFLICT ("Id") DO NOTHING;

            ALTER TABLE contracts ALTER COLUMN "Description" DROP DEFAULT;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData("contracts", "Id", Guid.Parse("1c92d38c-6aca-4d33-9044-a4a92f6a6102"));
        migrationBuilder.DeleteData("contracts", "Id", Guid.Parse("1c92d38c-6aca-4d33-9044-a4a92f6a6103"));
        migrationBuilder.DeleteData("contracts", "Id", Guid.Parse("1c92d38c-6aca-4d33-9044-a4a92f6a6104"));
        migrationBuilder.DropColumn("Description", "contracts");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        BuildModel(modelBuilder);
    }

    internal static void BuildModel(ModelBuilder modelBuilder)
    {
        BindRequestIdToOperation.BuildModel(modelBuilder);
        modelBuilder.Entity("Millionnika.Api.Entities.Contract")
            .Property<string>("Description")
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");
    }
}
