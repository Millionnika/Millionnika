using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Hunting;
using Millionnika.Api.Inventory;

namespace Millionnika.Api.Admin;

public sealed class NpcAdminService(GameDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminNpcResponse>> GetAllAsync(CancellationToken token)
    {
        var templates = await dbContext.NpcTemplates.AsNoTracking().Include(value => value.LootRows)
            .ThenInclude(value => value.Item).ToListAsync(token);
        var huntingRobots = await dbContext.HuntingTargets.AsNoTracking().Include(value => value.LootRows)
            .ThenInclude(value => value.Item).ToListAsync(token);
        return templates.Select(ToResponse).Concat(huntingRobots.Select(ToResponse))
            .OrderBy(value => value.Category).ThenBy(value => value.Name).ThenBy(value => value.Id).ToList();
    }

    public async Task<AdminMutationResult> CreateAsync(Guid adminId, SaveNpcRequest request, CancellationToken token)
    {
        if (!IsValid(request)) return AdminMutationResult.Failure("Проверьте категорию, боевые поля, изображения и причину.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        if (!await ValidLootItemsAsync(request.LootRows, token)) return AdminMutationResult.Failure("Проверьте предметы и дубликаты добычи.");
        var value = new NpcTemplate { Id = Guid.NewGuid() };
        Apply(value, request);
        dbContext.NpcTemplates.Add(value);
        if (request.LootRows is not null) value.LootRows = request.LootRows.Select(row => new NpcTemplateLootRow
        {
            Id = Guid.NewGuid(), NpcId = value.Id, ItemId = row.ItemId, ChancePercent = row.ChancePercent,
            MinimumQuantity = row.MinimumQuantity, MaximumQuantity = row.MaximumQuantity
        }).ToList();
        AddAudit(adminId, "npc_create", value.Id, new { }, ToResponse(value), request.Reason!);
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return AdminMutationResult.Success("Шаблон НПЦ создан.");
    }

    public async Task<AdminMutationResult> UpdateAsync(Guid adminId, Guid id, SaveNpcRequest request, CancellationToken token)
    {
        if (!IsValid(request)) return AdminMutationResult.Failure("Проверьте категорию, боевые поля, изображения и причину.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        if (!await ValidLootItemsAsync(request.LootRows, token)) return AdminMutationResult.Failure("Проверьте предметы и дубликаты добычи.");
        var value = await dbContext.NpcTemplates.FromSqlInterpolated($"SELECT * FROM npc_templates WHERE \"Id\"={id} FOR UPDATE")
            .SingleOrDefaultAsync(token);
        if (value is not null)
        {
            var oldRows = await dbContext.NpcTemplateLootRows.AsNoTracking().Include(row => row.Item)
                .Where(row => row.NpcId == id).ToListAsync(token);
            var old = ToResponse(value, oldRows);
            Apply(value, request);
            var newRows = oldRows;
            if (request.LootRows is not null)
            {
                await dbContext.NpcTemplateLootRows.Where(row => row.NpcId == id).ExecuteDeleteAsync(token);
                newRows = request.LootRows.Select(row => new NpcTemplateLootRow
                {
                    Id = Guid.NewGuid(), NpcId = id, ItemId = row.ItemId,
                    ChancePercent = row.ChancePercent, MinimumQuantity = row.MinimumQuantity, MaximumQuantity = row.MaximumQuantity
                }).ToList();
                dbContext.NpcTemplateLootRows.AddRange(newRows);
            }
            AddAudit(adminId, "npc_update", id, old, ToResponse(value, newRows), request.Reason!);
        }
        else
        {
            // Existing hunting robots are their own canonical NPC records, not copied templates.
            var robot = await dbContext.HuntingTargets.FromSqlInterpolated($"SELECT * FROM hunting_targets WHERE \"Id\"={id} FOR UPDATE")
                .SingleOrDefaultAsync(token);
            if (robot is null) return AdminMutationResult.Failure("НПЦ не найден.");
            if (request.Category != robot.Category) return AdminMutationResult.Failure("Категорию НПЦ, связанного с охотой, менять нельзя.");
            var oldRows = await dbContext.HuntingLootRows.AsNoTracking().Include(row => row.Item)
                .Where(row => row.TargetId == id).ToListAsync(token);
            var old = ToResponse(robot, oldRows);
            Apply(robot, request);
            var newRows = oldRows;
            if (request.LootRows is not null)
            {
                await dbContext.HuntingLootRows.Where(row => row.TargetId == id).ExecuteDeleteAsync(token);
                newRows = request.LootRows.Select(row => new HuntingLootRow
                {
                    Id = Guid.NewGuid(), TargetId = id, ItemId = row.ItemId,
                    ChancePercent = row.ChancePercent, MinimumQuantity = row.MinimumQuantity, MaximumQuantity = row.MaximumQuantity
                }).ToList();
                dbContext.HuntingLootRows.AddRange(newRows);
            }
            AddAudit(adminId, "npc_hunting_robot_update", id, old, ToResponse(robot, newRows), request.Reason!);
        }
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return AdminMutationResult.Success("Шаблон НПЦ обновлён.");
    }

    private void AddAudit(Guid adminId, string action, Guid id, object oldValue, object newValue, string reason) =>
        dbContext.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(), AdminUserId = adminId, Action = action,
            TargetType = "npc", TargetId = id.ToString(), OldValue = JsonSerializer.Serialize(oldValue),
            NewValue = JsonSerializer.Serialize(newValue), Reason = reason.Trim(), CreatedAtUtc = DateTimeOffset.UtcNow
        });

    private static bool IsValid(SaveNpcRequest value)
    {
        if (value.Category is not ("peaceful" or "robot" or "mutant" or "animal") ||
            string.IsNullOrWhiteSpace(value.Name) || value.Name.Trim().Length > 120 ||
            string.IsNullOrWhiteSpace(value.Description) || value.Description.Trim().Length > 500 ||
            !AdminRules.IsReasonValid(value.Reason) || !ValidImagePath(value.PortraitPath) || !ValidImagePath(value.FigurePath) ||
            !ValidLoot(value.LootRows))
            return false;
        var hasAnyCombat = value.Health.HasValue || value.Strength.HasValue || value.Toughness.HasValue ||
            value.Reflexes.HasValue || value.Agility.HasValue || !string.IsNullOrWhiteSpace(value.WeaponName) ||
            value.WeaponDamage.HasValue || value.WeaponAccuracy.HasValue || value.Armor.HasValue ||
            !string.IsNullOrWhiteSpace(value.ArmorCoverage);
        if (value.Category == "peaceful" && !hasAnyCombat) return value.LootRows is null or { Count: 0 } && value.ArmorZones is null;
        return value.Health is > 0 and <= 1_000_000 && value.Strength is > 0 and <= GameNumericLimits.MaxStatValue &&
            value.Toughness is > 0 and <= GameNumericLimits.MaxStatValue &&
            value.Reflexes is > 0 and <= GameNumericLimits.MaxStatValue &&
            value.Agility is > 0 and <= GameNumericLimits.MaxStatValue &&
            !string.IsNullOrWhiteSpace(value.WeaponName) && value.WeaponName.Trim().Length <= 120 &&
            value.WeaponDamage is >= 1 and <= 1000 && value.WeaponAccuracy is >= 0 and <= 100 &&
            (value.ArmorZones is not null ? NpcZoneArmor.IsValid(value.ArmorZones) :
                value.Armor is >= 0 and <= 100 &&
                (value.Armor == 0 ? value.ArmorCoverage == "{}" : EquipmentRules.IsCoverageValid(value.ArmorCoverage)));
    }

    private static bool ValidLoot(IReadOnlyList<UpdateHuntingLootRowRequest>? rows) => rows is null ||
        rows.Count <= 30 && rows.Select(row => row.ItemId).Distinct().Count() == rows.Count &&
        rows.All(row => row.ItemId != Guid.Empty && row.ChancePercent is >= 0 and <= 100 &&
            row.ChancePercent * 1000 == decimal.Truncate(row.ChancePercent * 1000) &&
            row.MinimumQuantity is >= 1 and <= 100 && row.MaximumQuantity >= row.MinimumQuantity && row.MaximumQuantity <= 100);

    private async Task<bool> ValidLootItemsAsync(IReadOnlyList<UpdateHuntingLootRowRequest>? rows, CancellationToken token) =>
        rows is null || await dbContext.Items.CountAsync(item => rows.Select(row => row.ItemId).Contains(item.Id), token) == rows.Count;

    private static bool ValidImagePath(string? value) => string.IsNullOrWhiteSpace(value) ||
        (value.Length <= 500 && !value.Any(char.IsControl) && !value.Contains('\\') &&
         ((value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)) ||
          Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps));

    private static void Apply(NpcTemplate value, SaveNpcRequest request)
    {
        value.Category = request.Category!; value.Name = request.Name!.Trim(); value.Description = request.Description!.Trim();
        value.IsActive = request.IsActive; value.PortraitPath = Clean(request.PortraitPath); value.FigurePath = Clean(request.FigurePath);
        value.Health = request.Health; value.Strength = request.Strength; value.Toughness = request.Toughness;
        value.Reflexes = request.Reflexes; value.Agility = request.Agility; value.WeaponName = Clean(request.WeaponName);
        value.WeaponDamage = request.WeaponDamage; value.WeaponAccuracy = request.WeaponAccuracy;
        value.Armor = request.ArmorZones is null ? request.Armor : value.Armor ?? 0;
        value.ArmorCoverage = request.ArmorZones is null ? Clean(request.ArmorCoverage) : value.ArmorCoverage ?? "{}";
        value.ArmorZones = request.ArmorZones is not null ? NpcZoneArmor.Write(request.ArmorZones) :
            NpcZoneArmor.Write(NpcZoneArmor.FromLegacy(value.Armor ?? 0, value.ArmorCoverage));
    }

    private static void Apply(HuntingTarget value, SaveNpcRequest request)
    {
        value.Name = request.Name!.Trim(); value.Description = request.Description!.Trim(); value.IsActive = request.IsActive;
        value.PortraitPath = Clean(request.PortraitPath); value.FigurePath = Clean(request.FigurePath);
        value.Health = request.Health!.Value; value.Strength = request.Strength!.Value;
        value.Toughness = request.Toughness!.Value; value.Reflexes = request.Reflexes!.Value;
        value.Agility = request.Agility!.Value; value.WeaponName = request.WeaponName!.Trim();
        value.WeaponDamage = request.WeaponDamage!.Value; value.WeaponAccuracy = request.WeaponAccuracy!.Value;
        if (request.ArmorZones is null) { value.Armor = request.Armor!.Value; value.ArmorCoverage = request.ArmorCoverage!; }
        value.ArmorZones = request.ArmorZones is not null ? NpcZoneArmor.Write(request.ArmorZones) :
            NpcZoneArmor.Write(NpcZoneArmor.FromLegacy(value.Armor, value.ArmorCoverage));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminNpcResponse ToResponse(NpcTemplate value) => ToResponse(value, value.LootRows);
    private static AdminNpcResponse ToResponse(NpcTemplate value, IEnumerable<NpcTemplateLootRow> rows) => new(value.Id, "template", value.Category,
        value.Name, value.Description, value.IsActive, value.PortraitPath, value.FigurePath,
        value.Health, value.Strength, value.Toughness, value.Reflexes, value.Agility,
        value.WeaponName, value.WeaponDamage, value.WeaponAccuracy, value.Armor, value.ArmorCoverage,
        NpcZoneArmor.Read(value.ArmorZones), rows.Select(row => new AdminHuntingLootRowResponse(row.ItemId,
            row.Item?.Name ?? string.Empty, row.ChancePercent, row.MinimumQuantity, row.MaximumQuantity)).ToList());

    private static AdminNpcResponse ToResponse(HuntingTarget value) => ToResponse(value, value.LootRows);
    private static AdminNpcResponse ToResponse(HuntingTarget value, IEnumerable<HuntingLootRow> rows) => new(value.Id, "hunting", value.Category,
        value.Name, value.Description, value.IsActive, value.PortraitPath, value.FigurePath,
        value.Health, value.Strength, value.Toughness, value.Reflexes, value.Agility,
        value.WeaponName, value.WeaponDamage, value.WeaponAccuracy, value.Armor, value.ArmorCoverage,
        NpcZoneArmor.Read(value.ArmorZones), rows.Select(row => new AdminHuntingLootRowResponse(row.ItemId,
            row.Item?.Name ?? string.Empty, row.ChancePercent, row.MinimumQuantity, row.MaximumQuantity)).ToList());
}
