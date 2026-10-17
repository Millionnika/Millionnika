using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Inventory;
using Millionnika.Api.Resources;

namespace Millionnika.Api.Hunting;

public sealed class HuntingService(
    GameDbContext db,
    ICombatCalculator combat,
    IProgressionService progression,
    IResourceRecoveryService recovery,
    IOptions<HuntingOptions> options) : IHuntingService
{
    private readonly HuntingOptions settings = options.Value;

    public async Task<IReadOnlyList<HuntingLocationResponse>> GetCatalogAsync(Guid userId, CancellationToken token)
    {
        var character = await db.Characters.AsNoTracking().SingleAsync(value => value.UserId == userId, token);
        var locations = await db.HuntingLocations.AsNoTracking().Where(value => value.IsActive)
            .Include(value => value.Targets.Where(target => target.IsActive))
            .OrderBy(value => value.MinimumHuntingLevel).ThenBy(value => value.Id).ToListAsync(token);
        return locations.Select(location => new HuntingLocationResponse(location.Id, location.Name, location.Description,
            location.MinimumHuntingLevel, character.HuntingLevel >= location.MinimumHuntingLevel,
            location.Targets.OrderBy(target => target.MinimumHuntingLevel).ThenBy(target => target.Id)
                .Select(target => TargetResponse(target, character.HuntingLevel, location.MinimumHuntingLevel)).ToList())).ToList();
    }

    public async Task<HuntFightResponse?> GetCurrentFightAsync(Guid userId, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var character = await LockCharacterAsync(userId, token);
        var fight = await db.HuntFights.FromSqlInterpolated($"SELECT * FROM hunt_fights WHERE \"CharacterId\" = {character.Id} ORDER BY \"StartedAtUtc\" DESC LIMIT 1 FOR UPDATE")
            .SingleOrDefaultAsync(token);
        if (fight is null) return null;
        var snapshot = ReadSnapshot(fight);
        var state = ReadState(fight);
        if (recovery.UtcNow >= fight.ExpiresAtUtc)
            await FinalizeAsync(character, fight, snapshot, state, "timeout", Guid.NewGuid(), token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return BuildResponse(character, fight, snapshot, state, Guid.Empty, false,
            fight.Status == "active" ? "Бой продолжается." : ResultMessage(fight.Status));
    }

    public async Task<HuntingActionResult> StartAsync(Guid userId, StartHuntRequest request, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var character = await LockCharacterAsync(userId, token);
        var signature = $"start:{request.TargetId:N}";
        var replay = await ReplayAsync(character, request.RequestId, "start", signature, token);
        if (replay is not null) return await CommitResultAsync(transaction, replay, token);

        var active = await db.HuntFights.AnyAsync(value => value.CharacterId == character.Id && value.Status == "active", token);
        var target = await db.HuntingTargets.Include(value => value.Location)
            .SingleOrDefaultAsync(value => value.Id == request.TargetId && value.IsActive && value.Location.IsActive, token);
        recovery.Apply(character);
        if (active) return await RejectAsync(transaction, character, request.RequestId, "start", signature,
            "Сначала завершите текущий бой.", 409, token);
        if (target is null) return await RejectAsync(transaction, character, request.RequestId, "start", signature,
            "Эта цель сейчас недоступна.", 409, token);
        var requiredLevel = Math.Max(target.MinimumHuntingLevel, target.Location.MinimumHuntingLevel);
        if (character.HuntingLevel < requiredLevel) return await RejectAsync(transaction, character, request.RequestId,
            "start", signature, $"Нужен уровень охоты {requiredLevel}.", 409, token);
        if (character.Energy < target.EnergyCost) return await RejectAsync(transaction, character, request.RequestId,
            "start", signature, $"Не хватает энергии: нужно ещё {target.EnergyCost - character.Energy}.", 409, token);
        if (character.Health <= 0) return await RejectAsync(transaction, character, request.RequestId, "start", signature,
            "Для охоты нужно восстановить здоровье.", 409, token);
        var maximumCreditReward = checked((long)Math.Floor(target.RewardCredits * (1 + settings.MaximumCreditBonus)));
        if (character.Credits > GameNumericLimits.MaxEconomyValue - maximumCreditReward ||
            character.Experience > GameNumericLimits.MaxEconomyValue - target.RewardExperience ||
            character.HuntingExperience > GameNumericLimits.MaxEconomyValue - target.RewardHuntingExperience)
            return await RejectAsync(transaction, character, request.RequestId, "start", signature,
                "Награда превысит допустимый числовой предел. Бой не начат.", 409, token);

        var equipment = await db.EquipmentInstances.Include(value => value.Item)
            .Where(value => value.CharacterId == character.Id && value.EquippedSlot != null).ToListAsync(token);
        var weapons = new List<HuntWeaponState>();
        foreach (var instance in equipment.Where(value => value.EquippedSlot is EquipmentSlots.Primary or EquipmentSlots.Secondary or EquipmentSlots.Melee)
                     .OrderBy(value => Array.IndexOf(EquipmentSlots.WeaponPermanent, value.EquippedSlot!)))
        {
            var item = instance.Item;
            if (item.ClipSize == 0)
            {
                weapons.Add(new(instance.ItemId, Guid.Empty, instance.EquippedSlot!, item.Name, instance.Damage,
                    instance.Accuracy, string.Empty, 0, 0, 0, 0, 0));
                continue;
            }
            var ammo = await db.InventoryItems.Include(value => value.Item).SingleOrDefaultAsync(value =>
                value.CharacterId == character.Id && value.Item.Type == ItemTypes.Ammunition && value.Item.Caliber == item.Caliber, token);
            var reserved = Math.Min(ammo?.Quantity ?? 0, checked(item.ClipSize * (1 + settings.MaximumSpareClips)));
            if (ammo is not null && reserved > 0)
            {
                ammo.Quantity -= reserved;
                if (ammo.Quantity == 0) db.InventoryItems.Remove(ammo);
            }
            var loaded = Math.Min(item.ClipSize, reserved);
            weapons.Add(new(instance.ItemId, ammo?.ItemId ?? Guid.Empty, instance.EquippedSlot!, item.Name, instance.Damage,
                instance.Accuracy, item.Caliber, item.ClipSize, item.RateOfFireMin, item.RateOfFireMax, loaded, reserved - loaded));
        }
        if (weapons.Count == 0)
            return await RejectAsync(transaction, character, request.RequestId, "start", signature,
                "Перед охотой экипируйте оружие.", 409, token, errorCode: "weapon_not_equipped");
        if (!weapons.Any(value => value.ClipSize == 0 || value.AmmoInClip > 0))
        {
            var firearm = weapons.First(value => value.ClipSize > 0);
            var ammunitionName = await db.Items.AsNoTracking()
                .Where(value => value.Type == ItemTypes.Ammunition && value.Caliber == firearm.Caliber)
                .OrderByDescending(value => value.IsActive).ThenBy(value => value.Name)
                .Select(value => value.Name).FirstOrDefaultAsync(token)
                ?? $"Боеприпасы калибра {firearm.Caliber}";
            return await RejectAsync(transaction, character, request.RequestId, "start", signature,
                $"Для оружия «{firearm.Name}» нет подходящих патронов: «{ammunitionName}». Они берутся из инвентаря автоматически.",
                409, token, "compatible_ammo_missing", firearm.Name, ammunitionName);
        }

        var armor = equipment.Where(value => value.Item.Type == ItemTypes.Armor)
            .Select(value => new CombatArmor(value.Armor, ReadCoverage(value.Item.ArmorCoverage))).ToList();
        var playerEquipment = equipment.Where(value => value.Item.Type is ItemTypes.Weapon or ItemTypes.Armor)
            .OrderBy(value => Array.IndexOf(EquipmentSlots.All, value.EquippedSlot!))
            .Select(value => new HuntEquipmentSnapshot(value.EquippedSlot!, value.Item.Type, value.Item.Name,
                value.Damage, value.Accuracy, value.Armor,
                value.Item.Type == ItemTypes.Armor ? ReadCoverage(value.Item.ArmorCoverage) : []))
            .ToList();
        var playerName = await db.Users.AsNoTracking().Where(value => value.Id == character.UserId)
            .Select(value => value.Username).SingleAsync(token);
        var robotWeapon = new HuntWeaponState(Guid.Empty, Guid.Empty, "robot", target.WeaponName, target.WeaponDamage,
            target.WeaponAccuracy, "robot", target.ClipSize, target.RateOfFireMin, target.RateOfFireMax,
            target.ClipSize, checked(target.ClipSize * target.SpareClips));
        var snapshot = new HuntSnapshot(target.Id, target.Name, target.EnergyCost, requiredLevel, character.HuntingLevel,
            character.MaxHealth, Stats(character), armor, target.Health,
            new(target.Strength, target.Reflexes, target.Toughness, target.Agility), robotWeapon.ToCombat(),
            target.Armor > 0 ? [new CombatArmor(target.Armor, ReadCoverage(target.ArmorCoverage))] : [],
            target.RewardCredits, target.RewardExperience, target.RewardHuntingExperience, playerName, playerEquipment);
        var state = new HuntState(weapons, robotWeapon);
        var now = recovery.UtcNow;
        character.Energy -= target.EnergyCost;
        var fight = new HuntFight
        {
            Id = Guid.NewGuid(), CharacterId = character.Id, TargetId = target.Id, PlayerHealth = character.Health,
            RobotHealth = target.Health, SnapshotJson = JsonSerializer.Serialize(snapshot, CombatJson.Options),
            StateJson = JsonSerializer.Serialize(state, CombatJson.Options), LogJson = "[]", StartedAtUtc = now,
            ExpiresAtUtc = now.AddSeconds(settings.FightDurationSeconds)
        };
        db.HuntFights.Add(fight);
        var response = BuildResponse(character, fight, snapshot, state, request.RequestId, false,
            $"Охота началась: {target.Name}.");
        AddReceipt(userId, request.RequestId, "start", signature, response);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(response, 200);
    }

    public async Task<HuntingActionResult> TurnAsync(Guid userId, HuntTurnRequest request, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var character = await LockCharacterAsync(userId, token);
        var signature = $"turn:{request.Action}:{request.Slot}";
        var replay = await ReplayAsync(character, request.RequestId, "turn", signature, token);
        if (replay is not null) return await CommitResultAsync(transaction, replay, token);
        var fight = await db.HuntFights.FromSqlInterpolated($"SELECT * FROM hunt_fights WHERE \"CharacterId\" = {character.Id} AND \"Status\" = 'active' FOR UPDATE")
            .SingleOrDefaultAsync(token);
        if (fight is null) return await RejectAsync(transaction, character, request.RequestId, "turn", signature,
            "Активный бой не найден.", 409, token);
        var snapshot = ReadSnapshot(fight);
        var state = ReadState(fight);
        var now = recovery.UtcNow;
        if (now >= fight.ExpiresAtUtc)
        {
            await FinalizeAsync(character, fight, snapshot, state, "timeout", request.RequestId, token);
            var timeout = BuildResponse(character, fight, snapshot, state, request.RequestId, false, ResultMessage(fight.Status));
            AddReceipt(userId, request.RequestId, "turn", signature, timeout);
            await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return new(timeout, 200);
        }
        var next = fight.LastActionAtUtc?.AddSeconds(settings.ActionCooldownSeconds);
        if (next > now)
        {
            var early = BuildResponse(character, fight, snapshot, state, request.RequestId, false,
                $"Следующее действие доступно через {Math.Max(1, (int)Math.Ceiling((next.Value - now).TotalSeconds))} сек.");
            await transaction.RollbackAsync(token);
            return new(early, 429);
        }

        var weaponIndex = state.Weapons.FindIndex(value => value.Slot == request.Slot);
        if (weaponIndex < 0) return await RejectAsync(transaction, character, request.RequestId, "turn", signature,
            "В этом слоте нет оружия.", 409, token);
        var weapon = state.Weapons[weaponIndex];
        var messages = ReadLog(fight);
        if (request.Action == "reload")
        {
            if (weapon.ClipSize == 0)
                return await RejectAsync(transaction, character, request.RequestId, "turn", signature,
                    "Это оружие не требует перезарядки.", 409, token);
            if (weapon.AmmoInClip >= weapon.ClipSize)
                return await RejectAsync(transaction, character, request.RequestId, "turn", signature,
                    "Магазин уже полный. Можно выбрать другое оружие.", 409, token);
            if (weapon.ReserveAmmo == 0)
                return await RejectAsync(transaction, character, request.RequestId, "turn", signature,
                    "Запас патронов пуст. Можно выбрать другое оружие.", 409, token);
            var amount = Math.Min(weapon.ClipSize - weapon.AmmoInClip, weapon.ReserveAmmo);
            weapon = weapon with { AmmoInClip = weapon.AmmoInClip + amount, ReserveAmmo = weapon.ReserveAmmo - amount };
            messages.Add($"{weapon.Name}: перезарядка, в магазин добавлено {amount} патронов.");
        }
        else
        {
            if (weapon.ClipSize > 0 && weapon.AmmoInClip <= 0)
                return await RejectAsync(transaction, character, request.RequestId, "turn", signature,
                    "Магазин пуст — перезарядите оружие.", 409, token);
            var hit = combat.Attack(snapshot.PlayerStats, snapshot.RobotStats, weapon.ToCombat(), snapshot.RobotArmor);
            weapon = weapon with { AmmoInClip = Math.Max(0, weapon.AmmoInClip - hit.Bullets) };
            fight.RobotHealth = Math.Max(0, fight.RobotHealth - hit.Damage);
            messages.Add(hit.Message);
        }
        state.Weapons[weaponIndex] = weapon;
        fight.Turn++;
        fight.LastActionAtUtc = now;

        if (fight.RobotHealth > 0)
        {
            var robot = state.RobotWeapon;
            if (robot.ClipSize > 0 && robot.AmmoInClip == 0)
            {
                var amount = Math.Min(robot.ClipSize, robot.ReserveAmmo);
                robot = robot with { AmmoInClip = amount, ReserveAmmo = robot.ReserveAmmo - amount };
                messages.Add(amount > 0 ? $"{robot.Name}: перезарядка." : $"{robot.Name}: боезапас закончился.");
            }
            else
            {
                var hit = combat.Attack(snapshot.RobotStats, snapshot.PlayerStats, robot.ToCombat(), snapshot.PlayerArmor);
                robot = robot with { AmmoInClip = Math.Max(0, robot.AmmoInClip - hit.Bullets) };
                fight.PlayerHealth = Math.Max(0, fight.PlayerHealth - hit.Damage);
                messages.Add(hit.Message);
            }
            state = state with { RobotWeapon = robot };
        }
        fight.LogJson = JsonSerializer.Serialize(messages.TakeLast(30), CombatJson.Options);
        fight.StateJson = JsonSerializer.Serialize(state, CombatJson.Options);
        var finalStatus = fight.RobotHealth == 0 ? "won" : fight.PlayerHealth == 0 ? "lost" :
            fight.Turn >= settings.MaximumTurns ? "stalemate" : null;
        if (finalStatus is not null)
            await FinalizeAsync(character, fight, snapshot, state, finalStatus, request.RequestId, token);

        var response = BuildResponse(character, fight, snapshot, state, request.RequestId, false,
            finalStatus is null ? "Ход выполнен." : ResultMessage(fight.Status));
        AddReceipt(userId, request.RequestId, "turn", signature, response);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(response, 200);
    }

    private async Task FinalizeAsync(GameCharacter character, HuntFight fight, HuntSnapshot snapshot,
        HuntState state, string status, Guid requestId, CancellationToken token)
    {
        if (fight.FinalizationApplied) return;
        fight.Status = status;
        fight.CompletedAtUtc = recovery.UtcNow;
        long credits = 0, experience = 0, huntingExperience = 0;
        if (status == "won")
        {
            var bonus = Math.Min(Math.Max(0, snapshot.StartingHuntingLevel - snapshot.MinimumHuntingLevel) * settings.CreditBonusPerExtraLevel,
                settings.MaximumCreditBonus);
            credits = checked((long)Math.Floor(snapshot.RewardCredits * (1 + bonus)));
            experience = snapshot.RewardExperience;
            huntingExperience = snapshot.RewardHuntingExperience;
            if (character.Credits > GameNumericLimits.MaxEconomyValue - credits ||
                character.Experience > GameNumericLimits.MaxEconomyValue - experience ||
                character.HuntingExperience > GameNumericLimits.MaxEconomyValue - huntingExperience)
                throw new InvalidOperationException("Hunt reward exceeds configured numeric limits.");
            character.Credits += credits;
            character.Experience += experience;
            character.HuntingExperience += huntingExperience;
            progression.Apply(character);
            character.Health = fight.PlayerHealth;
        }
        else
        {
            var floor = (int)Math.Ceiling(character.MaxHealth * settings.DefeatHealthFloor);
            character.Health = Math.Max(fight.PlayerHealth, floor);
            fight.PlayerHealth = character.Health;
        }
        foreach (var group in state.Weapons.Where(value => value.AmmoItemId != Guid.Empty)
                     .GroupBy(value => value.AmmoItemId))
        {
            var amount = group.Sum(value => value.AmmoInClip + value.ReserveAmmo);
            if (amount == 0) continue;
            var inventory = await db.InventoryItems.SingleOrDefaultAsync(value => value.CharacterId == character.Id && value.ItemId == group.Key, token);
            if (inventory is null) db.InventoryItems.Add(new PlayerInventoryItem { Id = Guid.NewGuid(), CharacterId = character.Id, ItemId = group.Key, Quantity = amount });
            else inventory.Quantity = checked(inventory.Quantity + amount);
        }
        fight.FinalizationApplied = true;
        db.ActionLogs.Add(new ActionLog
        {
            Id = Guid.NewGuid(), UserId = character.UserId, RequestId = requestId == Guid.Empty ? Guid.NewGuid() : requestId,
            ActionType = "hunt_result", RequestSignature = $"hunt:{fight.Id:N}:{status}", Succeeded = status == "won",
            ErrorCode = status == "won" ? null : status, Message = ResultMessage(status), EnergySpent = snapshot.EnergyCost,
            CreditsAwarded = credits, ExperienceAwarded = experience, HuntingExperienceAwarded = huntingExperience,
            CreatedAtUtc = recovery.UtcNow
        });
    }

    private async Task<HuntingActionResult?> ReplayAsync(GameCharacter character, Guid requestId, string action, string signature, CancellationToken token)
    {
        var receipt = await db.HuntingRequests.AsNoTracking().SingleOrDefaultAsync(value => value.UserId == character.UserId && value.RequestId == requestId, token);
        if (receipt is null)
        {
            if (!await db.ActionLogs.AsNoTracking().AnyAsync(value => value.UserId == character.UserId && value.RequestId == requestId, token))
                return null;
            return new(EmptyResponse(character, requestId, "Этот requestId уже использован для другого действия или других параметров."), 409);
        }
        if (receipt.ActionType != action || receipt.Signature != signature)
        {
            var response = JsonSerializer.Deserialize<HuntFightResponse>(receipt.ResponseJson, CombatJson.Options)! with
            { Replayed = false, Message = "Этот requestId уже использован с другими параметрами." };
            return new(response, 409);
        }
        return new(JsonSerializer.Deserialize<HuntFightResponse>(receipt.ResponseJson, CombatJson.Options)! with { Replayed = true }, 200);
    }

    private async Task<HuntingActionResult> RejectAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        GameCharacter character, Guid requestId, string action, string signature, string message, int code, CancellationToken token,
        string? errorCode = null, string? requiredAmmoWeapon = null, string? requiredAmmoName = null)
    {
        var response = EmptyResponse(character, requestId, message) with
        {
            ErrorCode = errorCode,
            RequiredAmmoWeapon = requiredAmmoWeapon,
            RequiredAmmoName = requiredAmmoName
        };
        AddReceipt(character.UserId, requestId, action, signature, response);
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return new(response, code);
    }

    private void AddReceipt(Guid userId, Guid requestId, string action, string signature, HuntFightResponse response) =>
        db.HuntingRequests.Add(new HuntingRequest { Id = Guid.NewGuid(), UserId = userId, RequestId = requestId,
            ActionType = action, Signature = signature, ResponseJson = JsonSerializer.Serialize(response, CombatJson.Options), CreatedAtUtc = recovery.UtcNow });

    private async Task<HuntingActionResult> CommitResultAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        HuntingActionResult result, CancellationToken token) { await transaction.CommitAsync(token); return result; }

    private static HuntingTargetResponse TargetResponse(HuntingTarget target, int level, int locationLevel)
    {
        var needed = Math.Max(target.MinimumHuntingLevel, locationLevel);
        return new(target.Id, target.Name, target.Description, needed, target.EnergyCost, target.Health,
            target.RewardCredits, target.RewardExperience, target.RewardHuntingExperience, level >= needed,
            level >= needed ? null : $"Откроется на уровне охоты {needed}");
    }

    private HuntFightResponse BuildResponse(GameCharacter character, HuntFight fight, HuntSnapshot snapshot, HuntState state,
        Guid requestId, bool replayed, string message)
    {
        var log = ReadLog(fight);
        var actionLog = fight.Status == "won" ? snapshot : null;
        var credits = actionLog is null ? 0 : (long)Math.Floor(snapshot.RewardCredits *
            (1 + Math.Min(Math.Max(0, snapshot.StartingHuntingLevel - snapshot.MinimumHuntingLevel) * settings.CreditBonusPerExtraLevel, settings.MaximumCreditBonus)));
        var participantList = new List<HuntParticipantResponse>
        {
            new("player", snapshot.PlayerName, "player", fight.PlayerHealth, snapshot.PlayerMaximumHealth),
            new("opponent", snapshot.TargetName, "robot", fight.RobotHealth, snapshot.RobotMaximumHealth)
        };
        var playerEquipment = (snapshot.PlayerEquipment ??
            state.Weapons.Select(value => new HuntEquipmentSnapshot(value.Slot, ItemTypes.Weapon, value.Name,
                value.Damage, value.Accuracy, 0, [])).ToList())
            .Select(EquipmentResponse).ToList();
        var robotEquipment = new List<HuntEquipmentResponse>
        {
            new("weapon", ItemTypes.Weapon, snapshot.RobotWeapon.Name, snapshot.RobotWeapon.Damage,
                snapshot.RobotWeapon.Accuracy, 0, new Dictionary<string, decimal>())
        };
        var robotProtection = snapshot.RobotArmor
            .Select(value => new HuntProtectionResponse("natural", value.Armor, value.Coverage)).ToList();
        return new(fight.Id, requestId, replayed, fight.Status, fight.Turn, settings.MaximumTurns,
            fight.PlayerHealth, snapshot.PlayerMaximumHealth, fight.RobotHealth, snapshot.RobotMaximumHealth,
            fight.ExpiresAtUtc, fight.Status == "active" ? fight.LastActionAtUtc?.AddSeconds(settings.ActionCooldownSeconds) : null,
            message, log, state.Weapons.Select(value => new HuntWeaponResponse(value.Slot, value.Name, value.AmmoInClip, value.ReserveAmmo, value.ClipSize)).ToList(),
            credits, actionLog?.RewardExperience ?? 0, actionLog?.RewardHuntingExperience ?? 0,
            character.HuntingLevel, character.Energy, fight.Status == "won" && character.HuntingLevel > progression.GetHunting(character.HuntingExperience - snapshot.RewardHuntingExperience).Level,
            Participants: participantList, PlayerEquipment: playerEquipment, RobotEquipment: robotEquipment,
            RobotProtection: robotProtection);
    }

    private HuntFightResponse EmptyResponse(GameCharacter character, Guid requestId, string message) =>
        new(Guid.Empty, requestId, false, "rejected", 0, settings.MaximumTurns, character.Health, character.MaxHealth,
            0, 0, recovery.UtcNow, null, message, [], [], 0, 0, 0, character.HuntingLevel, character.Energy, false);
    private static HuntSnapshot ReadSnapshot(HuntFight fight) => JsonSerializer.Deserialize<HuntSnapshot>(fight.SnapshotJson, CombatJson.Options)!;
    private static HuntState ReadState(HuntFight fight) => JsonSerializer.Deserialize<HuntState>(fight.StateJson, CombatJson.Options)!;
    private static List<string> ReadLog(HuntFight fight) => JsonSerializer.Deserialize<List<string>>(fight.LogJson, CombatJson.Options) ?? [];
    private static Dictionary<string, decimal> ReadCoverage(string json) => JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, CombatJson.Options) ?? [];
    private static HuntEquipmentResponse EquipmentResponse(HuntEquipmentSnapshot value) =>
        new(value.Slot, value.Type, value.Name, value.Damage, value.Accuracy, value.Armor, value.Coverage);
    private static CombatStats Stats(GameCharacter value) => new(value.Strength, value.Reflexes, value.Toughness, value.Agility);
    private static string ResultMessage(string status) => status switch { "won" => "Робот побеждён.", "lost" => "Персонаж проиграл бой.", "stalemate" => "Лимит ходов исчерпан.", _ => "Время боя истекло." };
    private Task<GameCharacter> LockCharacterAsync(Guid userId, CancellationToken token) => db.Characters
        .FromSqlInterpolated($"SELECT * FROM characters WHERE \"UserId\" = {userId} FOR UPDATE").SingleAsync(token);
}
