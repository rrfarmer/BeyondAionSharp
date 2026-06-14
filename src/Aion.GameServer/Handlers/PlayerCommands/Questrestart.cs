using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Questrestart (ginho1, Neon). Restarts a bugged, currently-active quest.</summary>
public class Questrestart : PlayerCommand
{
    public Questrestart()
        : base("questrestart", "Restarts a bugged Quest.")
    {
        SetSyntaxInfo("<quest link|ID> - Restarts the specified quest.");
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        int id = ChatUtil.GetQuestId(paramsArr[0]);
        if (id == 0)
        {
            SendInfo(player, "Invalid quest.");
            return;
        }

        QuestState qs = player.GetQuestStateList().GetQuestState(id);
        if (qs == null || (qs.GetStatus() != QuestStatus.START && qs.GetStatus() != QuestStatus.REWARD))
        {
            SendInfo(player, "Only currently active quests can be restarted.");
            return;
        }

        if (id == 1006 || id == 2008 || (qs.GetStatus() == QuestStatus.REWARD && qs.GetQuestVars().GetQuestVars() == 1))
        {
            SendInfo(player, "Quest " + ChatUtil.Quest(id) + " can't be restarted.");
            return;
        }

        if (qs.GetQuestVarById(0) == 0)
        {
            SendInfo(player, "Restarting quest " + ChatUtil.Quest(id) + " would have no effect.");
            return;
        }

        qs.SetStatus(QuestStatus.START);
        qs.SetQuestVar(0);
        qs.SetFlags(0);
        qs.SetRewardGroup(null);
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
        SendInfo(player, "Quest " + ChatUtil.Quest(id) + " was restarted.");
    }
}
