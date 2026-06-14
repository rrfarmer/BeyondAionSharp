using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Ahserion (Yeats, Neon).</summary>
public class Ahserion : AdminCommand
{
    public Ahserion()
        : base("ahserion", "Starts/stops Ahserions Flight.")
    {
        SetSyntaxInfo(
            "<start> - Starts Ahserions Flight.",
            "<stop> - Stops Ahserions Flight.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if (paramsArr[0].Equals("start", System.StringComparison.OrdinalIgnoreCase))
        {
            if (PanesterraService.GetInstance().IsAhserionRaidStarted())
            {
                SendInfo(admin, "Ahserion's Flight is already running.");
            }
            else
            {
                PanesterraService.GetInstance().StartAhserionRaid();
                SendInfo(admin, "Started Ahserion's Flight.");
            }
        }
        else if (paramsArr[0].Equals("stop", System.StringComparison.OrdinalIgnoreCase))
        {
            if (!PanesterraService.GetInstance().IsAhserionRaidStarted())
            {
                SendInfo(admin, "Ahserion's Flight is not running.");
            }
            else
            {
                PanesterraService.GetInstance().StopAhserionRaid();
                SendInfo(admin, "Stopped Ahserion's Flight.");
            }
        }
    }
}
