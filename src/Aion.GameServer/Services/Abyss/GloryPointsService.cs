using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Abyss;

/// <summary>Java parity: services/abyss/GloryPointsService (ViAl, Sykra). Adds/removes glory points: offline players via AbyssRankDAO, online players update AbyssRank + gain/lose system message + abyss-rank packet. AbyssRankDAO/SM_ packets red-tolerated.</summary>
public class GloryPointsService
{
    private GloryPointsService()
    {
    }

    public static void AddGp(int playerObjId, int amount)
    {
        if (amount == 0)
            return;
        Player player = World.GetInstance().GetPlayer(playerObjId);
        bool addToStats = amount > 0;
        if (player == null)
        {
            AbyssRankDAO.AddGp(playerObjId, amount, addToStats);
        }
        else
        {
            int oldGp = player.GetAbyssRank().GetCurrentGP();
            player.GetAbyssRank().AddGp(amount, addToStats);
            int added = player.GetAbyssRank().GetCurrentGP() - oldGp;

            SM_SYSTEM_MESSAGE msg = amount >= 0 ? SM_SYSTEM_MESSAGE.STR_MSG_GLORY_POINT_GAIN(added) : SM_SYSTEM_MESSAGE.STR_MSG_GLORY_POINT_LOSE(-added);
            PacketSendUtility.SendPacket(player, msg);
            if (added != 0)
                PacketSendUtility.SendPacket(player, new SM_ABYSS_RANK(player));
        }
    }
}
