using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/Rank100CorridorAI (Estrayl).</summary>
[AIName("rank_100_advance_corridor")]
public class Rank100CorridorAI : AdvanceCorridorAI
{
    public Rank100CorridorAI(Npc owner)
        : base(owner)
    {
        despawnInMin = 5;
    }

    protected override void HandleDialogStart(Player player)
    {
        if ((int)player.GetAbyssRank().GetRank() < (int)AbyssRankEnum.STAR5_OFFICER)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_TELEPOTER_GAB1_USER03());
            return;
        }
        base.HandleDialogStart(player);
    }
}
