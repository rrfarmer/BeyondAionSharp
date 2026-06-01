using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegion : GameClientPacket
{
	public CmLegion(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ExOpcode { get; private set; }

	public short DeputyPermission { get; private set; }

	public short CenturionPermission { get; private set; }

	public short LegionaryPermission { get; private set; }

	public short VolunteerPermission { get; private set; }

	public int Rank { get; private set; }

	public int LegionDominionId { get; private set; }

	public string LegionName { get; private set; } = string.Empty;

	public string CharacterName { get; private set; } = string.Empty;

	public string NewNickname { get; private set; } = string.Empty;

	public string Announcement { get; private set; } = string.Empty;

	public string NewSelfIntro { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION.readImpl.
		ExOpcode = buffer.ReadC();
		switch (ExOpcode)
		{
			case 0x00:
				buffer.ReadD();
				LegionName = buffer.ReadS();
				break;
			case 0x01:
			case 0x04:
			case 0x05:
				buffer.ReadD();
				CharacterName = buffer.ReadS();
				break;
			case 0x02:
			case 0x07:
			case 0x08:
			case 0x0E:
				buffer.ReadD();
				buffer.ReadSignedH();
				break;
			case 0x06:
				Rank = buffer.ReadD();
				CharacterName = buffer.ReadS();
				break;
			case 0x09:
				buffer.ReadD();
				Announcement = buffer.ReadS();
				break;
			case 0x0A:
				buffer.ReadD();
				NewSelfIntro = buffer.ReadS();
				break;
			case 0x0D:
				DeputyPermission = buffer.ReadSignedH();
				CenturionPermission = buffer.ReadSignedH();
				LegionaryPermission = buffer.ReadSignedH();
				VolunteerPermission = buffer.ReadSignedH();
				break;
			case 0x0F:
				CharacterName = buffer.ReadS();
				NewNickname = buffer.ReadS();
				break;
			case 0x10:
				LegionDominionId = buffer.ReadD();
				break;
		}
	}
}
