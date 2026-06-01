using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmFindGroup : GameClientPacket
{
	public CmFindGroup(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Action { get; private set; }
	public int PlayerOrTeamId { get; private set; }
	public int BannedPlayerId { get; private set; }
	public string? Message { get; private set; }
	public int GroupType { get; private set; }
	public int ClassId { get; private set; }
	public int Level { get; private set; }
	public byte ServerId { get; private set; }
	public byte Unknown1 { get; private set; }
	public byte Unknown2 { get; private set; }
	public byte Unknown3 { get; private set; }
	public int InstanceMaskId { get; private set; }
	public int MinMembers { get; private set; }
	public byte InstanceApplicationReply { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_FIND_GROUP.readImpl.
		Action = buffer.ReadC();

		switch (Action)
		{
			case 0:
			case 4:
			case 10:
			case 13:
			case 20:
				break;
			case 1:
				PlayerOrTeamId = buffer.ReadD();
				ServerId = buffer.ReadC();
				Unknown1 = buffer.ReadC();
				Unknown2 = buffer.ReadC();
				Unknown3 = buffer.ReadC();
				break;
			case 2:
				PlayerOrTeamId = buffer.ReadD();
				Message = buffer.ReadS();
				GroupType = buffer.ReadC();
				break;
			case 3:
				PlayerOrTeamId = buffer.ReadD();
				ServerId = buffer.ReadC();
				Unknown1 = buffer.ReadC();
				Unknown2 = buffer.ReadC();
				Unknown3 = buffer.ReadC();
				Message = buffer.ReadS();
				GroupType = buffer.ReadC();
				break;
			case 5:
				PlayerOrTeamId = buffer.ReadD();
				break;
			case 6:
			case 7:
				PlayerOrTeamId = buffer.ReadD();
				Message = buffer.ReadS();
				GroupType = buffer.ReadC();
				ClassId = buffer.ReadC();
				Level = buffer.ReadC();
				break;
			case 8:
				InstanceMaskId = buffer.ReadD();
				_ = buffer.ReadC();
				Message = buffer.ReadS();
				MinMembers = buffer.ReadC();
				break;
			case 9:
			case 11:
			case 15:
				PlayerOrTeamId = buffer.ReadD();
				InstanceMaskId = buffer.ReadD();
				break;
			case 12:
				PlayerOrTeamId = buffer.ReadD();
				InstanceApplicationReply = buffer.ReadC();
				break;
			case 17:
				PlayerOrTeamId = buffer.ReadD();
				InstanceMaskId = buffer.ReadD();
				Message = buffer.ReadS();
				break;
			case 25:
				PlayerOrTeamId = buffer.ReadD();
				InstanceMaskId = buffer.ReadD();
				BannedPlayerId = buffer.ReadD();
				break;
		}
	}
}
