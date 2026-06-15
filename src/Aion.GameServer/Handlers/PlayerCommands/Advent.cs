using System.Drawing;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Reward;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Advent (Neon).</summary>
public class Advent : PlayerCommand
{
    public Advent()
        : base("advent", "Gets your advent reward for today.")
    {
        SetSyntaxInfo(
            "show - Shows today's reward.",
            "get - Gets your reward for today on this character.\n" + ChatUtil.Color("ATTENTION:", Color.Pink) + " Only one character per account can receive this reward!");
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length != 1)
            SendInfo(player);
        else if ("show".Equals(paramsArr[0], System.StringComparison.OrdinalIgnoreCase))
            AdventService.GetInstance().ShowTodaysReward(player);
        else if ("get".Equals(paramsArr[0], System.StringComparison.OrdinalIgnoreCase))
            AdventService.GetInstance().RedeemReward(player);
    }

    public override bool ValidateAccess(Player player)
    {
        if (!base.ValidateAccess(player))
            return false;
        if (!EventsConfig.ENABLE_ADVENT_CALENDAR)
        {
            if (player.IsStaff())
                SendInfo(player, "The advent calendar is currently disabled.");
            return false;
        }
        if (!AdventService.GetInstance().IsAdventSeason())
        {
            if (player.IsStaff())
                SendInfo(player, "This command is only active during the Advent season.");
            return false;
        }
        return true;
    }
}
