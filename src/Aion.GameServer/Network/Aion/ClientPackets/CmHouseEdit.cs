using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmHouseEdit : GameClientPacket
{
	public const int EnterDecorationMode = 1;
	public const int ExitDecorationMode = 2;
	public const int AddItem = 3;
	public const int DeleteItem = 4;
	public const int SpawnObject = 5;
	public const int MoveObject = 6;
	public const int DespawnObject = 7;
	public const int EnterRenovationMode = 14;
	public const int ExitRenovationMode = 15;
	public const int RenovateBuilding = 16;

	public CmHouseEdit(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Action { get; private set; }

	public int ItemObjectId { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public int Rotation { get; private set; }

	public int BuildingId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_EDIT.readImpl.
		Action = buffer.ReadC();
		switch (Action)
		{
			case AddItem:
			case DeleteItem:
			case DespawnObject:
				ItemObjectId = buffer.ReadD();
				break;
			case SpawnObject:
			case MoveObject:
				ItemObjectId = buffer.ReadD();
				X = buffer.ReadF();
				Y = buffer.ReadF();
				Z = buffer.ReadF();
				Rotation = buffer.ReadH();
				break;
			case RenovateBuilding:
				BuildingId = buffer.ReadD();
				break;
		}
	}
}
