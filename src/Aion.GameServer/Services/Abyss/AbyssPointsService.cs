using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Services.Abyss;

/// <summary>Java parity: services/abyss/AbyssPointsService (ATracer). Adds abyss points (with big-AP warning + siege hook), broadcasts gain/use system message, legion contribution, and onRankChanged (rank packet, broadcast update, rank-limit item check, skill refresh). Integer newRankingListPosition->int?. SiegeService/Legion/SM_ packets red-tolerated.</summary>
public class AbyssPointsService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AbyssPointsService));

    public static void AddAp(Player player, VisibleObject obj, int value)
    {
        if (value > 30000)
        {
            log.LogWarning("WARN BIG COUNT AP: " + value + " for " + player + " from " + obj);
        }
        AddAp(player, value);
        SiegeService.GetInstance().OnAbyssPointsAdded(player, obj, value);
    }

    public static void AddAp(Player player, int amount)
    {
        if (player == null)
            return;

        int oldAp = player.GetAbyssRank().GetAp();
        AbyssRankEnum oldAbyssRank = player.GetAbyssRank().GetRank();
        player.GetAbyssRank().AddAp(amount);
        int added = player.GetAbyssRank().GetAp() - oldAp;

        SM_SYSTEM_MESSAGE msg = amount >= 0 ? SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_MY_ABYSS_POINT_GAIN(added) : SM_SYSTEM_MESSAGE.STR_MSG_USE_ABYSSPOINT(-added);
        PacketSendUtility.SendPacket(player, msg);
        OnRankChanged(player, added != 0, oldAbyssRank != player.GetAbyssRank().GetRank(), null);
        if (player.IsLegionMember() && added > 0)
        {
            player.GetLegion().AddContributionPoints(added);
            PacketSendUtility.BroadcastToLegion(player.GetLegion(), new SM_LEGION_EDIT(0x03, player.GetLegion()));
        }
    }

    public static void OnRankChanged(Player player, bool abyssPointChanged, bool abyssRankChanged, int? newRankingListPosition)
    {
        if (abyssPointChanged || abyssRankChanged || newRankingListPosition != null)
            PacketSendUtility.SendPacket(player, new SM_ABYSS_RANK(player, newRankingListPosition));
        if (abyssRankChanged)
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ABYSS_RANK_UPDATE(0, player));
            player.GetEquipment().CheckRankLimitItems();
            AbyssSkillService.UpdateSkills(player);
        }
    }
}
