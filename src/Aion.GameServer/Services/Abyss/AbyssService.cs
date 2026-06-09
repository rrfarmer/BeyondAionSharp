using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Services.Abyss;

/// <summary>Java parity: services/abyss/AbyssService (ATracer).</summary>
public class AbyssService
{
    private static readonly int[] killAnnounceMaps = { 210050000, 210070000, 220070000, 220080000, 400010000, 400020000, 400030000, 400040000, 400050000,
        400060000, 600010000, 600070000, 600090000, 600100000 };

    private static bool ShouldAnnounceHighRankedDeath(Aion.GameServer.Model.GameObjects.Player.Player victim)
    {
        if (victim.GetAbyssRank().GetRank().GetId() >= AbyssRankEnum.GRADE1_SOLDIER.GetId())
        {
            foreach (int map in killAnnounceMaps)
            {
                if (map == victim.GetWorldId())
                    return true;
            }
        }
        return false;
    }

    public static void AnnounceHighRankedDeath(Aion.GameServer.Model.GameObjects.Player.Player victim)
    {
        if (!ShouldAnnounceHighRankedDeath(victim))
            return;
        PacketSendUtility.BroadcastToWorld(Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ABYSS_ORDER_RANKER_DIE(victim),
            p => p != victim && victim.GetWorldType() == p.GetWorldType() && !p.IsInInstance());
    }

    public static void AnnounceAbyssSkillUsage(Aion.GameServer.Model.GameObjects.Player.Player player, string skillL10n)
    {
        PacketSendUtility.BroadcastToWorld(Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SKILL_ABYSS_SKILL_IS_FIRED(player, skillL10n),
            p => p != player && player.GetWorldType() == p.GetWorldType() && !p.IsInInstance());
    }
}
