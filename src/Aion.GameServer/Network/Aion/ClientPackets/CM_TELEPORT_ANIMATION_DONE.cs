using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>
/// Java parity: network/aion/clientpackets/CM_TELEPORT_ANIMATION_DONE (Rolandas, Neon). Sent when the teleport animation finishes; runs the deferred TELEPORT task now and surfaces any error.
/// Java RunnableFuture run()/get() + InterruptedException|ExecutionException multi-catch -> ScheduledTask Run()/Get() + single catch with e.InnerException as getCause(); concurrency model adapted. World/SM_PLAYER_INFO red-tolerated.
/// </summary>
public class CM_TELEPORT_ANIMATION_DONE : AionClientPacket
{
    public CM_TELEPORT_ANIMATION_DONE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        var task = player.GetController().GetAndRemoveTask(TaskId.TELEPORT);
        if (task != null && !task.IsDone())
            try
            {
                task.Run(); // run now since it's not started yet
                task.Get(); // get to throw exception, if any
            }
            catch (Exception e)
            {
                NullLoggerFactory.Instance.CreateLogger(nameof(CM_TELEPORT_ANIMATION_DONE)).LogError(e.InnerException, "");
                if (!player.IsSpawned())
                {
                    PacketSendUtility.SendPacket(player, new SM_PLAYER_INFO(player));
                    global::Aion.GameServer.World.World.GetInstance().Spawn(player);
                }
            }
    }
}
