using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/GovernorCorridorAI (Estrayl).</summary>
[AIName("governor_advance_corridor")]
public class GovernorCorridorAI : AdvanceCorridorAI
{
    public GovernorCorridorAI(Npc owner)
        : base(owner)
    {
        despawnInMin = 5;
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.GetAbyssRank().GetRank() != AbyssRankEnum.SUPREME_COMMANDER)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_TELEPOTER_GAB1_USER05());
            return;
        }
        base.HandleDialogStart(player);
    }
}
