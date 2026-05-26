using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed record SmAbnormalEffectEntry(
	int EffectorObjectId,
	int SkillId,
	int SkillLevel,
	int TargetSlotId,
	int TargetSlotOrdinal,
	int RemainingTimeToDisplayMillis);

public sealed class SmAbnormalEffect : GameServerPacket
{
	public const int PacketOpCode = 50;
	public const int FullSkillTargetSlots = 127;
	private const int PlayerEffectType = 2;
	private const int CreatureEffectType = 1;

	private readonly int _effectedObjectId;
	private readonly int _effectType;
	private readonly int _abnormals;
	private readonly int _slots;
	private readonly IReadOnlyList<SmAbnormalEffectEntry> _effects;

	public SmAbnormalEffect(
		int effectedObjectId,
		bool effectedIsPlayer,
		int abnormals,
		IReadOnlyList<SmAbnormalEffectEntry> effects,
		int slots = FullSkillTargetSlots)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_ABNORMAL_EFFECT(Creature, int, Collection<Effect>, int).
		_effectedObjectId = effectedObjectId;
		_effectType = effectedIsPlayer ? PlayerEffectType : CreatureEffectType;
		_abnormals = abnormals;
		_slots = slots;
		_effects = slots == FullSkillTargetSlots
			? effects
			: effects.Where(effect => (slots & effect.TargetSlotId) != 0).ToArray();
	}

	public SmAbnormalEffect(
		Player player,
		int abnormals,
		IReadOnlyList<SmAbnormalEffectEntry> effects,
		int slots = FullSkillTargetSlots)
		: this(player.ObjectId, effectedIsPlayer: true, abnormals, effects, slots)
	{
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_ABNORMAL_EFFECT.writeImpl.
		buffer.WriteD(_effectedObjectId);
		buffer.WriteC(_effectType);
		buffer.WriteD(0);
		buffer.WriteD(_abnormals);
		buffer.WriteD(0);
		buffer.WriteC(_slots);
		buffer.WriteH(_effects.Count);

		foreach (var effect in _effects)
		{
			if (_effectType == PlayerEffectType)
				buffer.WriteD(effect.EffectorObjectId);

			buffer.WriteH(effect.SkillId);
			buffer.WriteC(effect.SkillLevel);
			buffer.WriteC(effect.TargetSlotOrdinal);
			buffer.WriteD(effect.RemainingTimeToDisplayMillis);
		}
	}
}
