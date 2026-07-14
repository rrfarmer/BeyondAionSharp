using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Deletecquest (ginho1, Neon). Deletes a quest from the target's quest list.</summary>
public class Deletecquest : ConsoleCommand
{
    public Deletecquest()
        : base("deletecquest", "Deletes a quest from the players quest list.")
    {
        SetSyntaxInfo("<3> <quest> - Deletes the quest from the targets quest list.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        VisibleObject target = admin.GetTarget();
        if (target is not Player player)
        {
            PacketSendUtility.SendMessage(admin, "Please select a player.");
            return;
        }

        // Java parity: Integer.valueOf(params[0]) throws NumberFormatException -> sendInfo(admin).
        if (!TryParseInt(paramsArr[0], out int questId))
        {
            SendInfo(admin);
            return;
        }

        QuestState qs = player.GetQuestStateList().DeleteQuest(questId);
        if (qs == null)
        {
            SendInfo(admin, "Player " + player.GetName() + " does not have that quest.");
            return;
        }
        if (qs.GetStatus() == QuestStatus.COMPLETE)
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().SendCompletedQuests(player); // rewrite completed quest list
        else
            PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ABANDON, qs));
        player.GetController().UpdateNearbyQuests();
    }
}
