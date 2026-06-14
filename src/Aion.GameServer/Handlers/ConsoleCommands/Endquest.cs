using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Endquest (ginho1, Neon). Completes the specified quest without giving rewards.</summary>
public class Endquest : ConsoleCommand
{
    public Endquest()
        : base("endquest", "Completes a quest.")
    {
        SetSyntaxInfo("<quest> - Completes the specified quest (without giving rewards).");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        VisibleObject target = admin.GetTarget();
        if (target is not Player player)
        {
            SendInfo(admin, "Please select a player.");
            return;
        }

        int questId = ChatUtil.GetQuestId(paramsArr[0]);
        if (questId == 0)
        {
            SendInfo(admin, "Invalid quest link or ID.");
            return;
        }

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            SendInfo(admin, "Quest must be started first.");
            return;
        }
        qs.SetStatus(QuestStatus.COMPLETE);
        qs.SetQuestVar(0);
        if (DataManager.QUEST_DATA.GetQuestById(qs.GetQuestId()).GetRewards().Count != 0)
            qs.SetRewardGroup(0); // follow quests could require reward group > 0 to be unlocked (see quest_data.xml)
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnQuestCompleted(player, qs.GetQuestId());
        player.GetController().UpdateNearbyQuests();
    }
}
