using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_ATTACK (alexa026, Avol, ATracer, KID). Player initiates auto-attack on a creature target. Creature/controller red-tolerated.</summary>
public class CM_ATTACK : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_ATTACK));
    /// <summary>Target object id that client wants to TALK WITH or 0 if wants to unselect</summary>
    private int targetObjectId;
    // TODO: Question, are they really needed?
    private int attackno;

    private int time;
    private int type;

    public CM_ATTACK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();// empty
        attackno = ReadUC();// empty
        time = ReadUH();// empty
        type = ReadUC();// empty
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsDead())
            return;

        if (player.IsProtectionActive())
            player.GetController().StopProtectionActiveTask();

        VisibleObject obj = player.GetKnownList().GetObject(targetObjectId);
        if (obj is Creature)
        {
            player.GetController().AttackTarget((Creature)obj, time, false);
        }
        else if (obj != null)
        {
            log.LogWarning(player + " attacking unsupported target " + obj);
        }
    }
}
