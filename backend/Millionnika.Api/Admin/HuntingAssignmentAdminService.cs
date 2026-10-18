using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;

namespace Millionnika.Api.Admin;

public sealed class HuntingAssignmentAdminService(GameDbContext db)
{
    public async Task<IReadOnlyList<HuntingAssignmentResponse>> GetAllAsync(CancellationToken token) =>
        await db.HuntingLocationTargets.AsNoTracking()
            .OrderBy(value => value.LocationId).ThenBy(value => value.Target.Name).ThenBy(value => value.TargetId)
            .Select(value => new HuntingAssignmentResponse(value.LocationId, value.TargetId,
                value.Target.Name, value.Target.Category, value.Target.IsActive)).ToListAsync(token);

    public async Task<AdminMutationResult> AssignAsync(Guid adminId, Guid locationId,
        AssignHuntingNpcRequest request, CancellationToken token)
    {
        if (locationId == Guid.Empty || request.NpcId == Guid.Empty || !AdminRules.IsReasonValid(request.Reason))
            return AdminMutationResult.Failure("Выберите локацию и НПЦ, укажите причину.");

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var location = await db.HuntingLocations.FromSqlInterpolated(
            $"SELECT * FROM hunting_locations WHERE \"Id\"={locationId} FOR UPDATE").SingleOrDefaultAsync(token);
        if (location is null) return AdminMutationResult.Failure("Локация не найдена.");

        var target = await db.HuntingTargets.FromSqlInterpolated(
            $"SELECT * FROM hunting_targets WHERE \"Id\"={request.NpcId} FOR UPDATE").SingleOrDefaultAsync(token);
        if (target is null)
        {
            var template = await db.NpcTemplates.FromSqlInterpolated(
                $"SELECT * FROM npc_templates WHERE \"Id\"={request.NpcId} FOR UPDATE").SingleOrDefaultAsync(token);
            if (template is null) return AdminMutationResult.Failure("НПЦ не найден.");
            if (template.Category == "peaceful" || template.Health is null || template.Strength is null ||
                template.Toughness is null || template.Reflexes is null || template.Agility is null ||
                template.WeaponDamage is null || template.WeaponAccuracy is null || template.Armor is null ||
                string.IsNullOrWhiteSpace(template.WeaponName) || string.IsNullOrWhiteSpace(template.ArmorCoverage))
                return AdminMutationResult.Failure("В охоту можно назначить только НПЦ с заполненными боевыми характеристиками.");
            if (!IsSetupValid(request.Setup))
                return AdminMutationResult.Failure("Для нового НПЦ укажите допустимую стоимость, уровень, боезапас и награды охоты.");

            var setup = request.Setup!;
            target = new HuntingTarget
            {
                Id = template.Id, Category = template.Category, Name = template.Name,
                Description = template.Description, IsActive = template.IsActive,
                PortraitPath = template.PortraitPath, FigurePath = template.FigurePath,
                Health = template.Health.Value, Strength = template.Strength.Value,
                Toughness = template.Toughness.Value, Reflexes = template.Reflexes.Value,
                Agility = template.Agility.Value, WeaponName = template.WeaponName,
                WeaponDamage = template.WeaponDamage.Value, WeaponAccuracy = template.WeaponAccuracy.Value,
                Armor = template.Armor.Value, ArmorCoverage = template.ArmorCoverage,
                MinimumHuntingLevel = setup.MinimumHuntingLevel, EnergyCost = setup.EnergyCost,
                ClipSize = setup.ClipSize, RateOfFireMin = setup.RateOfFireMin,
                RateOfFireMax = setup.RateOfFireMax, SpareClips = setup.SpareClips,
                RewardCredits = setup.RewardCredits, MinimumCredits = setup.MinimumCredits,
                MaximumCredits = setup.MaximumCredits, RewardExperience = setup.RewardExperience,
                RewardHuntingExperience = setup.RewardHuntingExperience
            };
            db.NpcTemplates.Remove(template);
            db.HuntingTargets.Add(target);
            AddAudit(adminId, "npc_hunting_setup", target.Id.ToString(),
                new { source = "template" }, new { source = "hunting", setup }, request.Reason!);
        }
        else if (request.Setup is not null)
        {
            return AdminMutationResult.Failure("Настройки существующего НПЦ редактируются в разделе «Охота → Роботы».");
        }

        if (await db.HuntingLocationTargets.AnyAsync(value => value.LocationId == locationId && value.TargetId == target.Id, token))
            return AdminMutationResult.Failure("Этот НПЦ уже назначен в выбранную локацию.");
        db.HuntingLocationTargets.Add(new HuntingLocationTarget { LocationId = locationId, TargetId = target.Id });
        AddAudit(adminId, "hunting_npc_assign", $"{locationId:N}{target.Id:N}",
            new { assigned = false }, new { assigned = true, locationId, npcId = target.Id }, request.Reason!);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return AdminMutationResult.Success("НПЦ добавлен в локацию.");
    }

    public async Task<AdminMutationResult> RemoveAsync(Guid adminId, Guid locationId, Guid npcId,
        RemoveHuntingNpcRequest request, CancellationToken token)
    {
        if (locationId == Guid.Empty || npcId == Guid.Empty || !AdminRules.IsReasonValid(request.Reason))
            return AdminMutationResult.Failure("Укажите причину снятия назначения.");
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        // The same location lock serializes assignment and removal in this location.
        var location = await db.HuntingLocations.FromSqlInterpolated(
            $"SELECT * FROM hunting_locations WHERE \"Id\"={locationId} FOR UPDATE").SingleOrDefaultAsync(token);
        if (location is null) return AdminMutationResult.Failure("Локация не найдена.");
        var assignment = await db.HuntingLocationTargets.SingleOrDefaultAsync(
            value => value.LocationId == locationId && value.TargetId == npcId, token);
        if (assignment is null) return AdminMutationResult.Failure("НПЦ уже отсутствует в локации.");
        db.HuntingLocationTargets.Remove(assignment);
        AddAudit(adminId, "hunting_npc_remove", $"{locationId:N}{npcId:N}",
            new { assigned = true, locationId, npcId }, new { assigned = false }, request.Reason!);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return AdminMutationResult.Success("НПЦ убран из локации. Сам НПЦ и начатые бои сохранены.");
    }

    private static bool IsSetupValid(HuntingSetupRequest? value) => value is not null &&
        value.MinimumHuntingLevel is >= 1 and <= 20 && value.EnergyCost > 0 &&
        value.ClipSize is >= 0 and <= 1000 && value.RateOfFireMin >= 0 &&
        value.RateOfFireMax >= value.RateOfFireMin && value.RateOfFireMax <= value.ClipSize &&
        value.SpareClips is >= 0 and <= 3 &&
        value.RewardCredits is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        value.MinimumCredits is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        value.MaximumCredits >= value.MinimumCredits && value.MaximumCredits <= GameNumericLimits.MaxEconomyValue &&
        value.RewardExperience is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        value.RewardHuntingExperience is >= 0 and <= GameNumericLimits.MaxEconomyValue;

    private void AddAudit(Guid adminId, string action, string targetId, object oldValue, object newValue, string reason) =>
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(), AdminUserId = adminId, Action = action,
            TargetType = "hunting_npc", TargetId = targetId,
            OldValue = JsonSerializer.Serialize(oldValue), NewValue = JsonSerializer.Serialize(newValue),
            Reason = reason.Trim(), CreatedAtUtc = DateTimeOffset.UtcNow
        });
}
