using Aion.Commons.Network;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSystemMessage : GameServerPacket
{
	public const int PacketOpCode = 25;
	private const int GoldenYellowChatType = 25;

	private readonly int _messageId;
	private readonly IReadOnlyList<string?> _parameters;
	private readonly IReadOnlyList<string?> _specialParameters;

	public SmSystemMessage(int messageId, params string?[] parameters)
		: this(messageId, parameters, Array.Empty<string?>()) { }

	private SmSystemMessage(int messageId, IReadOnlyList<string?> parameters, IReadOnlyList<string?> specialParameters)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_SYSTEM_MESSAGE(ChatType.GOLDEN_YELLOW, null, msgId, params).
		_messageId = messageId;
		_parameters = parameters;
		_specialParameters = specialParameters;
	}

	public int MessageId => _messageId;

	public IReadOnlyList<string?> Parameters => _parameters;

	public static SmSystemMessage DialogTooFarToTalk()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DIALOG_TOO_FAR_TO_TALK.
		return new SmSystemMessage(1300346);
	}

	public static SmSystemMessage WarehouseTooFarFromNpc()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WAREHOUSE_TOO_FAR_FROM_NPC.
		return new SmSystemMessage(1300419);
	}

	public static SmSystemMessage CannotGetLoreItem(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CAN_NOT_GET_LORE_ITEM.
		return new SmSystemMessage(1300422, itemName);
	}

	public static SmSystemMessage LootFailOnLooting()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_LOOT_FAIL_ONLOOTING.
		return new SmSystemMessage(1300829);
	}

	public static SmSystemMessage LootNoRight()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_LOOT_NO_RIGHT.
		return new SmSystemMessage(901338);
	}

	public static SmSystemMessage BuySellHeDoesNotSellItem(string npcName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM(String) -> 1300336.
		return new SmSystemMessage(1300336, npcName);
	}

	public static SmSystemMessage PartyEnteredParty()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_ENTERED_PARTY.
		return new SmSystemMessage(1390262);
	}

	public static SmSystemMessage PartyHeEnteredParty(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_HE_ENTERED_PARTY(String).
		return new SmSystemMessage(1400009, playerName);
	}

	public static SmSystemMessage PartyHeRejectInvitation(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_HE_REJECT_INVITATION(String).
		return new SmSystemMessage(1300161, playerName);
	}

	public static SmSystemMessage PartyInvitedHim(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_INVITED_HIM(String).
		return new SmSystemMessage(1300173, playerName);
	}

	public static SmSystemMessage PartyCantInviteWhenDead()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_CANT_INVITE_WHEN_DEAD.
		return new SmSystemMessage(1300163);
	}

	public static SmSystemMessage RejectedInviteParty(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_INVITE_PARTY(String).
		return new SmSystemMessage(1390116, playerName);
	}

	public static SmSystemMessage DuelNoUserToRequest()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_NO_USER_TO_REQUEST.
		return new SmSystemMessage(1300096);
	}

	public static SmSystemMessage DuelPartnerInvalid(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_PARTNER_INVALID(String).
		return new SmSystemMessage(1300091, playerName);
	}

	public static SmSystemMessage DuelYouAreInDuelAlready()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_YOU_ARE_IN_DUEL_ALREADY.
		return new SmSystemMessage(1300093);
	}

	public static SmSystemMessage DuelPartnerInDuelAlready(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_PARTNER_IN_DUEL_ALREADY(String).
		return new SmSystemMessage(1300092, playerName);
	}

	public static SmSystemMessage RejectedDuel(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_DUEL(String).
		return new SmSystemMessage(1390120, playerName);
	}

	public static SmSystemMessage DuelRequested(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_REQUESTED(String).
		return new SmSystemMessage(1301065, playerName);
	}

	public static SmSystemMessage DuelRequestToPartner(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_REQUEST_TO_PARTNER(String).
		return new SmSystemMessage(1300094, playerName);
	}

	public static SmSystemMessage DuelCantRequestWhenHeIsAskedQuestion(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_CANT_REQUEST_WHEN_HE_IS_ASKED_QUESTION(String).
		return new SmSystemMessage(1300095, playerName);
	}

	public static SmSystemMessage DuelRejectDuel(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_REJECT_DUEL(String).
		return new SmSystemMessage(1301064, playerName);
	}

	public static SmSystemMessage DuelWithdrawRequest(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DUEL_WITHDRAW_REQUEST(String).
		return new SmSystemMessage(1300135, playerName);
	}

	public static SmSystemMessage NotEnoughMoney()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY.
		return new SmSystemMessage(1300388);
	}

	public static SmSystemMessage CraftCantExtendMoney()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CRAFT_CANT_EXTEND_MONEY.
		return new SmSystemMessage(1300834);
	}

	public static SmSystemMessage DontRankUp()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DONT_RANK_UP.
		return new SmSystemMessage(1390233);
	}

	public static SmSystemMessage DontRankUpGathering()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DONT_RANK_UP_GATHERING.
		return new SmSystemMessage(1390253);
	}

	public static SmSystemMessage CraftCantExtendGrandMaster()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CRAFT_CANT_EXTEND_GRAND_MASTER.
		return new SmSystemMessage(1400286);
	}

	public static SmSystemMessage SkillNotEnoughDp()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_DP.
		return new SmSystemMessage(1300016);
	}

	public static SmSystemMessage SkillNotReady()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_NOT_READY.
		return new SmSystemMessage(1300021);
	}

	public static SmSystemMessage SkillCanceled()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_CANCELED.
		return new SmSystemMessage(1300023);
	}

	public static SmSystemMessage SkillCannotCast(string stateName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST(String).
		return new SmSystemMessage(1300026, stateName);
	}

	public static SmSystemMessage SkillCannotCastDead()
	{
		// Java parity: CM_CASTSPELL.runImpl -> STR_SKILL_CANT_CAST(ChatUtil.l10n(1400059)).
		return SkillCannotCast(ChatUtil.L10n(1400059));
	}

	public static SmSystemMessage SkillNotNeedPet()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_NOT_NEED_PET.
		return new SmSystemMessage(1402918);
	}

	public static SmSystemMessage SkillSummonAttackMode(string summonName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_ATTACK_MODE(String). %0 is in Attack mode.
		return new SmSystemMessage(1200008, summonName);
	}

	public static SmSystemMessage SkillSummonGuardMode(string summonName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_GUARD_MODE(String). %0 is in Guard mode.
		return new SmSystemMessage(1200009, summonName);
	}

	public static SmSystemMessage SkillSummonRestMode(string summonName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_REST_MODE(String). %0 is in Resting mode.
		return new SmSystemMessage(1200010, summonName);
	}

	public static SmSystemMessage SkillSummonUnsummoned(string summonName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMONED(String).
		return new SmSystemMessage(1200006, summonName);
	}

	public static SmSystemMessage SkillSummonUnsummonFollower(string summonName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMON_FOLLOWER(String).
		return new SmSystemMessage(1200011, summonName);
	}

	public static SmSystemMessage SkillSummonUnsummonByTooDistance()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMON_BY_TOO_DISTANCE().
		return new SmSystemMessage(1300073);
	}

	public static SmSystemMessage SkillSummonAlreadyHaveAFollower()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_ALREADY_HAVE_A_FOLLOWER(). You already have a spirit following you.
		return new SmSystemMessage(1300072);
	}

	public static SmSystemMessage InstanceDungeonOpenedForSelf(int worldId)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_DUNGEON_OPENED_FOR_SELF(worldId).
		return new SmSystemMessage(1400640, worldId.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage CannotMakeInstanceCoolTime()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME.
		return new SmSystemMessage(1400043);
	}

	public static SmSystemMessage CantInstanceEnterLevel()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ENTER_LEVEL.
		return new SmSystemMessage(1400179);
	}

	public static SmSystemMessage MentorCantEnter(int worldId)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_CANT_ENTER(worldId).
		return new SmSystemMessage(1400766, worldId.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage InstanceDungeonCantEnterNotOpened()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_DUNGEON_CANT_ENTER_NOT_OPENED.
		return new SmSystemMessage(1400361);
	}

	public static SmSystemMessage MentorStart()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_START.
		return new SmSystemMessage(1400762);
	}

	public static SmSystemMessage MentorStartPartyMessage(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_START_PARTYMSG(String).
		return new SmSystemMessage(1400763, playerName);
	}

	public static SmSystemMessage MentorEnd()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_END.
		return new SmSystemMessage(1400764);
	}

	public static SmSystemMessage MentorEndPartyMessage(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_END_PARTYMSG(String).
		return new SmSystemMessage(1400765, playerName);
	}

	public static SmSystemMessage MovePortalErrorInvalidRace()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MOVE_PORTAL_ERROR_INVALID_RACE.
		return new SmSystemMessage(901354);
	}

	public static SmSystemMessage SkillCanNotUseGroupgateNoRight()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_USE_GROUPGATE_NO_RIGHT.
		return new SmSystemMessage(1300150);
	}

	public static SmSystemMessage InstanceCantEnterWithoutItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM.
		return new SmSystemMessage(1400219);
	}

	public static SmSystemMessage EnterOnlyPartyDon()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_PARTY_DON.
		return new SmSystemMessage(1390256);
	}

	public static SmSystemMessage EnterOnlyForceDon()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_FORCE_DON.
		return new SmSystemMessage(1400544);
	}

	public static SmSystemMessage ForceCannotPromoteManager()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_CANNOT_PROMOTE_MANAGER.
		return new SmSystemMessage(1301061);
	}

	public static SmSystemMessage ForceRightNotHave()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_RIGHT_NOT_HAVE.
		return new SmSystemMessage(1300976);
	}

	public static SmSystemMessage ForceYouAreNotForceMember()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_YOU_ARE_NOT_FORCE_MEMBER.
		return new SmSystemMessage(1301015);
	}

	public static SmSystemMessage ForceHeIsNewLeader(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_HE_IS_NEW_LEADER(String).
		return new SmSystemMessage(1300998, playerName);
	}

	public static SmSystemMessage ForceYouBecomeNewLeader()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_YOU_BECOME_NEW_LEADER.
		return new SmSystemMessage(1300999);
	}

	public static SmSystemMessage PartyHeIsNewLeader(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_HE_IS_NEW_LEADER(String).
		return new SmSystemMessage(1300154, playerName);
	}

	public static SmSystemMessage PartyYouBecomeNewLeader()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_YOU_BECOME_NEW_LEADER.
		return new SmSystemMessage(1300155);
	}

	public static SmSystemMessage PartyYouAreBanished()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_YOU_ARE_BANISHED.
		return new SmSystemMessage(1300166);
	}

	public static SmSystemMessage PartyIsDispersed()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_IS_DISPERSED.
		return new SmSystemMessage(1300167);
	}

	public static SmSystemMessage PartyHeLeaveParty(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_HE_LEAVE_PARTY(String).
		return new SmSystemMessage(1300168, playerName);
	}

	public static SmSystemMessage PartyAllianceHeRejectInvitation(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_HE_REJECT_INVITATION(String).
		return new SmSystemMessage(1300190, playerName);
	}

	public static SmSystemMessage PartyAllianceInvitedHisParty(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_INVITED_HIS_PARTY(String).
		return new SmSystemMessage(1300189, playerName);
	}

	public static SmSystemMessage ForceInviteParty(string leaderName, int memberCount)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_INVITE_PARTY(String, int).
		return new SmSystemMessage(1300968, leaderName, memberCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ForceInvitePartyHim(string playerName, string leaderName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_INVITE_PARTY_HIM(String, String).
		return new SmSystemMessage(1300969, playerName, leaderName);
	}

	public static SmSystemMessage ForceInvitedHim(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_INVITED_HIM(String).
		return new SmSystemMessage(1301017, playerName);
	}

	public static SmSystemMessage ForceCantInviteWhenDead()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_CANT_INVITE_WHEN_DEAD.
		return new SmSystemMessage(1301007);
	}

	public static SmSystemMessage ForceOnlyLeaderCanInvite()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_ONLY_LEADER_CAN_INVITE.
		return new SmSystemMessage(1301004);
	}

	public static SmSystemMessage ForceCantAddNewMember()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_CANT_ADD_NEW_MEMBER.
		return new SmSystemMessage(1300996);
	}

	public static SmSystemMessage ForceInviteFailedNotEnoughSlot()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_INVITE_FAILED_NOT_ENOUGH_SLOT.
		return new SmSystemMessage(1300975);
	}

	public static SmSystemMessage ForceCanNotInviteSelf()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_CAN_NOT_INVITE_SELF.
		return new SmSystemMessage(1301006);
	}

	public static SmSystemMessage ForceHeIsAlreadyMemberOfOurForce(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_HE_IS_ALREADY_MEMBER_OF_OUR_FORCE(String).
		return new SmSystemMessage(1301014, playerName);
	}

	public static SmSystemMessage ForceAlreadyOtherForce(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_ALREADY_OTHER_FORCE(String).
		return new SmSystemMessage(1300974, playerName);
	}

	public static SmSystemMessage UnionInviteHim(string playerName, int allianceSize)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_INVITE_HIM(String, int).
		return new SmSystemMessage(1400558, playerName, allianceSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage UnionInviteHisLeader(string playerName, string leaderName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_INVITE_HIS_LEADER(String, String).
		return new SmSystemMessage(1400559, playerName, leaderName);
	}

	public static SmSystemMessage UnionCantInviteWhenHeIsAskedQuestion(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_CANT_INVITE_WHEN_HE_IS_ASKED_QUESTION(String).
		return new SmSystemMessage(1400567, playerName);
	}

	public static SmSystemMessage UnionCantInviteSelf()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_CANT_INVITE_SELF.
		return new SmSystemMessage(1400568);
	}

	public static SmSystemMessage UnionOfflineMember()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_OFFLINE_MEMBER.
		return new SmSystemMessage(1400569);
	}

	public static SmSystemMessage UnionCantInviteWhenDead()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_CANT_INVITE_WHEN_DEAD.
		return new SmSystemMessage(1400570);
	}

	public static SmSystemMessage UnionCantAddNewMember()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_CANT_ADD_NEW_MEMBER.
		return new SmSystemMessage(1400565);
	}

	public static SmSystemMessage UnionAlreadyMyUnion()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_ALREADY_MY_UNION.
		return new SmSystemMessage(1400603);
	}

	public static SmSystemMessage PartyHeBecomeOfflineTimeout(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_HE_BECOME_OFFLINE_TIMEOUT(String).
		return new SmSystemMessage(1300176, playerName);
	}

	public static SmSystemMessage PartyHeIsBanished(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_HE_IS_BANISHED(String).
		return new SmSystemMessage(1300177, playerName);
	}

	public static SmSystemMessage ForceOnlyLeaderCanBanish()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_ONLY_LEADER_CAN_BANISH.
		return new SmSystemMessage(1301009);
	}

	public static SmSystemMessage ForceCantBanSelf()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_CANT_BAN_SELF.
		return new SmSystemMessage(1400706);
	}

	public static SmSystemMessage PartyCantBanSelf()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_CANT_BAN_SELF.
		return new SmSystemMessage(1400705);
	}

	public static SmSystemMessage PartyForceNoRightToDecide()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PARTY_FORCE_NO_RIGHT_TO_DECIDE.
		return new SmSystemMessage(1400749);
	}

	public static SmSystemMessage ForceEnteredForce()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_ENTERED_FORCE.
		return new SmSystemMessage(1390263);
	}

	public static SmSystemMessage ForceHeEnteredForce(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_HE_ENTERED_FORCE(String).
		return new SmSystemMessage(1400013, playerName);
	}

	public static SmSystemMessage UnionChangeForceNumberMe(int forceNumber)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_FORCE_NUMBER_ME(int).
		return new SmSystemMessage(1400589, forceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage UnionChangeLeaderTimeout(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_LEADER_TIMEOUT(String).
		return new SmSystemMessage(1400588, playerName);
	}

	public static SmSystemMessage UnionChangeLeader(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_LEADER(String, String).
		return new SmSystemMessage(1400580, playerName, playerName);
	}

	public static SmSystemMessage UnionChangeForceNumberHim(string playerName, int forceNumber)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_FORCE_NUMBER_HIM(String, int).
		return new SmSystemMessage(1400590, playerName, forceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ForceHeBecomeOffline(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_HE_BECOME_OFFLINE(String).
		return new SmSystemMessage(1301019, playerName);
	}

	public static SmSystemMessage PartyAllianceDispersed()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_DISPERSED.
		return new SmSystemMessage(1300201);
	}

	public static SmSystemMessage PartyAllianceHeLeavedPartyOfflineTimeout(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_HE_LEAVED_PARTY_OFFLINE_TIMEOUT(String).
		return new SmSystemMessage(1300203, playerName);
	}

	public static SmSystemMessage ForceLeaveHim(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_LEAVE_HIM(String).
		return new SmSystemMessage(1300978, playerName);
	}

	public static SmSystemMessage ForceBanMe(string banPersonName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_BAN_ME(String).
		return new SmSystemMessage(1300979, banPersonName);
	}

	public static SmSystemMessage ForceBanHim(string banPersonName, string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FORCE_BAN_HIM(String, String).
		return new SmSystemMessage(1300980, banPersonName, playerName);
	}

	public static SmSystemMessage LeaveInstanceNotParty()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE_NOT_PARTY.
		return new SmSystemMessage(1400042);
	}

	public static SmSystemMessage LeaveInstance(int minutes)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE(int).
		return new SmSystemMessage(1400044, minutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage LeaveInstanceParty(int minutes)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE_PARTY(int).
		return new SmSystemMessage(1400045, minutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage LeaveInstanceForce(int minutes)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE_FORCE(int).
		return new SmSystemMessage(1400046, minutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage EnterOnlyUnionDon()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ENTER_ONLY_UNION_DON.
		return new SmSystemMessage(1401251);
	}

	public static SmSystemMessage NotEnoughKinah(long kinah)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_KINA.
		return new SmSystemMessage(901285, kinah.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage CannotMoveToAirportNotEnoughFee()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE.
		return new SmSystemMessage(1300689);
	}

	public static SmSystemMessage CannotMoveToAirportNoRoute()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE.
		return new SmSystemMessage(1300691);
	}

	public static SmSystemMessage FlyingTimeNotReady()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FLYING_TIME_NOT_READY.
		return new SmSystemMessage(1300961);
	}

	public static SmSystemMessage CannotAskRecoverExperienceByOtherQuestion()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_ASK_RECOVER_EXPERIENCE_BY_OTHER_QUESTION.
		return new SmSystemMessage(1300671);
	}

	public static SmSystemMessage SuccessRecoverExperience()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SUCCESS_RECOVER_EXPERIENCE.
		return new SmSystemMessage(1300674);
	}

	public static SmSystemMessage DoNotHaveRecoverExperience()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DONOT_HAVE_RECOVER_EXPERIENCE.
		return new SmSystemMessage(1300682);
	}

	public static SmSystemMessage GetExp2(long exp)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GET_EXP2.
		return new SmSystemMessage(1370002, exp.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage GetExp(string npcName, long exp)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GET_EXP(String, long).
		return new SmSystemMessage(1370000, npcName, exp.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage GetExpVitalBonus(string npcName, long exp, long repose)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GET_EXP_VITAL_BONUS(String, long, long).
		return new SmSystemMessage(
			1400342,
			npcName,
			exp.ToString(System.Globalization.CultureInfo.InvariantCulture),
			repose.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);
	}

	public static SmSystemMessage GetExpMakeupBonus(string npcName, long exp, long salvation)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GET_EXP_MAKEUP_BONUS(String, long, long).
		return new SmSystemMessage(
			1400343,
			npcName,
			exp.ToString(System.Globalization.CultureInfo.InvariantCulture),
			salvation.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);
	}

	public static SmSystemMessage GetExpVitalMakeupBonus(string npcName, long exp, long repose, long salvation)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GET_EXP_VITAL_MAKEUP_BONUS(String, long, long, long).
		return new SmSystemMessage(
			1400344,
			npcName,
			exp.ToString(System.Globalization.CultureInfo.InvariantCulture),
			repose.ToString(System.Globalization.CultureInfo.InvariantCulture),
			salvation.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);
	}

	public static SmSystemMessage GetExp2VitalBonus(long exp, long repose)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GET_EXP2_VITAL_BONUS(long, long).
		return new SmSystemMessage(
			1400348,
			exp.ToString(System.Globalization.CultureInfo.InvariantCulture),
			repose.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);
	}

	public static SmSystemMessage GetExp2MakeupBonus(long exp, long salvation)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GET_EXP2_MAKEUP_BONUS(long, long).
		return new SmSystemMessage(
			1400349,
			exp.ToString(System.Globalization.CultureInfo.InvariantCulture),
			salvation.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);
	}

	public static SmSystemMessage GetExp2VitalMakeupBonus(long exp, long repose, long salvation)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GET_EXP2_VITAL_MAKEUP_BONUS(long, long, long).
		return new SmSystemMessage(
			1400350,
			exp.ToString(System.Globalization.CultureInfo.InvariantCulture),
			repose.ToString(System.Globalization.CultureInfo.InvariantCulture),
			salvation.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);
	}

	public static SmSystemMessage LevelLimitQuestNotFinished()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_LEVEL_LIMIT_QUEST_NOT_FINISHED1.
		return new SmSystemMessage(1400545);
	}

	public static SmSystemMessage UseAbyssPoint(long abyssPoints)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_USE_ABYSSPOINT.
		return new SmSystemMessage(1300965, abyssPoints.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage CombatMyAbyssPointGain(int abyssPoints)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_MY_ABYSS_POINT_GAIN.
		return new SmSystemMessage(1320000, abyssPoints.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage GloryPointGain(int additionalGp)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_GLORY_POINT_GAIN.
		return new SmSystemMessage(1402081, additionalGp.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage GloryPointLose(int gpLoss)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_GLORY_POINT_LOSE.
		return new SmSystemMessage(1402219, gpLoss.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage InventoryCantExtendMore()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXTEND_INVENTORY_CANT_EXTEND_MORE.
		return new SmSystemMessage(1300430);
	}

	public static SmSystemMessage WarehouseTooManyItemsInventory()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WAREHOUSE_TOO_MANY_ITEMS_INVENTORY.
		return new SmSystemMessage(1300416);
	}

	public static SmSystemMessage InventorySizeExtended(int slots)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXTEND_INVENTORY_SIZE_EXTENDED.
		return new SmSystemMessage(1300431, slots.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage InventoryCantExtendBelowNpcMinimum(string npcName, int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXTEND_INVENTORY_CANT_EXTEND_DUE_TO_MINIMUM_EXTEND_LEVEL_BY_THIS_NPC.
		return new SmSystemMessage(1300436, npcName, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage InventoryCantExtendAboveNpcMaximum(string npcName, int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXTEND_INVENTORY_CANT_EXTEND_MORE_DUE_TO_MAXIMUM_EXTEND_LEVEL_BY_THIS_NPC.
		return new SmSystemMessage(1300437, npcName, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage WarehouseCantExtendMore()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_MORE.
		return new SmSystemMessage(1300432);
	}

	public static SmSystemMessage WarehouseSizeExtended(int slots)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_SIZE_EXTENDED.
		return new SmSystemMessage(1300433, slots.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage WarehouseCantExtendBelowNpcMinimum(string npcName, int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_DUE_TO_MINIMUM_EXTEND_LEVEL_BY_THIS_NPC.
		return new SmSystemMessage(1300438, npcName, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage WarehouseCantExtendAboveNpcMaximum(string npcName, int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_MORE_DUE_TO_MAXIMUM_EXTEND_LEVEL_BY_THIS_NPC.
		return new SmSystemMessage(1300439, npcName, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage WarehouseExpandNotEnoughMoney()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WAREHOUSE_EXPAND_NOT_ENOUGH_MONEY.
		return new SmSystemMessage(1300831);
	}

	public static SmSystemMessage ItemChargeSuccess(string itemName, int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_SUCCESS.
		return new SmSystemMessage(1400887, itemName, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ItemCharge2Success(string itemName, int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_SUCCESS.
		return new SmSystemMessage(1401335, itemName, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ItemChargeAllComplete()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_ALL_COMPLETE.
		return new SmSystemMessage(1400892);
	}

	public static SmSystemMessage ItemCharge2AllComplete()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_ALL_COMPLETE.
		return new SmSystemMessage(1401340);
	}

	public static SmSystemMessage ItemChargeAllFailNoChargeableEquipment()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_ALL_FAIL_NO_CHARGEABLE_EQUIPMENT.
		return new SmSystemMessage(1400895);
	}

	public static SmSystemMessage ItemCharge2AllFailNoChargeableEquipment()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_ALL_FAIL_NO_CHARGEABLE_EQUIPMENT.
		return new SmSystemMessage(1401343);
	}

	public static SmSystemMessage ItemChargeCanceled()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_CANCELED.
		return new SmSystemMessage(1400891);
	}

	public static SmSystemMessage ItemCharge2Canceled()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_CANCELED.
		return new SmSystemMessage(1401339);
	}

	public static SmSystemMessage EnchantItemFailed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_FAILED.
		return new SmSystemMessage(1300456, itemName);
	}

	public static SmSystemMessage EnchantItemNoTargetItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_NO_TARGET_ITEM.
		return new SmSystemMessage(1300452);
	}

	public static SmSystemMessage EnchantItemCanceled(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_CANCELED.
		return new SmSystemMessage(1300457, itemName);
	}

	public static SmSystemMessage EnchantItemSucceedNew(string itemName, int enchantLevel)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_ITEM_SUCCEED_NEW.
		return new SmSystemMessage(1401681, itemName, enchantLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage EnchantItemSucceeded15(string playerName, string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_ITEM_SUCCEEDED_15.
		return new SmSystemMessage(1400435, playerName, itemName);
	}

	public static SmSystemMessage EnchantItemSucceeded20(string playerName, string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_ITEM_SUCCEEDED_20.
		return new SmSystemMessage(1402285, playerName, itemName);
	}

	public static SmSystemMessage GiveItemOptionNoTargetItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_NO_TARGET_ITEM.
		return new SmSystemMessage(1300459);
	}

	public static SmSystemMessage GiveItemOptionCannotBeGivenOption(string itemName, string enchantmentStoneName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_IT_CAN_NOT_BE_GIVEN_OPTION.
		return new SmSystemMessage(1300460, itemName, enchantmentStoneName);
	}

	public static SmSystemMessage GiveItemOptionCannotBeGivenOptionMoreTime(string itemName, string enchantmentStoneName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_IT_CAN_NOT_BE_GIVEN_OPTION_MORE_TIME.
		return new SmSystemMessage(1300461, itemName, enchantmentStoneName);
	}

	public static SmSystemMessage GiveItemOptionSucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_SUCCEED.
		return new SmSystemMessage(1300462, itemName);
	}

	public static SmSystemMessage GiveItemOptionFailed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_FAILED.
		return new SmSystemMessage(1300463, itemName);
	}

	public static SmSystemMessage GiveItemOptionCanceled(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_CANCELED.
		return new SmSystemMessage(1300464, itemName);
	}

	public static SmSystemMessage ItemEnchantAssistantNoRightItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ITEM_ENCHANT_ASSISTANT_NO_RIGHT_ITEM.
		return new SmSystemMessage(1400961);
	}

	public static SmSystemMessage ItemCanceled()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED.
		return new SmSystemMessage(1300427);
	}

	public static SmSystemMessage PolishWrongLevel()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_POLISH_WRONG_LEVEL.
		return new SmSystemMessage(1401649);
	}

	public static SmSystemMessage PolishSuccess(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_POLISH_SUCCEED.
		return new SmSystemMessage(1401650, itemName);
	}

	public static SmSystemMessage PolishChargeEnd(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_POLISH_CHANGE_CONDITION_END.
		return new SmSystemMessage(1401652, itemName);
	}

	public static SmSystemMessage PolishNeedIdentify()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_POLISH_NEED_IDENTIFY.
		return new SmSystemMessage(1401750);
	}

	public static SmSystemMessage BrokerPriceExceedsLimit()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_LIMITED_VENDOR_CANT_OVER_GOLD.
		return new SmSystemMessage(1402666);
	}

	public static SmSystemMessage ExceedCannotAmplify(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_CANNOT_01.
		return new SmSystemMessage(1402650, itemName);
	}

	public static SmSystemMessage ExceedNeedsMaxEnchant()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_CANNOT_02.
		return new SmSystemMessage(1402651);
	}

	public static SmSystemMessage ExceedCannotEnchantAmplified(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_CANNOT_02(String).
		return new SmSystemMessage(1402661, itemName);
	}

	public static SmSystemMessage ExceedNoTargetItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_NO_TARGET_ITEM.
		return new SmSystemMessage(1402655);
	}

	public static SmSystemMessage ExceedAlready()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_ALREADY.
		return new SmSystemMessage(1402656);
	}

	public static SmSystemMessage ExceedSucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_SUCCEED.
		return new SmSystemMessage(1402657, itemName);
	}

	public static SmSystemMessage ExceedSkillEnchant(string itemName, int enchantLevel, string skillName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_SKILL_ENCHANT.
		return new SmSystemMessage(1402662, itemName, enchantLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), skillName);
	}

	public static SmSystemMessage EnchantType1EnchantFail(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_TYPE1_ENCHANT_FAIL.
		return new SmSystemMessage(1402674, itemName);
	}

	public static SmSystemMessage VendorCannotBuyOwnRegisteredItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_VENDOR_CAN_NOT_BUY_MY_REGISTER_ITEM.
		return new SmSystemMessage(1400750);
	}

	public static SmSystemMessage FullInventory()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_FULL_INVENTORY.
		return new SmSystemMessage(1300762);
	}

	public static SmSystemMessage CombineInventoryFull()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_COMBINE_INVENTORY_IS_FULL.
		return new SmSystemMessage(1330037);
	}

	public static SmSystemMessage UiInventoryFull()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UI_INVENTORY_FULL.
		return new SmSystemMessage(1300042);
	}

	public static SmSystemMessage DiceResultMe(int luck, int maxRoll)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_RESULT_ME(int value0, int num1). You rolled and got %0 (max. %num1).
		return new SmSystemMessage(1390162, luck.ToString(System.Globalization.CultureInfo.InvariantCulture), maxRoll.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage DiceResultOther(string playerName, int luck, int maxRoll)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_RESULT_OTHER(String, int, int). %0 rolled and got %1 (max. %num2).
		return new SmSystemMessage(1390163, playerName, luck.ToString(System.Globalization.CultureInfo.InvariantCulture), maxRoll.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage DiceGiveupMe()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_GIVEUP_ME. You gave up rolling the dice.
		return new SmSystemMessage(1390164);
	}

	public static SmSystemMessage DiceGiveupOther(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_GIVEUP_OTHER(String). %0 gave up rolling the dice.
		return new SmSystemMessage(1390165, playerName);
	}

	public static SmSystemMessage DiceInventoryError()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_INVEN_ERROR.
		return new SmSystemMessage(1390182);
	}

	public static SmSystemMessage PayResultMe()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PAY_RESULT_ME. You placed a bid.
		return new SmSystemMessage(1390183);
	}

	public static SmSystemMessage PayResultOther(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PAY_RESULT_OTHER(String). %0 placed a bid.
		return new SmSystemMessage(1390184, playerName);
	}

	public static SmSystemMessage PayAllGiveup()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PAY_ALL_GIVEUP. All members have given up bidding.
		return new SmSystemMessage(1390227);
	}

	public static SmSystemMessage PayGiveupMe()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PAY_GIVEUP_ME. You gave up the Bidding.
		return new SmSystemMessage(1390228);
	}

	public static SmSystemMessage PayGiveupOther(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PAY_GIVEUP_OTHER(String). %0 gave up the Bidding.
		return new SmSystemMessage(1390229, playerName);
	}

	public static SmSystemMessage AssemblyItemSucceeded()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ASSEMBLY_ITEM_SUCCEEDED.
		return new SmSystemMessage(1401122);
	}

	public static SmSystemMessage ItemUpgradeSuccess(string baseItemName, string resultItemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ITEM_UPGRADE_MSG_UPGRADE_SUCCESS.
		return new SmSystemMessage(1402579, baseItemName, resultItemName);
	}

	public static SmSystemMessage DecomposeItemNoTarget()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_NO_TARGET_ITEM.
		return new SmSystemMessage(1300445);
	}

	public static SmSystemMessage DecomposeItemCannotDecompose(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_IT_CAN_NOT_BE_DECOMPOSED.
		return new SmSystemMessage(1300446, itemName);
	}

	public static SmSystemMessage DecomposeItemInventoryFull()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_INVENTORY_IS_FULL.
		return new SmSystemMessage(1300447);
	}

	public static SmSystemMessage DecompressInventoryFull()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DECOMPRESS_INVENTORY_IS_FULL.
		return new SmSystemMessage(1400363);
	}

	public static SmSystemMessage ExpExtractionUse(string extractionItem, long exp, string rewardItem)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXP_EXTRACTION_USE.
		return new SmSystemMessage(1401705, extractionItem, exp.ToString(System.Globalization.CultureInfo.InvariantCulture), rewardItem);
	}

	public static SmSystemMessage ExpExtractionUseNotEnoughExp()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_EXP_EXTRACTION_USE_NOT_ENOUGH_EXP.
		return new SmSystemMessage(1401706);
	}

	public static SmSystemMessage DecomposeItemFailed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_FAILED.
		return new SmSystemMessage(1300448, itemName);
	}

	public static SmSystemMessage DecomposeItemSucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_SUCCEED.
		return new SmSystemMessage(1300449, itemName);
	}

	public static SmSystemMessage DecomposeItemCanceled(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_CANCELED.
		return new SmSystemMessage(1300450, itemName);
	}

	public static SmSystemMessage DecomposeEquippedItemCannotDecompose()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_DECOMPOSE_EQUIP_ITEM_CAN_NOT_BE_DECOMPOSED.
		return new SmSystemMessage(1400279);
	}

	public static SmSystemMessage ChangeItemSkinPcLevelLimit()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_PC_LEVEL_LIMIT.
		return new SmSystemMessage(1300476);
	}

	public static SmSystemMessage ChangeItemSkinNotSkinChangeable(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_NOT_SKIN_CHANGABLE_ITEM.
		return new SmSystemMessage(1300478, itemName);
	}

	public static SmSystemMessage ChangeItemSkinNotCompatible(string keepItemName, string extractItemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_NOT_COMPATIBLE.
		return new SmSystemMessage(1300480, keepItemName, extractItemName);
	}

	public static SmSystemMessage ChangeItemSkinNotEnoughGold(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_NOT_ENOUGH_GOLD.
		return new SmSystemMessage(1300481, itemName);
	}

	public static SmSystemMessage ChangeItemSkinCannotRemoveSkinItem(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_CAN_NOT_REMOVE_SKIN_ITEM.
		return new SmSystemMessage(1300482, itemName);
	}

	public static SmSystemMessage ChangeItemSkinSucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_SUCCEED.
		return new SmSystemMessage(1300483, itemName);
	}

	public static SmSystemMessage ChangeItemSkinInvalidItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CHANGE_ITEM_SKIN_CANNOT_INVALID_ITEM.
		return new SmSystemMessage(1400089);
	}

	public static SmSystemMessage CantChangeSkinOppositeRequirement(string keepItemName, string extractItemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANT_CHANGE_SKIN_OPPOSITE_REQUIREMENT.
		return new SmSystemMessage(1400290, keepItemName, extractItemName);
	}

	public static SmSystemMessage CannotUseItemInvalidRank(string rankName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_RANK.
		return new SmSystemMessage(1300370, rankName);
	}

	public static SmSystemMessage UnequipRankItem(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_UNEQUIP_RANKITEM.
		return new SmSystemMessage(1401329, itemName);
	}

	public static SmSystemMessage CannotUseItemInvalidClass()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_CLASS.
		return new SmSystemMessage(1300371);
	}

	public static SmSystemMessage CannotUseItemTooLowLevel(string itemName, int requiredLevel)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_TOO_LOW_LEVEL_MUST_BE_THIS_LEVEL.
		return new SmSystemMessage(1300372, requiredLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), itemName);
	}

	public static SmSystemMessage CannotUseItemInvalidRace()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_RACE.
		return new SmSystemMessage(1300373);
	}

	public static SmSystemMessage CannotUseItemInvalidGender()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_GENDER.
		return new SmSystemMessage(1300375);
	}

	public static SmSystemMessage CannotUseItemTooHighLevel(int maxLevel, string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_TOO_HIGH_LEVEL.
		return new SmSystemMessage(1400267, maxLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), itemName);
	}

	public static SmSystemMessage CannotUseItemInvalidLocation()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_LOCATION.
		return new SmSystemMessage(1300426);
	}

	public static SmSystemMessage ItemColorError()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ITEM_COLOR_ERROR.
		return new SmSystemMessage(1300514);
	}

	public static SmSystemMessage ItemColorRemoveSucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ITEM_COLOR_REMOVE_SUCCEED.
		return new SmSystemMessage(1300510, itemName);
	}

	public static SmSystemMessage ItemColorChangeSucceed(string itemName, string dyeName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ITEM_COLOR_CHANGE_SUCCEED.
		return new SmSystemMessage(1300511, itemName, dyeName);
	}

	public static SmSystemMessage TooltipLearnedEmotion()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_TOOLTIP_LEARNED_EMOTION.
		return new SmSystemMessage(901713);
	}

	public static SmSystemMessage TooltipLearnedTitle()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_TOOLTIP_LEARNED_TITLE.
		return new SmSystemMessage(901714);
	}

	public static SmSystemMessage QuestGetRewardTitle(string titleName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_QUEST_GET_REWARD_TITLE(String).
		return new SmSystemMessage(1300035, titleName);
	}

	public static SmSystemMessage CashTitle(string titleName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_GET_CASH_TITLE.
		return new SmSystemMessage(1390242, titleName);
	}

	public static SmSystemMessage UncompressCompressedItemSucceeded(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_UNCOMPRESS_COMPRESSED_ITEM_SUCCEEDED.
		return new SmSystemMessage(1400452, itemName);
	}

	public static SmSystemMessage DeleteCashTitleByTimeout(string titleName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DELETE_CASH_TITLE_BY_TIMEOUT.
		return new SmSystemMessage(1390244, titleName);
	}

	public static SmSystemMessage DeleteCashSocialActionByTimeout()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DELETE_CASH_SOCIALACTION_BY_TIMEOUT().
		return new SmSystemMessage(1390245);
	}

	public static SmSystemMessage DeleteCashCustomAnimationByTimeout()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DELETE_CASH_CUSTOMANIMATION_BY_TIMEOUT().
		return new SmSystemMessage(1400917);
	}

	public static SmSystemMessage DeleteCashItemByTimeout(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DELETE_CASH_ITEM_BY_TIMEOUT.
		return new SmSystemMessage(1400034, itemName);
	}

	public static SmSystemMessage DeleteCashItemByTimeoutInWarehouse(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DELETE_CASH_ITEM_BY_TIMEOUT_IN_WAREHOUSE.
		return new SmSystemMessage(1400406, itemName);
	}

	public static SmSystemMessage CashItemTimeLeft(string itemName, int remainingMinutes)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CASH_ITEM_TIME_LEFT.
		return new SmSystemMessage(1400481, itemName, remainingMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "min");
	}

	public static SmSystemMessage CannotRide(string stateName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANT_RIDE.
		return new SmSystemMessage(1401211, stateName);
	}

	public static SmSystemMessage CannotRideInvalidLocation()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_RIDE_INVALID_LOCATION.
		return new SmSystemMessage(1401099);
	}

	public static SmSystemMessage ItemRestrictionRide()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_RESTRICTION_RIDE.
		return new SmSystemMessage(1401094);
	}

	public static SmSystemMessage UnrideAbnormalState()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_UNRIDE_ABNORMAL_STATE.
		return new SmSystemMessage(1401254);
	}

	public static SmSystemMessage CannotRideAbnormalState()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_RIDE_ABNORMAL_STATE.
		return new SmSystemMessage(1401255);
	}

	public static SmSystemMessage CannotRegisterBindstoneFarFromNpc()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_BINDSTONE_FAR_FROM_NPC.
		return new SmSystemMessage(1300800);
	}

	public static SmSystemMessage CannotRegisterBindstoneHaveNoAuthority()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_BINDSTONE_HAVE_NO_AUTHORITY.
		return new SmSystemMessage(1300799);
	}

	public static SmSystemMessage CannotUseBindstoneItemWhileFlying()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_USE_BINDSTONE_ITEM_WHILE_FLYING.
		return new SmSystemMessage(1300806);
	}

	public static SmSystemMessage BindstoneRegister()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BINDSTONE_REGISTER.
		return new SmSystemMessage(1390159);
	}

	public static SmSystemMessage BindstoneAlreadyInstalled()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BINDSTONE_ALREADY_INSTALLED.
		return new SmSystemMessage(1390160);
	}

	public static SmSystemMessage BindstoneAlreadyRegistered()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BINDSTONE_ALREADY_REGISTERED.
		return new SmSystemMessage(1390161);
	}

	public static SmSystemMessage CannotRegisterBindstoneFull()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_BINDSTONE_FULL.
		return new SmSystemMessage(1400247);
	}

	public static SmSystemMessage RecipeItemCannotUseNoRecipe()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_RECIPEITEM_CANT_USE_NO_RECIPE.
		return new SmSystemMessage(1300621);
	}

	public static SmSystemMessage CraftRecipeRaceCheck()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CRAFTRECIPE_RACE_CHECK.
		return new SmSystemMessage(1300876);
	}

	public static SmSystemMessage CraftRecipeLearnedAlready()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CRAFT_RECIPE_LEARNED_ALREADY.
		return new SmSystemMessage(1330060);
	}

	public static SmSystemMessage CraftRecipeLearn(int recipeId, string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CRAFT_RECIPE_LEARN.
		return new SmSystemMessage(1330061, $"[recipe_ex:{recipeId};{playerName}]");
	}

	public static SmSystemMessage CraftRecipeCantLearnSkill(string skillName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CRAFT_RECIPE_CANT_LEARN_SKILL.
		return new SmSystemMessage(1330062, skillName);
	}

	public static SmSystemMessage CraftRecipeCantLearnSkillPoint()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CRAFT_RECIPE_CANT_LEARN_SKILLPOINT.
		return new SmSystemMessage(1330063);
	}

	public static SmSystemMessage CombineCannotFindRecipe()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_COMBINE_CAN_NOT_FIND_RECIPE.
		return new SmSystemMessage(1330043);
	}

	public static SmSystemMessage CombineCantUse(string skillName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_COMBINE_CANT_USE.
		return new SmSystemMessage(1330042, skillName);
	}

	public static SmSystemMessage CombineOutOfSkillPoint(string skillName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_COMBINE_OUT_OF_SKILL_POINT.
		return new SmSystemMessage(1330044, skillName);
	}

	public static SmSystemMessage CombineNoComponentItemSingle(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_COMBINE_NO_COMPONENT_ITEM_SINGLE.
		return new SmSystemMessage(1330046, itemName);
	}

	public static SmSystemMessage CombineNoComponentItemMultiple(long quantity, string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_COMBINE_NO_COMPONENT_ITEM_MULTIPLE.
		return new SmSystemMessage(1330047, itemName, quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage SoulBoundItemSucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_ITEM_SUCCEED.
		return new SmSystemMessage(1300485, itemName);
	}

	public static SmSystemMessage SoulBoundItemCanceled(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_ITEM_CANCELED.
		return new SmSystemMessage(1300487, itemName);
	}

	public static SmSystemMessage SoulBoundCloseOtherMsgBoxAndRetry()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_CLOSE_OTHER_MSG_BOX_AND_RETRY.
		return new SmSystemMessage(1300488);
	}

	public static SmSystemMessage SoulBoundInvalidStance(string stance)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SOUL_BOUND_INVALID_STANCE.
		return new SmSystemMessage(1300489, stance);
	}

	public static SmSystemMessage WeaponBoostNoBoosterEquipped()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_NO_BOOSTER_EQUIPED.
		return new SmSystemMessage(1300490);
	}

	public static SmSystemMessage WeaponBoostStarted()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_BOOST_MODE_STARTED.
		return new SmSystemMessage(1300491);
	}

	public static SmSystemMessage WeaponBoostEnded()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_BOOST_MODE_ENDED.
		return new SmSystemMessage(1300492);
	}

	public static SmSystemMessage RebirthMassageMe()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME. Sent to revived player on skill/rebirth/item resurrection.
		return new SmSystemMessage(1300738);
	}

	public static SmSystemMessage WeaponBoostBurnOut()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_WEAPON_BOOST_MODE_BURN_OUT.
		return new SmSystemMessage(1400075);
	}

	public static SmSystemMessage ItemCantUseUntilDelayTime()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ITEM_CANT_USE_UNTIL_DELAY_TIME.
		return new SmSystemMessage(1300494);
	}

	public static SmSystemMessage SkillCannotChangeModeWhileInCurrentStance()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_CHANGE_MODE__WHILE_IN_CURRENT_STANCE.
		return new SmSystemMessage(1300124);
	}

	public static SmSystemMessage CraftCannotCombineWhileInCurrentStance()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_COMBINE_WHILE_IN_CURRENT_STANCE.
		return new SmSystemMessage(1300122);
	}

	public static SmSystemMessage SkillCannotTakeOffWhileInCurrentStance()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_TAKE_OFF__WHILE_IN_CURRENT_STANCE.
		return new SmSystemMessage(1300147);
	}

	public static SmSystemMessage FlyingForbiddenHere()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FLYING_FORBIDDEN_HERE.
		return new SmSystemMessage(1300960);
	}

	public static SmSystemMessage CantFlyNowDueToNoFly()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANT_FLY_NOW_DUE_TO_NOFLY.
		return new SmSystemMessage(1301032);
	}

	public static SmSystemMessage GlideOnlyDaevaCan()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GLIDE_ONLY_DEVA_CAN.
		return new SmSystemMessage(1301059);
	}

	public static SmSystemMessage GlideCannotGlidePolymorphStatus()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GLIDE_CANNOT_GLIDE_POLYMORPH_STATUS.
		return new SmSystemMessage(1400872);
	}

	public static SmSystemMessage FlyCannotFlyPolymorphStatus()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_FLY_CANNOT_FLY_POLYMORPH_STATUS.
		return new SmSystemMessage(1400873);
	}

	public static SmSystemMessage PersonalShopCanNotSellEquippedItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_CAN_NOT_SELL_EQUIPED_ITEM.
		return new SmSystemMessage(1300660);
	}

	public static SmSystemMessage PersonalShopCannotBeExchanged()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_CANNOT_BE_EXCHANGED. You cannot sell items that cannot be traded.
		return new SmSystemMessage(1300661);
	}

	public static SmSystemMessage PersonalShopCanNotSellUsedItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_CAN_NOT_SELL_USED_ITEM. You cannot sell used items.
		return new SmSystemMessage(1300662);
	}

	public static SmSystemMessage PersonalShopDisabledInCombatMode()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_DISABLED_IN_COMBAT_MODE.
		return new SmSystemMessage(1300663);
	}

	public static SmSystemMessage PersonalShopClosedForCombatMode()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_CLOSED_FOR_COMBAT_MODE.
		return new SmSystemMessage(1300664);
	}

	public static SmSystemMessage PersonalShopSoldOut(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_SOLD_OUT(String). %0 has already been sold.
		return new SmSystemMessage(1300665, itemName);
	}

	public static SmSystemMessage PersonalShopFullBasket()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_FULL_BASKET. You cannot register any more items.
		return new SmSystemMessage(1300666);
	}

	public static SmSystemMessage PersonalShopAlreadyRegistItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_ALREAY_REGIST_ITEM.
		return new SmSystemMessage(1300942);
	}

	public static SmSystemMessage PersonalShopCannotRegistDuringStigma()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_CANNOT_REGIST_DURING_STIGMA.
		return new SmSystemMessage(1300943);
	}

	public static SmSystemMessage PersonalShopCannotRegistDuringSelling()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_CANNOT_REGIST_DURING_SELLING.
		return new SmSystemMessage(1300944);
	}

	public static SmSystemMessage PersonalShopDisabledInMovingObject()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_DISABLED_IN_MOVING_OBJECT.
		return new SmSystemMessage(1300714);
	}

	public static SmSystemMessage PersonalShopDisabledInFlyMode()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_DISABLED_IN_FLY_MODE.
		return new SmSystemMessage(1300798);
	}

	public static SmSystemMessage CantOpenStoreDuringCrafting()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANT_OPEN_STORE_DURING_CRAFTING (name is NC fail; message content is correct).
		return new SmSystemMessage(1400078);
	}

	public static SmSystemMessage PersonalShopRestrictionRide()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_RESTRICTION_RIDE.
		return new SmSystemMessage(1401095);
	}

	public static SmSystemMessage PersonalShopDisabledInHiddenMode()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_DISABLED_IN_HIDDEN_MODE.
		return new SmSystemMessage(1401969);
	}

	public static SmSystemMessage InstanceClosedTime(int worldId)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CLOSED_TIME(int). Instance is not available at this time.
		return new SmSystemMessage(1401306, worldId.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ExchangePartnerTooHeavyToExchange()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_PARTNER_TOO_HEAVY_TO_EXCHANGE. Partner is carrying too many items.
		return new SmSystemMessage(1300357);
	}

	public static SmSystemMessage AbyssOrderRankerDie(string raceL10n, string rankL10n, string playerName, string zoneName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_ABYSS_ORDER_RANKER_DIE(Player, String). %0 %1 %2 has died in %3.
		return new SmSystemMessage(1400023, raceL10n, rankL10n, playerName, zoneName);
	}

	public static SmSystemMessage AbyssSkillIsFired(string raceL10n, string playerName, string subZoneString, string skillL10n)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_SKILL_ABYSS_SKILL_IS_FIRED(Player, String). %0 %1 in %2 used %3.
		return new SmSystemMessage(1390155, raceL10n, playerName, subZoneString, skillL10n);
	}

	public static SmSystemMessage ExchangeCantExchangeHeavyToAddExchangeItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_EXCHANGE_HEAVY_TO_ADD_EXCHANGE_ITEM. You are carrying too many items to trade.
		return new SmSystemMessage(1300359);
	}

	public static SmSystemMessage GuildWarehouseNoRight()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_NO_RIGHT. Legion warehouse access denied.
		return new SmSystemMessage(1300322);
	}

	public static SmSystemMessage WarehouseCantDepositItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WAREHOUSE_CANT_DEPOSIT_ITEM. You cannot store this in the warehouse.
		return new SmSystemMessage(1300418);
	}

	public static SmSystemMessage InventorySplitDuringTrade()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_INVENTORY_SPLIT_DURING_TRADE.
		return new SmSystemMessage(1300713);
	}

	public static SmSystemMessage WarehouseCantLegionDeposit()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_WAREHOUSE_CANT_LEGION_DEPOSIT. You cannot store this item in the Legion warehouse.
		return new SmSystemMessage(1400355);
	}

	public static SmSystemMessage WarehouseCantAccountDeposit()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_WAREHOUSE_CANT_ACCOUNT_DEPOSIT. You cannot store this item in the account warehouse.
		return new SmSystemMessage(1400356);
	}

	public static SmSystemMessage CaptchaRecovered()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CAPTCHA_RECOVERED.
		return new SmSystemMessage(1400269);
	}

	public static SmSystemMessage CanChatNow()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CAN_CHAT_NOW. You can now use the chatting functions again.
		return new SmSystemMessage(1400136);
	}

	public static SmSystemMessage DayCannotSellNpc(long remainingLimit)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DAY_CANNOT_SELL_NPC(long).
		return new SmSystemMessage(1400938, remainingLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage PersonalShopSellItem(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_SELL_ITEM(String). You have sold %0.
		return new SmSystemMessage(1400134, itemName);
	}

	public static SmSystemMessage PersonalShopSellItemMulti(long count, string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_SELL_ITEM_MULTI(long, String). You have sold %num1 %0s.
		// Java parameter order: (value0=itemName, num1=count).
		return new SmSystemMessage(1400135, itemName, count.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage MerchantPetGetSellItem(string? petName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_MERCHANT_PET_GET_SELL_ITEM(String). %0 sold the item automatically.
		return new SmSystemMessage(1402570, petName);
	}

	public static SmSystemMessage StigmaNotEnoughMoney()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_STIGMA_NOT_ENOUGH_MONEY.
		return new SmSystemMessage(1300413);
	}

	public static SmSystemMessage StigmaSkillUnavailable(string skillName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_STIGMA_YOU_CANNOT_USE_THIS_SKILL_AFTER_UNEQUIP_STIGMA_STONE.
		return new SmSystemMessage(1300403, skillName);
	}

	public static SmSystemMessage StigmaEnchantSuccess(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_STIGMA_ENCHANT_SUCCESS.
		return new SmSystemMessage(1402930, itemName);
	}

	public static SmSystemMessage StigmaEnchantFail(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_STIGMA_ENCHANT_FAIL.
		return new SmSystemMessage(1402931, itemName);
	}

	public static SmSystemMessage StigmaDeleteHiddenSkill(string? firstSkillName, int skillLevel, string? secondSkillName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_STIGMA_DELETE_HIDDEN_SKILL.
		return new SmSystemMessage(1402895, firstSkillName, skillLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), secondSkillName);
	}

	public static SmSystemMessage RemoveItemOptionNoTargetItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_NO_TARGET_ITEM.
		return new SmSystemMessage(1300468);
	}

	public static SmSystemMessage RemoveItemOptionNoOptionToRemove(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_NO_OPTION_TO_REMOVE.
		return new SmSystemMessage(1300470, itemName);
	}

	public static SmSystemMessage RemoveItemOptionInvalidSlot(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_INVALID_OPTION_SLOT_NUMBER.
		return new SmSystemMessage(1300471, itemName);
	}

	public static SmSystemMessage RemoveItemOptionNotEnoughGold(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_NOT_ENOUGH_GOLD.
		return new SmSystemMessage(1300472, itemName);
	}

	public static SmSystemMessage RemoveItemOptionSucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_SUCCEED.
		return new SmSystemMessage(1300473, itemName);
	}

	public static SmSystemMessage GiveItemProcNoTargetItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NO_TARGET_ITEM.
		return new SmSystemMessage(1300502);
	}

	public static SmSystemMessage GiveItemProcCannotGiveToEquippedItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_CANNOT_GIVE_PROC_TO_EQUIPPED_ITEM.
		return new SmSystemMessage(1300503);
	}

	public static SmSystemMessage GiveItemProcNotProcGivableItem(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NOT_PROC_GIVABLE_ITEM.
		return new SmSystemMessage(1300504, itemName);
	}

	public static SmSystemMessage GiveItemProcNoProcGiveItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NO_PROC_GIVE_ITEM.
		return new SmSystemMessage(1300505);
	}

	public static SmSystemMessage GiveItemProcEnchantedTargetItem(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_ENCHANTED_TARGET_ITEM.
		return new SmSystemMessage(1300508, itemName);
	}

	public static SmSystemMessage GiveItemProcCancel(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_GIVE_PROC_CANCEL.
		return new SmSystemMessage(1402238, itemName);
	}

	public static SmSystemMessage ItemReidentifyWrongSelect(string tuningScrollName, string targetItemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_WRONG_SELECT.
		return new SmSystemMessage(1401633, tuningScrollName, targetItemName);
	}

	public static SmSystemMessage ItemReidentifyWrongLevel(string tuningScrollName, string targetItemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_WRONG_LEVEL.
		return new SmSystemMessage(1401635, tuningScrollName, targetItemName);
	}

	public static SmSystemMessage ItemReidentifyCannotReidentify(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_CANNOT_REIDENTIFY.
		return new SmSystemMessage(1401636, itemName);
	}

	public static SmSystemMessage ItemReidentifyDidntIdentify(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_DIDNT_IDENTIFY.
		return new SmSystemMessage(1401637, itemName);
	}

	public static SmSystemMessage ItemReidentifyCanceled(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_CANCELED.
		return new SmSystemMessage(1401638, itemName);
	}

	public static SmSystemMessage ItemReidentifySucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_SUCCEED.
		return new SmSystemMessage(1401639, itemName);
	}

	public static SmSystemMessage ItemReidentifyApplyYes(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_APPLY_YES.
		return new SmSystemMessage(1401910, itemName);
	}

	public static SmSystemMessage ItemReidentifyApplyNo()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_REIDENTIFY_APPLY_NO.
		return new SmSystemMessage(1401911);
	}

	public static SmSystemMessage ItemIdentifyCanceled(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_IDENTIFY_CANCELED.
		return new SmSystemMessage(1401625, itemName);
	}

	public static SmSystemMessage ItemIdentifySucceed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_IDENTIFY_SUCCEED.
		return new SmSystemMessage(1401626, itemName);
	}

	public static SmSystemMessage ItemAuthorizeCancel(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_AUTHORIZE_CANCEL.
		return new SmSystemMessage(1402147, itemName);
	}

	public static SmSystemMessage ItemAuthorizeSucceeded(string itemName, int temperingLevel)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_AUTHORIZE_SUCCEEDED.
		return new SmSystemMessage(1402148, itemName, temperingLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ItemAuthorizeFailed(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_AUTHORIZE_FAILED.
		return new SmSystemMessage(1402149, itemName);
	}

	public static SmSystemMessage ItemAuthorizeSucceededMax(string playerName, string itemName, int temperingLevel)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_AUTHORIZE_SUCCEEDED_MAX.
		return new SmSystemMessage(1402154, playerName, itemName, temperingLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage ItemAuthorizeFailedTShirt(string plumeName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ITEM_AUTHORIZE_FAILED_TSHIRT.
		return new SmSystemMessage(1402447, plumeName);
	}

	public static SmSystemMessage ExchangeFullInventory()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_FULL_INVENTORY.
		return new SmSystemMessage(1300366);
	}

	public static SmSystemMessage ExchangeNoOneToExchange()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_NO_ONE_TO_EXCHANGE.
		return new SmSystemMessage(1300351);
	}

	public static SmSystemMessage ExchangeTooFarToExchange()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_TOO_FAR_TO_EXCHANGE.
		return new SmSystemMessage(1300352);
	}

	public static SmSystemMessage ExchangeAskedExchangeToHim(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_ASKED_EXCHANGE_TO_HIM.
		return new SmSystemMessage(1300353, playerName);
	}

	public static SmSystemMessage ExchangeHeRejectedExchange(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_HE_REJECTED_EXCHANGE.
		return new SmSystemMessage(1300354, playerName);
	}

	public static SmSystemMessage ExchangeCantAskWhenHeIsAskedQuestion(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_ASK_WHEN_HE_IS_ASKED_QUESTION.
		return new SmSystemMessage(1300356, playerName);
	}

	public static SmSystemMessage ExchangeCantExchangeWhileInvisible()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_EXCHANGE_WHILE_INVISIBLE.
		return new SmSystemMessage(1300348);
	}

	public static SmSystemMessage ExchangeCantExchangeWithInvisibleUser()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_EXCHANGE_WITH_INVISIBLE_USER.
		return new SmSystemMessage(1300349);
	}

	public static SmSystemMessage MsgRejectedTrade(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_TRADE.
		return new SmSystemMessage(1390121, playerName);
	}

	public static SmSystemMessage MailTakeAllCancel()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MAIL_TAKE_ALL_CANCEL.
		return new SmSystemMessage(1402251);
	}

	public static SmSystemMessage MailSendCannotSendEquippedItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MAIL_SEND_CAN_NOT_SEND_EQUIPPED_ITEM.
		return new SmSystemMessage(1300496);
	}

	public static SmSystemMessage MailSendUsedItem()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MAIL_SEND_USED_ITEM.
		return new SmSystemMessage(1300498);
	}

	public static SmSystemMessage PostmanAlreadySummoned()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_POSTMAN_ALREADY_SUMMONED.
		return new SmSystemMessage(1300877);
	}

	public static SmSystemMessage PostmanUnableInCooltime()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_POSTMAN_UNABLE_IN_COOLTIME.
		return new SmSystemMessage(1300878);
	}

	public static SmSystemMessage PostmanNotify()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_POSTMAN_NOTIFY.
		return new SmSystemMessage(1300899);
	}

	public static SmSystemMessage RecallRejectEffect(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_Recall_Reject_EFFECT.
		return new SmSystemMessage(1400099, playerName);
	}

	public static SmSystemMessage RecallRejectedEffect(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_Recall_Rejected_EFFECT.
		return new SmSystemMessage(1400100, playerName);
	}

	public static SmSystemMessage HousingObjectOccupiedByOther()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_OCCUPIED_BY_OTHER.
		return new SmSystemMessage(1401256);
	}

	public static SmSystemMessage HousingObjectUse(string? objectName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_USE.
		return new SmSystemMessage(1401257, objectName);
	}

	public static SmSystemMessage HousingObjectCancelUse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_CANCEL_USE.
		return new SmSystemMessage(1401258);
	}

	public static SmSystemMessage HousingObjectCantUsePerDay()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_CANT_USE_PER_DAY.
		return new SmSystemMessage(1401260);
	}

	public static SmSystemMessage HousingObjectDeleteExpireTime(string objectName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_DELETE_EXPIRE_TIME.
		return new SmSystemMessage(1401261, objectName);
	}

	public static SmSystemMessage HousingObjectRewardItem(string objectName, string rewardName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_REWARD_ITEM.
		return new SmSystemMessage(1401263, objectName, rewardName);
	}

	public static SmSystemMessage HousingObjectDeleteUseCountFinal(string objectName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_DELETE_USE_COUNT_FINAL.
		return new SmSystemMessage(1401470, objectName);
	}

	public static SmSystemMessage HousingBidCancel()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_CANCEL.
		return new SmSystemMessage(1401266);
	}

	public static SmSystemMessage HousingBidWin(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_WIN.
		return new SmSystemMessage(1401267, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingBidSuccess(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_SUCCESS.
		return new SmSystemMessage(1401265, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingBidFail()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_FAIL.
		return new SmSystemMessage(1401348);
	}

	public static SmSystemMessage HousingCantOwnNotCompleteQuest(int questId)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_OWN_NOT_COMPLETE_QUEST.
		return new SmSystemMessage(1401277, questId.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingCantBidTimeout()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_TIMEOUT.
		return new SmSystemMessage(1401274);
	}

	public static SmSystemMessage HousingCantBidMyHouse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_MY_HOUSE.
		return new SmSystemMessage(1401221);
	}

	public static SmSystemMessage HousingCantBidGraceHouse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_GRACE_HOUSE.
		return new SmSystemMessage(1401224);
	}

	public static SmSystemMessage HousingCantBidOverdue()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_OVERDUE.
		return new SmSystemMessage(1401349);
	}

	public static SmSystemMessage HousingCantBidLowLevel(int minLevel)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_LOW_LEVEL.
		return new SmSystemMessage(1401225, minLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingOverdue()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OVERDUE.
		return new SmSystemMessage(1401226);
	}

	public static SmSystemMessage HousingSequestrate()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_SEQUESTRATE.
		return new SmSystemMessage(1401227);
	}

	public static SmSystemMessage HousingCantBidAlreadyHighest()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_SUCC_BID_HOUSE.
		return new SmSystemMessage(1401222);
	}

	public static SmSystemMessage HousingCantBidOtherHouse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_OTHER_HOUSE.
		return new SmSystemMessage(1401223);
	}

	public static SmSystemMessage HousingCantBidNotEnoughMoney(long kinah)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_NOT_ENOUGH_MONEY.
		return new SmSystemMessage(1401283, kinah.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingCannotUseFlowerpotCooltime()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANNOT_USE_FLOWERPOT_COOLTIME.
		return new SmSystemMessage(1401280);
	}

	public static SmSystemMessage HousingCantBidExcessAccount()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_EXCESS_ACCOUNT.
		return new SmSystemMessage(1401497);
	}

	public static SmSystemMessage HousingCantBidLower()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_LOWER.
		return new SmSystemMessage(1401307);
	}

	public static SmSystemMessage HousingObjectTooFarToUse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_TOO_FAR_TO_USE.
		return new SmSystemMessage(1401297);
	}

	public static SmSystemMessage HousingObjectOnlyForOwnerValid()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_IS_ONLY_FOR_OWNER_VALID.
		return new SmSystemMessage(1401298);
	}

	public static SmSystemMessage CantUseHouseObjectItemEquip(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANT_USE_HOUSE_OBJECT_ITEM_EQUIP.
		return new SmSystemMessage(1401294, itemName);
	}

	public static SmSystemMessage HousingObjectAchieveUseCount()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_ACHIEVE_USE_COUNT.
		return new SmSystemMessage(1401295);
	}

	public static SmSystemMessage HousingObjectAllCantUse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_ALL_CANT_USE.
		return new SmSystemMessage(1401296);
	}

	public static SmSystemMessage CantUseHouseObjectItemCheck(string itemName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANT_USE_HOUSE_OBJECT_ITEM_CHECK.
		return new SmSystemMessage(1401199, itemName);
	}

	public static SmSystemMessage HousingPriceChange(long kinah)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_PRICE_CHANGE.
		return new SmSystemMessage(1401324, kinah.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingFlowerpotGoal(string objectName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_FLOWERPOT_GOAL.
		return new SmSystemMessage(1401333, objectName);
	}

	public static SmSystemMessage HousingAuctionSuccess(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_SUCCESS.
		return new SmSystemMessage(1401269, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingAuctionFail(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_FAIL.
		return new SmSystemMessage(1401270, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingCantAuctionTimeout()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_AUCTION_TIMEOUT.
		return new SmSystemMessage(1401308);
	}

	public static SmSystemMessage HousingCantAuctionOverdue()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_AUCTION_OVERDUE.
		return new SmSystemMessage(1401317);
	}

	public static SmSystemMessage HousingAuctionMyHouse(int address)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_MY_HOUSE.
		return new SmSystemMessage(1401268, address.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage HousingAuctionAlreadyRegistered()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_FAIL_ALREADY_REGISTED.
		return new SmSystemMessage(1401372);
	}

	public static SmSystemMessage HousingFeeFree()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_F2P_CASH_HOUSE_FEE_FREE.
		return new SmSystemMessage(1401445);
	}

	public static SmSystemMessage InvasionDirectPortalOpenNotice()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_INVADE_DIRECT_PORTAL_OPEN_NOTICE.
		return new SmSystemMessage(1401454);
	}

	public static SmSystemMessage HousingOrderOpenDoor()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_OPEN_DOOR.
		return new SmSystemMessage(1401379);
	}

	public static SmSystemMessage HousingOrderCloseDoorWithoutFriends()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_CLOSE_DOOR_WITHOUT_FRIENDS.
		return new SmSystemMessage(1401380);
	}

	public static SmSystemMessage HousingOrderCloseDoorAll()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_CLOSE_DOOR_ALL.
		return new SmSystemMessage(1401381);
	}

	public static SmSystemMessage HousingOrderOutWithoutFriends()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_OUT_WITHOUT_FRIENDS.
		return new SmSystemMessage(1401382);
	}

	public static SmSystemMessage HousingOrderOutAll()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_OUT_ALL.
		return new SmSystemMessage(1401383);
	}

	public static SmSystemMessage HousingRequestOut()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_REQUEST_OUT.
		return new SmSystemMessage(1401384);
	}

	public static SmSystemMessage CannotUseAlreadyHaveRewardItem(string rewardName, string objectName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_USE_ALREADY_HAVE_REWARD_ITEM.
		return new SmSystemMessage(1401396, rewardName, objectName);
	}

	public static SmSystemMessage HousingChangeOwner()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CHANGE_OWNER.
		return new SmSystemMessage(1401385);
	}

	public static SmSystemMessage NoSuchUser(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER.
		return new SmSystemMessage(1300627, playerName);
	}

	public static SmSystemMessage YouExcluded(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_YOU_EXCLUDED.
		return new SmSystemMessage(1300628, playerName);
	}

	public static SmSystemMessage WhisperRefuse(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_WHISPER_REFUSE.
		return new SmSystemMessage(1300629, playerName);
	}

	public static SmSystemMessage CantWhisperLevel(int level)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_CANT_WHISPER_LEVEL.
		return new SmSystemMessage(1310004, level.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	public static SmSystemMessage CantWhisperOtherRace()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_CANT_WHISPER_OTHER_RACE.
		return new SmSystemMessage(1401174);
	}

	public static SmSystemMessage BuddyListNotInList()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUDDYLIST_NOT_IN_LIST.
		return new SmSystemMessage(1300889);
	}

	public static SmSystemMessage BlockListNotInList()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BLOCKLIST_NOT_IN_LIST.
		return new SmSystemMessage(1300897);
	}

	public static SmSystemMessage BlockListNoBuddy()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BLOCKLIST_NO_BUDDY.
		return new SmSystemMessage(1300891);
	}

	public static SmSystemMessage BlockListAlreadyBlocked()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BLOCKLIST_ALREADY_BLOCKED.
		return new SmSystemMessage(1300894);
	}

	public static SmSystemMessage BuddyListBusy()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUDDYLIST_BUSY.
		return new SmSystemMessage(900847);
	}

	public static SmSystemMessage BuddyCantAddWhenAskedQuestion(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUDDY_CANT_ADD_WHEN_HE_IS_ASKED_QUESTION.
		return new SmSystemMessage(1300795, playerName);
	}

	public static SmSystemMessage BuddyListNoBlockedCharacter()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_BUDDYLIST_NO_BLOCKED_CHARACTER.
		return new SmSystemMessage(1300884);
	}

	public static SmSystemMessage RejectedFriend(string playerName)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_FRIEND.
		return new SmSystemMessage(1390119, playerName);
	}

	public static SmSystemMessage AccuseInfoNormal()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_INFO_NORMAL.
		return new SmSystemMessage(1400076);
	}

	public static SmSystemMessage DoNotAccuse()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DO_NOT_ACCUSE.
		return new SmSystemMessage(1400020);
	}

	public static SmSystemMessage InvalidTarget()
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_INVALID_TARGET.
		return new SmSystemMessage(1300823);
	}

	public static SmSystemMessage AccuseSubmit(string playerName, string remainingReports)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_SUBMIT.
		return new SmSystemMessage(1390258, playerName, remainingReports);
	}

	public static SmSystemMessage AccuseCountInfo(string remainingReports)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_COUNT_INFO.
		return new SmSystemMessage(1400091, remainingReports);
	}

	public static SmSystemMessage DiceCustomMe(int roll, int maxRoll)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_CUSTOM_ME.
		return new SmSystemMessage(
			1400126,
			roll.ToString(System.Globalization.CultureInfo.InvariantCulture),
			maxRoll.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);
	}

	public static SmSystemMessage DiceCustomOther(string playerName, int roll, int maxRoll)
	{
		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_DICE_CUSTOM_OTHER.
		return new SmSystemMessage(
			1400127,
			playerName,
			roll.ToString(System.Globalization.CultureInfo.InvariantCulture),
			maxRoll.ToString(System.Globalization.CultureInfo.InvariantCulture)
		);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_SYSTEM_MESSAGE.writeImpl.
		buffer.WriteC(GoldenYellowChatType);
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteD(_messageId);
		buffer.WriteC(_parameters.Count);
		foreach (var parameter in _parameters)
			buffer.WriteS(parameter);
		buffer.WriteC(_specialParameters.Count);
		foreach (var parameter in _specialParameters)
			buffer.WriteS(parameter);
	}
}
