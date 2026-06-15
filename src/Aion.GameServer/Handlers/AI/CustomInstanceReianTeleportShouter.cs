using System.Collections.Concurrent;
using Aion.GameServer.Ai;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/custom/eternalChallenge/CustomInstanceReianTeleportShouter (@author Estrayl).</summary>
[AIName("custom_instance_reian_teleport_shouter")]
public class CustomInstanceReianTeleportShouter : GeneralNpcAI
{
    private static readonly ConcurrentDictionary<int, long> lastShoutToPlayer = new ConcurrentDictionary<int, long>();

    public CustomInstanceReianTeleportShouter(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (!(creature is Player player) || creature.IsDead() || !GetOwner().CanSee(creature) || !GetOwner().GetPosition().IsMapRegionActive())
        {
            return;
        }
        if (lastShoutToPlayer.TryGetValue(creature.GetObjectId(), out long last)
            && System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - last <= 1800000) // only allow 1 shout half an hour
        {
            return;
        }

        if (PositionUtil.IsInRange(GetOwner(), creature, 15) && GeoService.GetInstance().CanSee(GetOwner(), creature))
        {
            lastShoutToPlayer[creature.GetObjectId()] = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (CustomInstanceService.GetInstance().CanEnter(creature.GetObjectId()))
            {
                PacketSendUtility.SendPacket(player,
                    new SM_MESSAGE(GetOwner(), "Hey you know, use this device to teleport into the 'Eternal Challenge'.", ChatType.NPC));
            }
            else
            {
                PacketSendUtility.SendPacket(player, new SM_MESSAGE(GetOwner(),
                    "I see... you already have shown your potential today. Come again tomorrow if you want to improve.", ChatType.NPC));
            }
        }
    }
}
