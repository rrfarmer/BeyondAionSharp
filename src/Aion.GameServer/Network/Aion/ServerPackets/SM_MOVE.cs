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
        CreatureMoveController<Creature> mc = creature.GetMoveController();
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
