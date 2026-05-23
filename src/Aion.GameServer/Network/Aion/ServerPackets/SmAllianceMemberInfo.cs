using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAllianceMemberInfo : GameServerPacket
{
	public const int PacketOpCode = 246;
	private const int FullSlots = 127;

	private readonly PlayerAllianceMemberInfoPacketPlan _plan;

	public SmAllianceMemberInfo(PlayerAllianceMemberInfoPacketPlan plan)
		: base(PacketOpCode)
	{
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ALLIANCE_MEMBER_INFO.writeImpl.
		var prefix = _plan.PrefixSnapshot;
		buffer.WriteD(_plan.AllianceId);
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
		buffer.WriteC(prefix.AllianceUnknown);

		var eventId = (int)_plan.EffectiveEvent;
		if (eventId is 0 or 1 or 3)
			return;

		if (_plan.EffectiveEventKind == PlayerAllianceMemberInfoEventKind.UpdateEffects)
		{
			buffer.WriteD(0);
			buffer.WriteD(0);
			buffer.WriteC(_plan.Slot);
			WriteEffects(buffer);
			WriteSlotTimers(buffer);
			return;
		}

		if (_plan.WritesName)
		{
			buffer.WriteS(prefix.Name);
			if (_plan.EffectiveEventKind == PlayerAllianceMemberInfoEventKind.MemberGroupChange)
				return;

			buffer.WriteD(0);
			buffer.WriteD(0);
			if (_plan.IsOnline)
			{
				buffer.WriteC(FullSlots);
				WriteEffects(buffer);
				WriteSlotTimers(buffer);
			}
			else
			{
				buffer.WriteH(0);
			}

			return;
		}

		throw new NotSupportedException($"Alliance member info event {_plan.EffectiveEvent} is not ported yet.");
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

	private static void WriteSlotTimers(PacketBuffer buffer)
	{
		for (var i = 0; i < 8; i++)
			buffer.WriteD(0);
	}
}
