using System.Collections.Concurrent;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/ManduriSupplyDeviceAI.
/// Feed and water supply devices of stage 5. They work only while the giant fights and only when two players pull at the same time: the second
/// one to start pulling puts out the feed or water, a player pulling alone gives up when the bar runs out.
/// </summary>
[AIName("empyrean_manduri_supply_device")]
public class ManduriSupplyDeviceAI : ActionItemNpcAI
{
    private readonly ConcurrentDictionary<int, long> pulling = new();
    private readonly AtomicBoolean used = new AtomicBoolean();

    public ManduriSupplyDeviceAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        Npc giant = GetPosition().GetWorldMapInstance().GetNpc(217553);
        if (giant == null || !(giant.GetAi() is ManduriGiantAI ai) || !ai.IsInFight())
            return;
        base.HandleDialogStart(player);
    }

    protected override void HandleUseItemStart(Player player)
    {
        long now = SystemClock.CurrentMillis();
        foreach (var entry in pulling)
        {
            if (now - entry.Value > GetTalkDelayInMs() + 500)
                pulling.TryRemove(entry.Key, out _);
        }
        bool helped = !pulling.IsEmpty && !pulling.ContainsKey(player.GetObjectId());
        if (helped)
        {
            if (used.CompareAndSet(false, true))
            {
                Say(player, 1111304); // Yay, it worked!
                AIActions.HandleUseItemFinish(this, player);
            }
            return;
        }
        pulling[player.GetObjectId()] = now;
        Say(player, 1111303); // Heave-ho...
        base.HandleUseItemStart(player);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        pulling.TryRemove(player.GetObjectId(), out _);
        if (!used.Get())
            Say(player, 1111305); // Too much for one, it takes another one to push it!
    }

    /// <summary>The pulling player says it, not the device.</summary>
    private void Say(Player player, int msgId)
    {
        PacketSendUtility.BroadcastPacket(player, new SM_SYSTEM_MESSAGE(ChatType.NPC, player, msgId), true);
    }
}
