using System.Collections.Generic;

using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/engulfedOphidianBridgeInstance/ExplosionDeviceAI (@author cheatkiller).</summary>
[AIName("engulfedophidianexplosiondevice")]
public class ExplosionDeviceAI : ActionItemNpcAI
{
    private readonly List<Npc> bomb = new List<Npc>();

    public ExplosionDeviceAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (CheckScroll(player))
        {
            switch (GetOwner().GetNpcId())
            {
                case 701969:
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701939 : 701953, 660.36865f, 461.0626f, 600.2547f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701939 : 701953, 667.22784f, 488.69467f, 599.8417f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701939 : 701953, 670.0791f, 458.5767f, 599.75f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701939 : 701953, 671.87946f, 472.70087f, 600.772f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701939 : 701953, 679.0547f, 488.28452f, 599.75f, (sbyte)116));
                    PacketSendUtility.BroadcastToMap(GetOwner(), 1402057);
                    break;
                case 701970:
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701940 : 701954, 539.50714f, 430.42578f, 620.25f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701940 : 701954, 540.8305f, 438.79446f, 620.25f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701940 : 701954, 544.2634f, 448.85068f, 620.19464f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701940 : 701954, 535.0376f, 449.98453f, 620.25f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701940 : 701954, 532.6748f, 441.45563f, 620.25f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701940 : 701954, 528.1262f, 448.87216f, 620.3671f, (sbyte)116));
                    PacketSendUtility.BroadcastToMap(GetOwner(), 1402067);
                    break;
                case 701971:
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701941 : 701955, 598.453f, 569.7365f, 590.91034f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701941 : 701955, 608.8183f, 568.04224f, 590.6276f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701941 : 701955, 616.0901f, 560.89703f, 590.6867f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701941 : 701955, 614.1525f, 547.63904f, 590.625f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701941 : 701955, 603.2911f, 542.8298f, 590.625f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701941 : 701955, 593.12506f, 547.4969f, 590.625f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701941 : 701955, 591.4903f, 559.3725f, 590.625f, (sbyte)116));
                    PacketSendUtility.BroadcastToMap(GetOwner(), 1402062);
                    break;
                case 701972:
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701942 : 701956, 477.32898f, 537.0476f, 597.375f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701942 : 701956, 482.75482f, 546.7067f, 597.5f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701942 : 701956, 486.86075f, 523.6781f, 597.375f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701942 : 701956, 492.95834f, 533.3082f, 598.8186f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701942 : 701956, 493.98892f, 549.46606f, 597.6485f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701942 : 701956, 503.4563f, 526.20776f, 597.5f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701942 : 701956, 505.39758f, 551.0327f, 597.7016f, (sbyte)116));
                    bomb.Add((Npc)Spawn(player.GetRace() == Race.ELYOS ? 701942 : 701956, 508.31046f, 539.70984f, 598.1651f, (sbyte)116));
                    PacketSendUtility.BroadcastToMap(GetOwner(), 1402072);
                    break;
            }
            Boom();
            player.GetInventory().DecreaseByItemId(164000278, 1);
        }
        else
        {
            PacketSendUtility.BroadcastToMap(GetOwner(), 1402005);
        }
    }

    private void Boom()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            foreach (Npc npc in bomb)
            {
                npc.GetController().UseSkill(21178);
            }
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 5000L);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            foreach (Npc npc in bomb)
            {
                npc.GetController().Delete();
            }
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 20000L);
    }

    private bool CheckScroll(Player player)
    {
        Item key = player.GetInventory().GetFirstItemByItemId(164000278);
        if (key != null && key.GetItemCount() >= 1)
        {
            return true;
        }
        return false;
    }
}
