using Aion.Commons.Network;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMove : GameServerPacket
{
	public const int PacketOpCode = 55;
	private readonly int _objectId;
	private readonly WorldPosition _position;
	private readonly byte _movementMask;
	private readonly bool _hasPlayableMoveController;
	private readonly float _targetX;
	private readonly float _targetY;
	private readonly float _targetZ;
	private readonly float _vectorX;
	private readonly float _vectorY;
	private readonly float _vectorZ;
	private readonly byte _glideFlag;
	private readonly int _geyserLocationId;
	private readonly int _vehicleUnk1;
	private readonly int _vehicleUnk2;

	public SmMove(Player player)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOVE(Creature).
		_objectId = player.ObjectId;
		_position = player.Position;
		_movementMask = player.Movement.Mask;
		_hasPlayableMoveController = true;
		_targetX = player.Movement.TargetX;
		_targetY = player.Movement.TargetY;
		_targetZ = player.Movement.TargetZ;
		_vectorX = player.Movement.VectorX;
		_vectorY = player.Movement.VectorY;
		_vectorZ = player.Movement.VectorZ;
		_glideFlag = player.Movement.GlideFlag;
		_geyserLocationId = player.Movement.GeyserLocationId;
		_vehicleUnk1 = player.Movement.VehicleUnk1;
		_vehicleUnk2 = player.Movement.VehicleUnk2;
	}

	public SmMove(WorldNpc npc, WorldNpcWalkerMovementState movementState, byte movementMask = MovementMask.NpcStartMove)
		: this(npc, movementMask, movementState.Target.X, movementState.Target.Y, movementState.Target.Z)
	{
	}

	public SmMove(WorldNpc npc, byte movementMask, float targetX, float targetY, float targetZ)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOVE writes CreatureMoveController targets for non-playable NPC movement.
		_objectId = npc.ObjectId;
		_position = npc.Position;
		_movementMask = movementMask;
		_hasPlayableMoveController = false;
		_targetX = targetX;
		_targetY = targetY;
		_targetZ = targetZ;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MOVE.writeImpl.
		buffer.WriteD(_objectId);
		buffer.WriteF(_position.X);
		buffer.WriteF(_position.Y);
		buffer.WriteF(_position.Z);
		buffer.WriteC(_position.Heading);
		buffer.WriteC(_movementMask);

		if (MovementMask.HasManualPosition(_movementMask))
		{
			if (_hasPlayableMoveController && !MovementMask.Has(_movementMask, MovementMask.Absolute))
			{
				buffer.WriteF(_vectorX);
				buffer.WriteF(_vectorY);
				buffer.WriteF(_vectorZ);
			}
			else
			{
				buffer.WriteF(_targetX);
				buffer.WriteF(_targetY);
				buffer.WriteF(_targetZ);
			}
		}

		if (MovementMask.Has(_movementMask, MovementMask.Glide))
		{
			buffer.WriteC(_hasPlayableMoveController ? _glideFlag : 0);
			if (_hasPlayableMoveController && _glideFlag == GlideFlag.Geyser)
				buffer.WriteC(_geyserLocationId);
		}

		if (_hasPlayableMoveController && MovementMask.Has(_movementMask, MovementMask.Vehicle))
		{
			buffer.WriteD(_vehicleUnk1);
			buffer.WriteD(_vehicleUnk2);
			buffer.WriteF(_vectorX);
			buffer.WriteF(_vectorY);
			buffer.WriteF(_vectorZ);
		}
	}
}
