using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Extensions;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/AddTitle (xavier). Adds titles to players.</summary>
public class AddTitle : AdminCommand
{
    public AddTitle()
        : base("addtitle", "Adds titles to players.")
    {
        SetSyntaxInfo("<titleId> [playerName] - Adds the title to your target or the specified player.");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 1 || paramsArr.Length > 2)
        {
            SendInfo(player);
            return;
        }

        TitleTemplate titleTemplate = DataManager.TITLE_DATA.GetTitleTemplate(int.TryParse(paramsArr[0], out int titleIdParam) ? titleIdParam : 0);
        if (titleTemplate == null)
        {
            SendInfo(player, "Invalid title id.");
            return;
        }

        Player target = null;
        if (paramsArr.Length == 2)
        {
            string playerName = Util.ConvertName(paramsArr[1]);
            target = Aion.GameServer.World.World.GetInstance().GetPlayer(playerName);
            if (target == null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
                return;
            }
        }
        else
        {
            VisibleObject creature = player.GetTarget();
            if (player.GetTarget() is Player)
            {
                target = (Player)creature;
            }

            if (target == null)
            {
                target = player;
            }
        }

        if (!target.GetTitleList().AddTitle(titleTemplate.GetTitleId(), false, 0))
        {
            if (!target.Equals(player))
                SendInfo(player, "Couldn't add title \"" + titleTemplate.GetL10n() + "\" to " + target);
        }
        else
        {
            if (!target.Equals(player))
            {
                SendInfo(player, "Added title \"" + titleTemplate.GetL10n() + "\" to " + target);
                SendInfo(target, player.GetName(true) + " gave you the title \"" + titleTemplate.GetL10n() + "\"");
            }
        }
    }
}
