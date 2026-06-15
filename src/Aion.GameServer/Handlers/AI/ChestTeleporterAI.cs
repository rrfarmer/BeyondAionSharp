using System.Collections.Generic;

using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theHexway/ChestTeleporterAI (@author Sykra).</summary>
[AIName("chest_teleporter")]
public class ChestTeleporterAI : ActionItemNpcAI
{
    private readonly List<int> recievedShouts = new List<int>();

    public ChestTeleporterAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (player.GetWorldId() == 300700000)
            TeleportService.TeleportTo(player, 300700000, 485.59f, 585.42f, 357f, (byte)60);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (!(creature is Player) || creature.IsDead() || !GetOwner().CanSee(creature) || !GetOwner().GetPosition().IsMapRegionActive())
            return;
        lock (recievedShouts)
        {
            if (!recievedShouts.Contains(creature.GetObjectId()))
            {
                if (PositionUtil.IsInRange(GetOwner(), creature, 8) && GeoService.GetInstance().CanSee(GetOwner(), creature))
                {
                    recievedShouts.Add(creature.GetObjectId());
                    PacketSendUtility.SendPacket((Player)creature,
                        new SM_MESSAGE(GetOwner(), "A mysterious orb of relocation is hidden in this chest", ChatType.NPC));
                }
            }
        }
    }
}
