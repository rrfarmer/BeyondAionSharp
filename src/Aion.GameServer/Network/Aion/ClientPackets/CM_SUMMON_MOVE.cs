using System.Collections.Generic;
using Aion.GameServer.Controllers.Effect;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SkillEngine.Effect;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SUMMON_MOVE (ATracer). Processes summon/mercenary movement packets (position/glide/vehicle masks). CreatureMoveController&lt;? extends Creature&gt; -> &lt;Creature&gt;. SM_MOVE/MovementMask red-tolerated.</summary>
public class CM_SUMMON_MOVE : AionClientPacket
{
    private int objectId;
    private byte type;
    private byte heading;
    private float x, y, z, x2, y2, z2, vehicleX, vehicleY, vehicleZ;
    private byte glideFlag;
    private int unk1, unk2;

    public CM_SUMMON_MOVE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = ReadD();
        x = ReadF();
        y = ReadF();
        z = ReadF();
        heading = ReadC();
        type = ReadC();
        if ((type & MovementMask.POSITION) == MovementMask.POSITION && (type & MovementMask.MANUAL) == MovementMask.MANUAL)
        {
            if ((type & MovementMask.ABSOLUTE) == 0)
            {
                // this type is sent when the summon is in move and it receives or resists movement restricting effects, like stun, stagger, etc.
                // summon's x/y/z is expected to be immediately updated to the sent x/y/z values and no vector or x2/y2/z2 coords are sent
            }
            else
            {
                x2 = ReadF();
                y2 = ReadF();
                z2 = ReadF();
            }
        }
        if ((type & MovementMask.GLIDE) == MovementMask.GLIDE)
        {
            glideFlag = ReadC();
        }
        if ((type & MovementMask.VEHICLE) == MovementMask.VEHICLE)
        {
            unk1 = ReadD();
            unk2 = ReadD();
            vehicleX = ReadF();
            vehicleY = ReadF();
            vehicleZ = ReadF();
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Creature summonOrMercenary = player.GetSummonOrMercenary(objectId);
        if (summonOrMercenary == null || !summonOrMercenary.IsSpawned())
            return;
        EffectController effectController = summonOrMercenary.GetEffectController();
        if (effectController.IsInAnyAbnormalState(AbnormalState.CANT_MOVE_STATE) || effectController.IsUnderFear() || effectController.IsConfused())
            return;
        CreatureMoveController<Creature> m = summonOrMercenary.GetMoveController();
        m.movementMask = type;

        if (m is SummonMoveController smc && (type & MovementMask.GLIDE) == MovementMask.GLIDE)
        {
            smc.glideFlag = glideFlag;
        }

        if (type == MovementMask.IMMEDIATE)
        {
            summonOrMercenary.GetController().OnStopMove();
        }
        else if ((type & MovementMask.POSITION) == MovementMask.POSITION && (type & MovementMask.MANUAL) == MovementMask.MANUAL)
        {
            if ((type & MovementMask.ABSOLUTE) == 0) // skip position update since the server has already set the correct position for stun or resist
                return;
            summonOrMercenary.GetMoveController().SetNewDirection(x2, y2, z2, heading);
            summonOrMercenary.GetController().OnStartMove();
        }
        else
            summonOrMercenary.GetController().OnMove();

        if (m is SummonMoveController smc2 && (type & MovementMask.VEHICLE) == MovementMask.VEHICLE)
        {
            smc2.unk1 = unk1;
            smc2.unk2 = unk2;
            smc2.vehicleX = vehicleX;
            smc2.vehicleY = vehicleY;
            smc2.vehicleZ = vehicleZ;
        }
        World.GetInstance().UpdatePosition(summonOrMercenary, x, y, z, heading);
        m.UpdateLastMove();

        if ((type & MovementMask.POSITION) == MovementMask.POSITION || type == MovementMask.IMMEDIATE)
            PacketSendUtility.BroadcastToSightedPlayers(summonOrMercenary, new SM_MOVE(summonOrMercenary));
    }
}
