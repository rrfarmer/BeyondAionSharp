using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

public sealed class Player
{
	public int ObjectId { get; init; }

	public int AccountId { get; init; }

	public string Name { get; init; } = string.Empty;

	public string PlayerClass { get; init; } = string.Empty;

	public string Race { get; init; } = string.Empty;

	public string Gender { get; init; } = string.Empty;

	public long Exp { get; init; }

	public bool IsOnline { get; set; }

	public DateTime? LastOnline { get; set; }

	public int TitleId { get; init; }

	public WorldPosition Position { get; init; }

	public IReadOnlyList<InventoryItem> InventoryItems { get; set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<PlayerSkill> Skills { get; set; } = Array.Empty<PlayerSkill>();

	public IReadOnlyDictionary<int, long> SkillCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, PlayerItemCooldown> ItemCooldowns { get; set; } = new Dictionary<int, PlayerItemCooldown>();

	public IReadOnlyList<PlayerQuestState> Quests { get; set; } = Array.Empty<PlayerQuestState>();

	public IReadOnlyList<PlayerMotion> Motions { get; set; } = Array.Empty<PlayerMotion>();

	public PlayerSettings Settings { get; set; } = new();
}
