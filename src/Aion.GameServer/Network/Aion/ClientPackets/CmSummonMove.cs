using Aion.Commons.Network;
using Aion.GameServer.Controllers.Movement;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSummonMove : GameClientPacket
{
	public CmSummonMove(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SummonObjectId { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public byte Heading { get; private set; }

	public byte Type { get; private set; }

	public float TargetX { get; private set; }

	public float TargetY { get; private set; }

	public float TargetZ { get; private set; }

	public byte GlideFlag { get; private set; }

	public int VehicleUnknown1 { get; private set; }

	public int VehicleUnknown2 { get; private set; }

	public float VehicleX { get; private set; }

	public float VehicleY { get; private set; }

	public float VehicleZ { get; private set; }

	public bool HasManualPosition => MovementMask.HasManualPosition(Type);

	public bool IsAbsolute => MovementMask.Has(Type, MovementMask.ABSOLUTE);

	public bool IsGliding => MovementMask.Has(Type, MovementMask.GLIDE);

	public bool IsVehicle => MovementMask.Has(Type, MovementMask.VEHICLE);

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SUMMON_MOVE.readImpl.
		SummonObjectId = buffer.ReadD();
		X = buffer.ReadF();
		Y = buffer.ReadF();
		Z = buffer.ReadF();
		Heading = buffer.ReadC();
		Type = buffer.ReadC();

		if (HasManualPosition && IsAbsolute)
		{
			TargetX = buffer.ReadF();
			TargetY = buffer.ReadF();
			TargetZ = buffer.ReadF();
		}

		if (IsGliding)
			GlideFlag = buffer.ReadC();

		if (IsVehicle)
		{
			VehicleUnknown1 = buffer.ReadD();
			VehicleUnknown2 = buffer.ReadD();
			VehicleX = buffer.ReadF();
			VehicleY = buffer.ReadF();
			VehicleZ = buffer.ReadF();
		}
	}
}
