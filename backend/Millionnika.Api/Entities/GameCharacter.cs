namespace Millionnika.Api.Entities;

public sealed class GameCharacter
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Level { get; set; }
    public long Credits { get; set; }
    public int Energy { get; set; }
    public int MaxEnergy { get; set; }
    public DateTimeOffset LastEnergyRecoveryAtUtc { get; set; }
    public int Nerve { get; set; }
    public int MaxNerve { get; set; }
    public DateTimeOffset LastNerveRecoveryAtUtc { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Strength { get; set; }
    public int Reflexes { get; set; }
    public int Toughness { get; set; }
    public int Agility { get; set; }
    public long Experience { get; set; }
    public int HackingLevel { get; set; } = 1;
    public long HackingExperience { get; set; }
    public int HuntingLevel { get; set; } = 1;
    public long HuntingExperience { get; set; }
    public ICollection<PlayerInventoryItem> Inventory { get; set; } = [];
    public ICollection<EquipmentInstance> Equipment { get; set; } = [];
    public ICollection<HuntFight> HuntFights { get; set; } = [];
}
