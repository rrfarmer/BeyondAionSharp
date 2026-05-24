using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAllianceInfo : GameServerPacket
{
	public const int PacketOpCode = 245;

	private readonly PlayerAllianceInfoPacketPlan _plan;

	public SmAllianceInfo(PlayerAllianceInfoPacketPlan plan)
		: base(PacketOpCode)
	{
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ALLIANCE_INFO.writeImpl header/body plus optional league rows.
		buffer.WriteH(_plan.AllianceGroupSize);
		buffer.WriteD(_plan.AllianceId);
		buffer.WriteD(_plan.LeaderObjectId);
		buffer.WriteD(_plan.ActivePlayerMapId);
		foreach (var viceCaptainObjectId in _plan.PaddedViceCaptainObjectIds)
			buffer.WriteD(viceCaptainObjectId);
		buffer.WriteD((int)_plan.LootRules.LootRule);
		buffer.WriteD(_plan.LootRules.Misc);
		buffer.WriteD(_plan.LootRules.CommonItemAbove);
		buffer.WriteD(_plan.LootRules.SuperiorItemAbove);
		buffer.WriteD(_plan.LootRules.HeroicItemAbove);
		buffer.WriteD(_plan.LootRules.FabledItemAbove);
		buffer.WriteD(_plan.LootRules.EternalItemAbove);
		buffer.WriteD(_plan.LootRules.MythicItemAbove);
		buffer.WriteD(_plan.ConstantGroupInfoMarker);
		buffer.WriteC(_plan.UnknownByte);
		buffer.WriteD(_plan.TeamType);
		buffer.WriteD(_plan.TeamSubType);
		buffer.WriteD(_plan.LeagueId);
		foreach (var group in _plan.GroupPlaceholders)
		{
			buffer.WriteD(group.GroupNumber);
			buffer.WriteD(group.GroupId);
		}

		buffer.WriteD(_plan.MessageId);
		buffer.WriteS(_plan.MessageId != 0 ? _plan.Message : string.Empty);

		if (_plan.LeagueRows.Count > 0)
		{
			buffer.WriteH(_plan.LeagueRows.Count);
			buffer.WriteD((int)_plan.LeagueLootRules.LootRule);
			buffer.WriteD(_plan.LeagueLootRules.Misc);
			buffer.WriteD(_plan.LeagueLootRules.CommonItemAbove);
			buffer.WriteD(_plan.LeagueLootRules.SuperiorItemAbove);
			buffer.WriteD(_plan.LeagueLootRules.HeroicItemAbove);
			buffer.WriteD(_plan.LeagueLootRules.FabledItemAbove);
			buffer.WriteD(_plan.LeagueLootRules.EternalItemAbove);
			buffer.WriteD(_plan.LeagueLootRules.MythicItemAbove);
			buffer.WriteD(_plan.ConstantGroupInfoMarker);
			foreach (var row in _plan.LeagueRows)
			{
				buffer.WriteD(row.AlliancePosition);
				buffer.WriteD(row.AllianceObjectId);
				buffer.WriteD(row.MemberCount);
				buffer.WriteS(row.CaptainName);
				buffer.WriteD(row.CaptainWorldId);
			}
		}
	}
}
