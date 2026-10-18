using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Inventory;

namespace Millionnika.Api.Admin;

public sealed class NpcAdminService(GameDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminNpcResponse>> GetAllAsync(CancellationToken token)
    {
        var templates = await dbContext.NpcTemplates.AsNoTracking().ToListAsync(token);
        var huntingRobots = await dbContext.HuntingTargets.AsNoTracking().ToListAsync(token);
        return templates.Select(ToResponse).Concat(huntingRobots.Select(ToResponse))
            .OrderBy(value => value.Category).ThenBy(value => value.Name).ThenBy(value => value.Id).ToList();
    }

    public async Task<AdminMutationResult> CreateAsync(Guid adminId, SaveNpcRequest request, CancellationToken token)
    {
        if (!IsValid(request)) return AdminMutationResult.Failure("Проверьте категорию, боевые поля, изображения и причину.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        var value = new NpcTemplate { Id = Guid.NewGuid() };
        Apply(value, request);
        dbContext.NpcTemplates.Add(value);
        AddAudit(adminId, "npc_create", value.Id, new { }, ToResponse(value), request.Reason!);
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return AdminMutationResult.Success("Шаблон НПЦ создан.");
    }

    public async Task<AdminMutationResult> UpdateAsync(Guid adminId, Guid id, SaveNpcRequest request, CancellationToken token)
    {
        if (!IsValid(request)) return AdminMutationResult.Failure("Проверьте категорию, боевые поля, изображения и причину.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        var value = await dbContext.NpcTemplates.FromSqlInterpolated($"SELECT * FROM npc_templates WHERE \"Id\"={id} FOR UPDATE")
            .SingleOrDefaultAsync(token);
        if (value is not null)
        {
            var old = ToResponse(value);
            Apply(value, request);
            AddAudit(adminId, "npc_update", id, old, ToResponse(value), request.Reason!);
        }
        else
        {
            // Existing hunting robots are their own canonical NPC records, not copied templates.
            var robot = await dbContext.HuntingTargets.FromSqlInterpolated($"SELECT * FROM hunting_targets WHERE \"Id\"={id} FOR UPDATE")
                .SingleOrDefaultAsync(token);
            if (robot is null) return AdminMutationResult.Failure("НПЦ не найден.");
            if (request.Category != robot.Category) return AdminMutationResult.Failure("Категорию НПЦ, связанного с охотой, менять нельзя.");
            var old = ToResponse(robot);
            Apply(robot, request);
            AddAudit(adminId, "npc_hunting_robot_update", id, old, ToResponse(robot), request.Reason!);
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
            !AdminRules.IsReasonValid(value.Reason) || !ValidImagePath(value.PortraitPath) || !ValidImagePath(value.FigurePath))
            return false;
        var hasAnyCombat = value.Health.HasValue || value.Strength.HasValue || value.Toughness.HasValue ||
            value.Reflexes.HasValue || value.Agility.HasValue || !string.IsNullOrWhiteSpace(value.WeaponName) ||
            value.WeaponDamage.HasValue || value.WeaponAccuracy.HasValue || value.Armor.HasValue ||
            !string.IsNullOrWhiteSpace(value.ArmorCoverage);
        if (value.Category == "peaceful" && !hasAnyCombat) return true;
        return value.Health is > 0 and <= 1_000_000 && value.Strength is > 0 and <= GameNumericLimits.MaxStatValue &&
            value.Toughness is > 0 and <= GameNumericLimits.MaxStatValue &&
            value.Reflexes is > 0 and <= GameNumericLimits.MaxStatValue &&
            value.Agility is > 0 and <= GameNumericLimits.MaxStatValue &&
            !string.IsNullOrWhiteSpace(value.WeaponName) && value.WeaponName.Trim().Length <= 120 &&
            value.WeaponDamage is >= 1 and <= 1000 && value.WeaponAccuracy is >= 0 and <= 100 &&
            value.Armor is >= 0 and <= 100 &&
            (value.Armor == 0 ? value.ArmorCoverage == "{}" : EquipmentRules.IsCoverageValid(value.ArmorCoverage));
    }

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
        value.Armor = request.Armor; value.ArmorCoverage = Clean(request.ArmorCoverage);
    }

    private static void Apply(HuntingTarget value, SaveNpcRequest request)
    {
        value.Name = request.Name!.Trim(); value.Description = request.Description!.Trim(); value.IsActive = request.IsActive;
        value.PortraitPath = Clean(request.PortraitPath); value.FigurePath = Clean(request.FigurePath);
        value.Health = request.Health!.Value; value.Strength = request.Strength!.Value;
        value.Toughness = request.Toughness!.Value; value.Reflexes = request.Reflexes!.Value;
        value.Agility = request.Agility!.Value; value.WeaponName = request.WeaponName!.Trim();
        value.WeaponDamage = request.WeaponDamage!.Value; value.WeaponAccuracy = request.WeaponAccuracy!.Value;
        value.Armor = request.Armor!.Value; value.ArmorCoverage = request.ArmorCoverage!;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminNpcResponse ToResponse(NpcTemplate value) => new(value.Id, "template", value.Category,
        value.Name, value.Description, value.IsActive, value.PortraitPath, value.FigurePath,
        value.Health, value.Strength, value.Toughness, value.Reflexes, value.Agility,
        value.WeaponName, value.WeaponDamage, value.WeaponAccuracy, value.Armor, value.ArmorCoverage);

    private static AdminNpcResponse ToResponse(HuntingTarget value) => new(value.Id, "hunting", value.Category,
        value.Name, value.Description, value.IsActive, value.PortraitPath, value.FigurePath,
        value.Health, value.Strength, value.Toughness, value.Reflexes, value.Agility,
        value.WeaponName, value.WeaponDamage, value.WeaponAccuracy, value.Armor, value.ArmorCoverage);
}
