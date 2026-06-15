using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/HiddenBirthdayCakeAI (Estrayl).</summary>
[AIName("hidden_cake")]
public class HiddenBirthdayCakeAI : ChestAI
{
    private static readonly ILogger log = NullLogger.Instance;
    private static readonly AtomicInteger collectedCakes = new AtomicInteger();
    private static readonly int JEST_SPAWN_CHANCE = 25;
    private static readonly int[] JEST_SPAWN_IDS = { 210341, 214732, 210595 };
    private static long lastLogTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static volatile int lastCakeCount;

    public HiddenBirthdayCakeAI(Npc owner)
        : base(owner)
    {
    }

    private void LogCollectedCakes(int cakes)
    {
        int deviation = cakes - lastCakeCount;
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (currentTime - lastLogTime >= 3600 * 1000)
        { // Only log once every hour
            log.LogInformation("[EVENT] Total cakes collected: {Cakes}; Cakes collected during the last hour: {Deviation}.", cakes, deviation);
            lastCakeCount = cakes;
            lastLogTime = currentTime;
        }
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.IsInPlayerMode(PlayerMode.RIDE))
            player.UnsetPlayerMode(PlayerMode.RIDE);
        base.HandleDialogStart(player);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (GetOwner().IsInState(CreatureState.DEAD))
            return;
        LogCollectedCakes(collectedCakes.IncrementAndGet());

        if (Rnd.Chance() < JEST_SPAWN_CHANCE)
        {
            Npc npc = (Npc)Spawn(Rnd.Get(JEST_SPAWN_IDS), GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
            npc.GetController().AddTask(TaskId.DESPAWN,
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    npc.GetController().DeleteIfAliveOrCancelRespawn();
                    return ValueTask.CompletedTask;
                }, 120000L));
            PacketSendUtility.SendPacket(player,
                SM_SYSTEM_MESSAGE.STR_MSG_TOYPET_FEED_FOOD_NOT_LOVEFLAVOR(npc.GetObjectTemplate().GetL10n(), GetObjectTemplate().GetL10n()));
        }
        base.HandleUseItemFinish(player);
    }
}
