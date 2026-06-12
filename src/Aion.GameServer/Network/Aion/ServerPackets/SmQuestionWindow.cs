using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmQuestionWindow : GameServerPacket
{
	public const int PacketOpCode = 52;
	// Java parity: SM_QUESTION_WINDOW STR_* message-id constants.
	public const int STR_DUEL_DO_YOU_ACCEPT_REQUEST = 50028;
	public const int STR_DUEL_DO_YOU_WITHDRAW_REQUEST = 50030;
	public const int STR_PARTY_DO_YOU_ACCEPT_INVITATION = 60000;
	public const int STR_PARTY_ALLIANCE_DO_YOU_ACCEPT_HIS_INVITATION = 70000;
	public const int STR_PARTY_ALLIANCE_CHANGE_LOOT_TO_FREE_HE_ASKED = 70001;
	public const int STR_PARTY_ALLIANCE_CHANGE_LOOT_TO_RANDOM_HE_ASKED = 70002;
	public const int STR_PARTY_ALLIANCE_PICKUP_ITEM_HE_ASKED = 70003;
	public const int STR_FORCE_DO_YOU_ACCEPT_INVITATION = 70004;
	public const int STR_GUILD_CREATE_DO_YOU_ACCEPT_PAY = 80000;
	public const int STR_GUILD_INVITE_DO_YOU_ACCEPT_INVITATION = 80001;
	public const int STR_GUILD_TRANSFER_GUILDMASTER = 80005;
	public const int STR_GUILD_DO_YOU_LEAVE = 80006;
	public const int STR_GUILD_DO_YOU_BANISH = 80007;
	public const int STR_GUILD_DISPERSE_STAYMODE = 80008;
	public const int STR_GUILD_DISPERSE_STAYMODE_CANCEL = 80009;
	public const int STR_GUILD_CHANGE_LEVEL_DO_YOU_ACCEPT_PAY = 80010;
	public const int STR_GUILD_CHANGE_MASTER_DO_YOU_ACCEPT_OFFER = 80011;
	public const int STR_BUY_SELL_CONFIRM_PURCHASE_EXCESSIVE_PRICE = 90000;
	public const int STR_EXCHANGE_DO_YOU_ACCEPT_EXCHANGE = 90001;
	public const int STR_QUEST_GIVEUP = 150000;
	public const int STR_QUEST_GIVEUP_WHEN_DELETE_QUEST_ITEM = 150001;
	public const int STR_ASK_RECOVER_EXPERIENCE = 160011;
	public const int STR_ASK_REGISTER_RESURRECT_POINT = 160012;
	public const int STR_TELEPORT_NEED_CONFIRM = 160013;
	public const int STR_ASK_GROUP_GATE_DO_YOU_ACCEPT_MOVE = 160014;
	public const int STR_HOUSE_GATE_ACCEPT_MOVE_DONT_RETURN = 904435;
	public const int STR_ASK_USE_ARTIFACT = 160016;
	public const int STR_ASK_PASS_BY_GATE = 160017;
	public const int STR_ASK_REGISTER_BINDSTONE = 160018;
	public const int STR_ASK_PASS_BY_DIRECT_PORTAL = 160019;
	public const int STR_ASK_DOOR_REPAIR_DO_YOU_ACCEPT_REPAIR = 160021;
	public const int STR_ASK_DOOR_REPAIR_POPUPDIALOG = 160027;
	public const int STR_ASK_ARTIFACT_POPUPDIALOG = 160028;
	public const int STR_ASK_JOIN_NEW_FACTION = 160033;
	public const int STR_CONFIRM_LOOT = 900495;
	public const int STR_WAREHOUSE_EXPAND_WARNING = 900686;
	public const int STR_CRAFT_ADDSKILL_CONFIRM = 900852;
	public const int STR_AIONJEWEL_SHOP_BUY_CONFIRM = 901972;
	public const int STR_SUMMON_PARTY_DO_YOU_ACCEPT_REQUEST = 901721;
	public const int STR_INSTANCE_DUNGEON_WITH_DIFFICULTY_ENTER_CONFIRM = 902050;
	public const int STR_MSGBOX_UNION_INVITE_ME = 902249;
	public const int STR_SOUL_BOUND_ITEM_DO_YOU_WANT_SOUL_BOUND = 95006;
	public const int STR_ITEM_CHARGE_ALL_CONFIRM = 903026;
	public const int STR_ITEM_CHARGE2_ALL_CONFIRM = 904039;
	public const int STR_ITEM_CHARGE_CONFIRM_SOME_ALREADY_CHARGED = 903028;
	public const int STR_ASSEMBLY_ITEM_POPUP_CONFIRM = 903441;
	public const int STR_HOUSING_TELEPORT_HOME_CONFIRM = 903533;
	public const int STR_HOUSING_TELEPORT_BUDDY_CONFIRM = 903534;
	public const int STR_HOUSING_TELEPORT_RANDOM_CONFIRM = 903535;
	public const int STR_HOUSING_TELEPORT_GUILD_CONFIRM = 903536;
	public const int STR_ASK_PASS_BY_SVS_DIRECT_PORTAL = 905067;
	public const int STR_BUDDYLIST_ADD_BUDDY_REQUEST = 1401498;
	public const int DuelAcceptRequest = 50028;
	public const int DuelWithdrawRequest = 50030;
	public const int PartyInvite = 60000;
	public const int AllianceInvite = 70000;
	public const int GuildInviteDoYouAcceptInvitation = 80001;
	public const int ExchangeAcceptRequest = 90001;
	public const int BuddyListAddBuddyRequest = 1401498;
	public const int SoulBoundItemConfirm = 95006;
	public const int GuildChangeMasterDoYouAcceptOffer = 80011;
	public const int ItemChargeAllConfirm = 903026;
	public const int ItemCharge2AllConfirm = 904039;
	public const int AskRecoverExperience = 160011;
	public const int DirectPortalPassConfirm = 160019;
	public const int VortexPortalPassConfirm = 904304;
	public const int RegisterBindstone = 160018;
	public const int UnionInviteMe = 902249;
	public const int WarehouseExpandWarning = 900686;
	public const int CraftAddSkillConfirm = 900852;
	public const int TeleportToNpcConfirm = 905097;
	public const int SummonPartyAcceptRequest = 901721;
	public const int InstanceDungeonWithDifficultyEnterConfirm = 902050;
	public const int VortexDefenderInvitation = 904306;
	private const int MaxParameterCount = 3;

	private readonly int _code;
	private readonly int _senderObjectId;
	private readonly int _rangeOrCooldownSeconds;
	private readonly IReadOnlyList<string> _parameters;

	public SmQuestionWindow(int code, int senderObjectId, int rangeOrCooldownSeconds, params string[] parameters)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_QUESTION_WINDOW(int code, int senderId, int rangeOrCooldownSeconds, Object... params).
		if (parameters.Length > MaxParameterCount)
			throw new ArgumentException("More than three question-window parameters are not supported.", nameof(parameters));
		_code = code;
		_senderObjectId = senderObjectId;
		_rangeOrCooldownSeconds = rangeOrCooldownSeconds;
		_parameters = parameters;
	}

	public int Code => _code;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_QUESTION_WINDOW.writeImpl.
		buffer.WriteD(_code);
		for (var index = 0; index < MaxParameterCount; index++)
			buffer.WriteS(index < _parameters.Count ? _parameters[index] : null);
		buffer.WriteD(0);
		buffer.WriteC(_rangeOrCooldownSeconds > 0 ? 1 : 0);
		buffer.WriteD(_senderObjectId);
		buffer.WriteD(_rangeOrCooldownSeconds);
	}
}
