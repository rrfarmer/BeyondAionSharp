using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MOVE (-Nemesiss-). Broadcasts a creature's movement (position/heading/mask, manual target vector, glide flag/geyser, vehicle data). Converges AntiHackService. CreatureMoveController&lt;?&gt;/PlayableMoveController&lt;?&gt;->&lt;Creature&gt; invariance bound; instanceof->is; MovementMask/GlideFlag red-tolerated.</summary>
public class SM_MOVE : AionServerPacket
{
    /// <summary>Object that is moving.</summary>
    private Creature creature;
    private byte movementMask;

    // Reworked WorldNpc random-walk spawn pillar has no faithful Creature/MoveController; this overload
    // serializes the NPC_STARTMOVE wire form (POSITION|MANUAL|ABSOLUTE) directly from the object's
    // position + absolute walk target, byte-identical to the Creature path's else-branch for that mask.
    private readonly bool _rawNpcMove;
    private readonly int _rawObjectId;
    private readonly float _rawX, _rawY, _rawZ;
    private readonly byte _rawHeading;
    private readonly float _rawTargetX, _rawTargetY, _rawTargetZ;

    public SM_MOVE(int objectId, float x, float y, float z, byte heading, byte movementMask, float targetX, float targetY, float targetZ)
    {
        _rawNpcMove = true;
        _rawObjectId = objectId;
        _rawX = x;
        _rawY = y;
        _rawZ = z;
        _rawHeading = heading;
        this.movementMask = movementMask;
        _rawTargetX = targetX;
        _rawTargetY = targetY;
        _rawTargetZ = targetZ;
    }

    public SM_MOVE(Creature creature)
        : this(creature, creature.GetMoveController().GetMovementMask())
    {
    }

    public SM_MOVE(Creature creature, byte movementMask)
    {
        this.creature = creature;
        this.movementMask = movementMask;
    }

    protected override void WriteImpl(AionConnection client)
    {
        if (_rawNpcMove)
        {
            WriteD(_rawObjectId);
            WriteF(_rawX);
            WriteF(_rawY);
            WriteF(_rawZ);
            WriteC(_rawHeading);
            WriteC(movementMask);
            if ((movementMask & MovementMask.POSITION) == MovementMask.POSITION && (movementMask & MovementMask.MANUAL) == MovementMask.MANUAL)
            {
                WriteF(_rawTargetX);
                WriteF(_rawTargetY);
                WriteF(_rawTargetZ);
            }
            return;
        }

        CreatureMoveController mc = creature.GetMoveController();
        PlayableMoveController<Creature> pmc = mc is PlayableMoveController<Creature> ? (PlayableMoveController<Creature>)mc : null;
        WriteD(creature.GetObjectId());
        WriteF(creature.GetX());
        WriteF(creature.GetY());
        WriteF(creature.GetZ());
        WriteC(creature.GetHeading());

        WriteC(movementMask);

        if ((movementMask & MovementMask.POSITION) == MovementMask.POSITION && (movementMask & MovementMask.MANUAL) == MovementMask.MANUAL)
        {
            if (pmc != null && (movementMask & MovementMask.ABSOLUTE) == 0)
            {
                WriteF(pmc.vectorX);
                WriteF(pmc.vectorY);
                WriteF(pmc.vectorZ);
            }
            else
            {
                WriteF(mc.GetTargetX2());
                WriteF(mc.GetTargetY2());
                WriteF(mc.GetTargetZ2());
            }
        }
        if ((movementMask & MovementMask.GLIDE) == MovementMask.GLIDE)
        {
            byte glideFlag = pmc == null ? (byte)0 : pmc.glideFlag;
            WriteC(glideFlag);
            if (glideFlag == GlideFlag.GEYSER)
                WriteC(pmc.geyserLocationId);
        }
        if (pmc != null && (movementMask & MovementMask.VEHICLE) == MovementMask.VEHICLE)
        {
            WriteD(pmc.unk1);
            WriteD(pmc.unk2);
            WriteF(pmc.vectorX);
            WriteF(pmc.vectorY);
            WriteF(pmc.vectorZ);
        }
    }
}
