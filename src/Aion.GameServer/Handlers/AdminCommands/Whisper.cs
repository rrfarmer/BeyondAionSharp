using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Whisper.</summary>
public class Whisper : AdminCommand
{
    public Whisper()
        : base("whisper", "Enables/disables incoming whispers.")
    {
        SetSyntaxInfo("<on|off> - Enable or disable whispers from others (GMs can always whisper you).");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if (paramsArr[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            admin.SetCustomState(CustomPlayerState.NO_WHISPERS_MODE);
            SendInfo(admin, "Accepting whispers: OFF");
        }
        else if (paramsArr[0].Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            admin.UnsetCustomState(CustomPlayerState.NO_WHISPERS_MODE);
            SendInfo(admin, "Accepting whispers: ON");
        }
    }
}
