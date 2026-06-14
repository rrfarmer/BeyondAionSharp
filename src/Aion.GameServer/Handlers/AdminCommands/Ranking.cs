using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Ranking (ATracer).</summary>
public class Ranking : AdminCommand
{
    public Ranking()
        : base("ranking", "Abyss rank control.")
    {
        SetSyntaxInfo("<update> - Runs the daily Abyss rank update task.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 1 && "update".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
            AbyssRankUpdateService.PerformUpdate();
        else
            SendInfo(admin);
    }
}
