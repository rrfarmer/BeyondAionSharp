using Aion.Bots.Protocol;

namespace Aion.Bots.World;

/// <summary>NR-110a: the bot's own spirit as SM_SUMMON_PANEL told it.</summary>
/// <param name="HpPercent">NR-110d: its HP as the server last told it: by the panel, and then by every SM_ATTACK_STATUS
/// of the spirit, which carries the HP as a percentage.</param>
public sealed record BotOwnSummon(int ObjectId, int Level, int CurrentHp, int MaxHp, int HpPercent = 100);

/// <summary>NR-110d: a skill the server told the bot's client to have its spirit cast (SM_SUMMON_USESKILL).</summary>
public sealed record BotSummonSkillOrder(int SummonObjectId, ushort SkillId, byte SkillLevel, int TargetObjectId);

public sealed partial class BotWorldModel
{
	private readonly List<BotSummonSkillOrder> summonSkillOrders = [];

	/// <summary>
	/// NR-110a: the spirit the bot has out; null for none. Java SummonsService.createSummon 29-40 tells the master of a
	/// new spirit with SM_SUMMON_PANEL, and its ReleaseSummonTask tells it with SM_RESET_SKILL_COOLDOWN and
	/// SM_SUMMON_OWNER_REMOVE of every end: the spirit's death, the master's death, a release, a master that went out
	/// of the spirit's sight, a timer. A new login starts with none: PlayerLeaveWorldService 117 releases it. Which
	/// spirit it is, is the template of the object with this id.
	/// </summary>
	public BotOwnSummon? Summon { get; private set; }

	/// <summary>
	/// NR-110c: true from the bot's order to attack until its order to guard. It is the bot's own word, sent by
	/// CM_SUMMON_COMMAND; the server keeps the mode and moves nothing by it. While it is true a walk of the bot leaves
	/// the spirit to its fight (BotMover.WeaveSpirit). A new spirit and a spirit that is gone have no order.
	/// </summary>
	public bool SummonFights { get; set; }

	/// <summary>NR-110c: how fast the spirit runs, by the client's own data of its kind; the bot's own walks drive it
	/// by this. No packet says it of a spirit that stands. Null until the journey has looked it up.</summary>
	public float? SummonSpeed { get; set; }

	/// <summary>
	/// NR-110d: the spirit skills the server has asked for and the bot has not answered, oldest first; taking them
	/// empties the list. An order of the master only queues the spirit's skill and tells the master's client with
	/// SM_SUMMON_USESKILL (Java PetOrderUseUltraSkillEffect.applyEffect 28-52). The spirit casts when the client
	/// answers with CM_SUMMON_CASTSPELL (Java CM_SUMMON_CASTSPELL.runImpl 68-75).
	/// </summary>
	public BotSummonSkillOrder[] TakeSummonSkillOrders()
	{
		BotSummonSkillOrder[] taken = [.. summonSkillOrders];
		summonSkillOrders.Clear();
		return taken;
	}

	private void ApplySummonPanel(DecodedBotServerPacket packet)
	{
		int objectId = packet.Get<int>("objectId"), hp = packet.Get<int>("currentHp"), most = packet.Get<int>("maxHp");
		if (Summon?.ObjectId != objectId) ForgetSummonOrders();
		Summon = new(objectId, packet.Get<ushort>("level"), hp, most, most > 0 ? (int)((long)hp * 100 / most) : 100);
	}

	private void ApplySummonOwnerRemove(DecodedBotServerPacket packet)
	{
		if (Summon?.ObjectId == packet.Get<int>("summonObjId")) ForgetSummon();
	}

	private void ForgetSummon()
	{
		Summon = null;
		ForgetSummonOrders();
	}

	private void ForgetSummonOrders()
	{
		(SummonFights, SummonSpeed) = (false, null);
		summonSkillOrders.Clear();
	}

	private void ApplySummonUseSkill(DecodedBotServerPacket packet)
	{
		int summonId = packet.Get<int>("summonId");
		if (Summon?.ObjectId == summonId)
			summonSkillOrders.Add(new(summonId, packet.Get<ushort>("skillId"), packet.Get<byte>("skillLvl"), packet.Get<int>("targetId")));
	}

	// Java SM_ATTACK_STATUS carries a creature's HP as a percentage with every change of it; the types from 19 on
	// are changes of MP and of flight time and carry those.
	private void ApplySummonStatus(DecodedBotServerPacket packet)
	{
		if (Summon is { } spirit && spirit.ObjectId == packet.Get<int>("objectId") && packet.Get<byte>("typeId") < 19)
			Summon = spirit with { HpPercent = packet.Get<byte>("hpOrMp") };
	}
}
