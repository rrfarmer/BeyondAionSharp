using Aion.Commons.Network;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMove : GameServerPacket
{
	public const int PacketOpCode = 55;
	private readonly Player _player;

	public SmMove(Player player)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOVE(Creature).
		_player = player;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MOVE.writeImpl.
		var position = _player.Position;
		var movement = _player.Movement;
		buffer.WriteD(_player.ObjectId);
		buffer.WriteF(position.X);
		buffer.WriteF(position.Y);
		buffer.WriteF(position.Z);
		buffer.WriteC(position.Heading);
		buffer.WriteC(movement.Mask);

		if (MovementMask.HasManualPosition(movement.Mask))
		{
			if (!MovementMask.Has(movement.Mask, MovementMask.Absolute))
			{
				buffer.WriteF(movement.VectorX);
				buffer.WriteF(movement.VectorY);
				buffer.WriteF(movement.VectorZ);
			}
			else
			{
				buffer.WriteF(movement.TargetX);
				buffer.WriteF(movement.TargetY);
				buffer.WriteF(movement.TargetZ);
			}
		}

		if (MovementMask.Has(movement.Mask, MovementMask.Glide))
		{
			buffer.WriteC(movement.GlideFlag);
			if (movement.GlideFlag == GlideFlag.Geyser)
				buffer.WriteC(movement.GeyserLocationId);
		}

		if (MovementMask.Has(movement.Mask, MovementMask.Vehicle))
		{
			buffer.WriteD(movement.VehicleUnk1);
			buffer.WriteD(movement.VehicleUnk2);
			buffer.WriteF(movement.VectorX);
			buffer.WriteF(movement.VectorY);
			buffer.WriteF(movement.VectorZ);
		}
	}
}
