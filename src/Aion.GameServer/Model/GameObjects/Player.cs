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

	public long RecoverableExp { get; init; }

	public int Dp { get; init; }

	public long ReposeEnergy { get; init; }

	public bool IsOnline { get; set; }

	public DateTime? LastOnline { get; set; }

	public int NpcExpands { get; init; }

	public int QuestExpands { get; init; }

	public int ItemExpands { get; init; }

	public int WarehouseNpcExpands { get; init; }

	public int WarehouseBonusExpands { get; init; }

	public int TitleId { get; init; }

	public int BonusTitleId { get; init; }

	public WorldPosition Position { get; init; }

	public IReadOnlyList<InventoryItem> InventoryItems { get; set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<InventoryItem> WarehouseItems { get; set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<InventoryItem> AccountWarehouseItems { get; set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<PlayerSkill> Skills { get; set; } = Array.Empty<PlayerSkill>();

	public IReadOnlyDictionary<int, long> SkillCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, PlayerItemCooldown> ItemCooldowns { get; set; } = new Dictionary<int, PlayerItemCooldown>();

	public IReadOnlyList<PlayerQuestState> Quests { get; set; } = Array.Empty<PlayerQuestState>();

	public IReadOnlyList<PlayerTitle> Titles { get; set; } = Array.Empty<PlayerTitle>();

	public IReadOnlyList<PlayerMotion> Motions { get; set; } = Array.Empty<PlayerMotion>();

	public IReadOnlyList<PlayerEmotion> Emotions { get; set; } = Array.Empty<PlayerEmotion>();

	public IReadOnlyList<int> Recipes { get; set; } = Array.Empty<int>();

	public IReadOnlyList<PlayerMacro> Macros { get; set; } = Array.Empty<PlayerMacro>();

	public IReadOnlyList<PlayerMail> Mailbox { get; set; } = Array.Empty<PlayerMail>();

	public PlayerBrokerSettlementSummary BrokerSettlements { get; set; } = PlayerBrokerSettlementSummary.Empty;

	public IReadOnlyList<PlayerHouse> Houses { get; set; } = Array.Empty<PlayerHouse>();

	public IReadOnlyDictionary<int, long> CraftCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, PlayerPortalCooldown> PortalCooldowns { get; set; } = new Dictionary<int, PlayerPortalCooldown>();

	public PlayerLifeStats? LifeStats { get; set; }

	public IReadOnlyList<PlayerFriend> Friends { get; set; } = Array.Empty<PlayerFriend>();

	public IReadOnlyList<PlayerBlockedUser> BlockedUsers { get; set; } = Array.Empty<PlayerBlockedUser>();

	public PlayerAbyssRank AbyssRank { get; set; } = PlayerAbyssRank.Default();

	public PlayerSettings Settings { get; set; } = new();

	public PlayerBindPoint? BindPoint { get; set; }
}
