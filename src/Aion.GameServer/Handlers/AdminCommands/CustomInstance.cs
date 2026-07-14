using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/CustomInstance (Estrayl). Utility command for the custom instance.</summary>
public class CustomInstance : AdminCommand
{
    public CustomInstance()
        : base("cinstance", "Utility command for the custom instance.")
    {
        SetSyntaxInfo(
            "<removecd> - Removes the custom instance cooldown of selected player.",
            "<getrank> - Gets the current custom instance rank of selected player.",
            "<setrank> [newRank] - Changes the custom instance rank of selected player to given value."
        );
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(player);
            return;
        }

        switch (paramsArr[0].ToLower())
        {
            case "removecd":
                if (player.GetTarget() is Player targetPlayerRemove)
                {
                    if (CustomInstanceService.GetInstance().ResetEntryCooldown(targetPlayerRemove.GetObjectId()))
                    {
                        PacketSendUtility.SendMessage(player, "Successfully removed custom instance cooldown for " + targetPlayerRemove.GetName() + ".");
                    }
                    else
                    {
                        PacketSendUtility.SendMessage(player, "Player " + targetPlayerRemove.GetName() + " does not need a reset.");
                    }
                }
                else
                {
                    PacketSendUtility.SendMessage(player, "Please select a player first.");
                }
                break;
            case "getrank":
                if (player.GetTarget() is Player targetPlayerRank)
                {
                    int rank = CustomInstanceService.GetInstance().LoadOrCreateRank(targetPlayerRank.GetObjectId()).GetRank();
                    PacketSendUtility.SendMessage(player,
                        targetPlayerRank.GetName() + "'s current rank is " + CustomInstanceRankEnumExtensions.GetRankDescription(rank) + "(" + rank + ").");
                }
                else
                {
                    PacketSendUtility.SendMessage(player, "Please select a player first.");
                }
                break;
            case "setrank":
                if (paramsArr.Length < 2)
                {
                    SendInfo(player);
                    return;
                }
                SetNewRank(player, paramsArr[1]);
                break;
        }
    }

    private void SetNewRank(Player player, string newRank)
    {
        int rank;
        if (!TryParseInt(newRank, out rank))
        {
            SendInfo(player, "The new rank have to be a number.");
            return;
        }
        VisibleObject target = player.GetTarget();
        if (player.GetTarget() is Player targetPlayer)
        {
            CustomInstanceService.GetInstance().ChangePlayerRank(targetPlayer.GetObjectId(), rank, 0);
            PacketSendUtility.SendMessage(player,
                "Changed " + target.GetName() + " to " + rank + " which is equivalent to " + CustomInstanceRankEnumExtensions.GetRankDescription(rank));
        }
        else
        {
            SendInfo(player, "Select a player first.");
        }
    }
}
