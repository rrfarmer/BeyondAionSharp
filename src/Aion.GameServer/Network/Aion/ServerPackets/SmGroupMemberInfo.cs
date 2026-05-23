using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmGroupMemberInfo : GameServerPacket
{
	public const int PacketOpCode = 91;
	private const int FullSkillTargetSlots = 127;
	private static readonly int[] JavaSkillTargetSlotIds = [1, 2, 4, 8, 16, 32, 64, 128];
	private readonly PlayerGroupMemberInfoPacketPlan _plan;

	public SmGroupMemberInfo(PlayerGroupMemberInfoPacketPlan plan)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO(PlayerGroup, Player, GroupEvent, int).
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO.writeImpl fixed prefix.
		var prefix = _plan.PrefixSnapshot;
		buffer.WriteD(_plan.GroupId);
		buffer.WriteD(_plan.MemberObjectId);
		buffer.WriteD(prefix.MaxHp ?? 0);
		buffer.WriteD(prefix.CurrentHp);
		buffer.WriteD(prefix.MaxMp ?? 0);
		buffer.WriteD(prefix.CurrentMp);
		buffer.WriteD(prefix.MaxFp ?? 0);
		buffer.WriteD(prefix.CurrentFp);
		buffer.WriteD(prefix.Unknown3Point5);
		buffer.WriteD(prefix.MapId);
		buffer.WriteD(prefix.MapInstanceId);
		buffer.WriteF(prefix.X);
		buffer.WriteF(prefix.Y);
		buffer.WriteF(prefix.Z);
		buffer.WriteC(prefix.ClassId);
		buffer.WriteC(prefix.GenderId);
		buffer.WriteC(prefix.Level);
		buffer.WriteC(prefix.EventId);
		buffer.WriteC(prefix.AlwaysOne);
		buffer.WriteC(prefix.FlyState);
		buffer.WriteC(prefix.MentorFlag);

		if (_plan.EffectiveEvent is PlayerGroupEvent.Movement
			or PlayerGroupEvent.Disconnected
			or PlayerGroupEvent.Leave)
		{
			return;
		}

		if (_plan.EffectiveEvent is PlayerGroupEvent.EnterOffline
			or PlayerGroupEvent.Join)
		{
			buffer.WriteS(prefix.Name);
			return;
		}

		if (_plan.EffectiveEvent is PlayerGroupEvent.Enter
			or PlayerGroupEvent.Update)
		{
			buffer.WriteS(prefix.Name);
			buffer.WriteD(0);
			buffer.WriteD(0);
			buffer.WriteC(FullSkillTargetSlots);
			WriteEffects(buffer);
			WriteSlotTimerPlaceholders(buffer);
			return;
		}

		if (_plan.EffectiveEvent == PlayerGroupEvent.UpdateEffects)
		{
			buffer.WriteD(0);
			buffer.WriteD(0);
			buffer.WriteC(_plan.Slot);
			WriteEffects(buffer);
			WriteSlotTimerPlaceholders(buffer);
			return;
		}

		throw new NotSupportedException(
			$"SM_GROUP_MEMBER_INFO branch {_plan.EffectiveEvent} is not ported yet; name/effect/slot-timer payloads are pending.");
	}

	private void WriteEffects(PacketBuffer buffer)
	{
		var effects = _plan.AbnormalEffects ?? Array.Empty<PlayerGroupMemberEffectInfo>();
		buffer.WriteH(effects.Count);
		foreach (var effect in effects)
		{
			buffer.WriteD(effect.EffectorObjectId);
			buffer.WriteH(effect.SkillId);
			buffer.WriteC(effect.SkillLevel);
			buffer.WriteC(effect.TargetSlotOrdinal);
			buffer.WriteD(effect.RemainingTimeToDisplayMillis);
		}
	}

	private static void WriteSlotTimerPlaceholders(PacketBuffer buffer)
	{
		foreach (var _ in JavaSkillTargetSlotIds)
			buffer.WriteD(0);
	}
}
