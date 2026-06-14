using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Ban;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/BanHdd (ViAl).</summary>
public class BanHdd : AdminCommand
{
    private static string SYNTAX = "Syntax: //banhdd <hdd_serial> <time_in_minutes|0 - infinite>";

    public BanHdd()
        : base("banhdd")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        try
        {
            string hddSerial = paramsArr[0];
            int timeMins = int.Parse(paramsArr[1]);
            if (timeMins == 0)
                timeMins = 10 * 365 * 24 * 60;
            DateTimeOffset banTime = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)timeMins * 60 * 1000);
            HDDBanService.GetInstance().AddBan(hddSerial, banTime);
        }
        catch (Exception)
        {
            PacketSendUtility.SendMessage(player, SYNTAX);
        }
    }
}
