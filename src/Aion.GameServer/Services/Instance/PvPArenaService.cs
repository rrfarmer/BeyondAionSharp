using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Services.Instance;

/// <summary>Java parity: services/instance/PvPArenaService (xTz). Static arena availability checks (time-window + entry-item). DayOfWeek.getValue() ISO (Mon=1..Sun=7) trap→helper (Sunday→7 else (int)); ServerTime.now()→DateTimeOffset, getHour→Hour; arena-type item-count gates. AutoGroupType/Storage red-tolerated.</summary>
public class PvPArenaService
{
    private static int IsoDayValue(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

    public static bool IsPvPArenaAvailable(Player player, AutoGroupType agt)
    {
        if (AutoGroupConfig.START_TIME_ENABLE && !CheckTime(agt))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CLOSED_TIME(agt.GetTemplate().GetInstanceMapId()));
            return false;
        }
        if (!CheckItem(player, agt))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM());
            return false;
        }
        // TODO check cool down
        return true;
    }

    public static bool CheckItem(Player player, AutoGroupType agt)
    {
        Storage inventory = player.GetInventory();
        if (agt.IsPvPFFAArena() || agt.IsPvPSoloArena())
        {
            return inventory.GetItemCountByItemId(186000135) > 0;
        }
        else if (agt.IsHarmonyArena())
        {
            return inventory.GetItemCountByItemId(186000184) > 0;
        }
        else if (agt.IsGloryArena())
        {
            return inventory.GetItemCountByItemId(186000185) >= 3;
        }
        return true;
    }

    private static bool CheckTime(AutoGroupType agt)
    {
        if (agt.IsPvPFFAArena() || agt.IsPvPSoloArena())
        {
            return IsPvPArenaAvailable();
        }
        else if (agt.IsHarmonyArena())
        {
            return IsHarmonyArenaAvailable();
        }
        else if (agt.IsGloryArena())
        {
            return IsGloryArenaAvailable();
        }
        return true;
    }

    private static bool IsPvPArenaAvailable()
    {
        DateTimeOffset now = ServerTime.Now();
        int hour = now.Hour;
        int day = IsoDayValue(now.DayOfWeek);
        if (day == 6 || day == 7)
        {
            return hour == 0 || hour == 1 || (hour >= 10 && hour <= 23);
        }
        return hour == 0 || hour == 1 || hour == 12 || hour == 13 || (hour >= 18 && hour <= 23);
    }

    private static bool IsHarmonyArenaAvailable()
    {
        DateTimeOffset now = ServerTime.Now();
        int hour = now.Hour;
        int day = IsoDayValue(now.DayOfWeek);
        if (day == 6)
            return hour >= 10 || hour == 1 || hour == 2;
        else if (day == 7)
            return hour == 0 || hour == 1 || hour >= 10;
        else
            return (hour >= 10 && hour < 14) || (hour >= 18 && hour <= 23);
    }

    private static bool IsGloryArenaAvailable()
    {
        DateTimeOffset now = ServerTime.Now();
        int hour = now.Hour;
        int day = IsoDayValue(now.DayOfWeek);
        return (day == 6 || day == 7) && hour >= 20 && hour < 22;
    }
}
