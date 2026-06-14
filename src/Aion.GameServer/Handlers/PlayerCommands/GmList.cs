using System.Text;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/GmList (Aion Gates, Neon). Lists online, whisper-enabled team members.</summary>
public class GmList : PlayerCommand
{
    public GmList()
        : base("gmlist", "Lists all available team members.")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        StringBuilder sb = new StringBuilder();
        int count = 0;

        foreach (Player gm in GMService.GetInstance().GetOnlineStaffMembers())
        {
            FriendList.Status status = gm.GetFriendList().GetStatus();
            if (!gm.IsInCustomState(CustomPlayerState.NO_WHISPERS_MODE) && status != FriendList.Status.OFFLINE)
            {
                sb.Append("\n\t" + ChatUtil.Name(gm) + " (" + status.ToString().ToLower() + ")");
                count++;
            }
        }

        if (count == 0)
        {
            SendInfo(player, "There is no GM online.");
            return;
        }
        SendInfo(player, "GMs online (" + count + "):" + sb.ToString());
    }
}
