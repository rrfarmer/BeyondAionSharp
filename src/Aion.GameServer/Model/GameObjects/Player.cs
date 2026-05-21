using Aion.GameServer.Model.Account;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

public sealed class Player
{
	public const byte MailboxClosedState = 0;
	public const byte MailboxRegularState = 1;
	public const byte MailboxExpressState = 2;

	public int ObjectId { get; init; }

	public int AccountId { get; init; }

	// Java parity: model/gameobjects/player/PlayerAccount.accessLevel used by AdminService.hasAccess checks.
	public byte AccessLevel { get; set; }

	// Java parity: model/account/PlayerAccount.membership consumed by chat/player-info packets.
	public byte AccountMembership { get; set; }

	// Java parity: model/team/legion/LegionMember data used by chat/player info packets.
	public int LegionId { get; set; }

	public string LegionName { get; set; } = string.Empty;

	public byte LegionEmblemId { get; set; }

	public byte LegionEmblemType { get; set; }

	public byte LegionEmblemColorA { get; set; }

	public byte LegionEmblemColorR { get; set; }

	public byte LegionEmblemColorG { get; set; }

	public byte LegionEmblemColorB { get; set; }

	public string Name { get; init; } = string.Empty;

	public string PlayerClass { get; init; } = string.Empty;

	public string Race { get; init; } = string.Empty;

	public string Gender { get; init; } = string.Empty;

	public string Note { get; set; } = string.Empty;

	public CharacterAppearance Appearance { get; set; } = new();

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

	public int TitleId { get; set; }

	public int BonusTitleId { get; set; }

	public WorldPosition Position { get; set; }

	// Java parity: controllers/movement/PlayerMoveController state mirrored for CM_MOVE/SM_MOVE.
	public PlayerMovementState Movement { get; } = new();

	// Java parity: model/gameobjects/VisibleObject target set by network/aion/clientpackets/CM_TARGET_SELECT.
	public int TargetObjectId { get; set; }

	// Java parity: model/gameobjects/player/Player.isTrading guard used by mail and broker packets.
	public bool IsTrading { get; set; }

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

	// Java parity: services/player/PlayerMailboxState and model/gameobjects/player/Mailbox.mailBoxState.
	public byte MailboxState { get; set; }

	// Java parity: CM_READ_EXPRESS_MAIL.runImpl checks Player.getPostman and TaskId.EXPRESS_MAIL_USE.
	public bool HasSummonedPostman { get; set; }

	public PostmanNpc? Postman { get; set; }

	public DateTimeOffset? ExpressMailCooldownUntil { get; set; }

	public PlayerBrokerSettlementSummary BrokerSettlements { get; set; } = PlayerBrokerSettlementSummary.Empty;

	// Java parity: model/broker/BrokerPlayerCache remembers the last broker list/search for refresh after buy.
	public int BrokerMaskCache { get; set; }

	public byte BrokerSortTypeCache { get; set; }

	public int BrokerStartPageCache { get; set; }

	public IReadOnlyList<int> BrokerSearchItemIds { get; set; } = Array.Empty<int>();

	public IReadOnlyList<PlayerHouse> Houses { get; set; } = Array.Empty<PlayerHouse>();

	public IReadOnlyDictionary<int, long> CraftCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, PlayerPortalCooldown> PortalCooldowns { get; set; } = new Dictionary<int, PlayerPortalCooldown>();

	public PlayerLifeStats? LifeStats { get; set; }

	public IReadOnlyList<PlayerFriend> Friends { get; set; } = Array.Empty<PlayerFriend>();

	// Java parity: model/gameobjects/player/FriendList.Status changed by CM_FRIEND_STATUS.
	public byte FriendListStatus { get; set; }

	// Java parity: model/gameobjects/player/ResponseRequester stores pending SM_QUESTION_WINDOW handlers.
	public PendingFriendRequest? PendingFriendRequest { get; set; }

	public PendingChargeAllRequest? PendingChargeAllRequest { get; set; }

	public IReadOnlyList<PlayerBlockedUser> BlockedUsers { get; set; } = Array.Empty<PlayerBlockedUser>();

	public PlayerAbyssRank AbyssRank { get; set; } = PlayerAbyssRank.Default();

	public PlayerSettings Settings { get; set; } = new();

	public PlayerBindPoint? BindPoint { get; set; }
}
