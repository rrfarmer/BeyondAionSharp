using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/CentralDeckMobileCannonAI (xTz).</summary>
[AIName("centralcannon")]
public class CentralDeckMobileCannonAI : ActionItemNpcAI
{
    public CentralDeckMobileCannonAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (!player.GetInventory().DecreaseByItemId(185000052, 1))
        {
            PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1111302));
            return;
        }

        if (player.GetWorldId() == 300100000)
        {
            WorldMapInstance worldMapInstance = player.GetPosition().GetWorldMapInstance();
            // need check
            // getOwner().getController().useSkill(18572);

            foreach (Npc npc in worldMapInstance.GetNpcs(215402, 215403, 215404, 215405, 230727, 231460, 214968))
            {
                npc.GetController().Die(GetOwner());
            }
            Spawn(215069, 473.178f, 508.966f, 1032.84f, (sbyte)0); // Chief Mate Menekiki
        }
    }
}
