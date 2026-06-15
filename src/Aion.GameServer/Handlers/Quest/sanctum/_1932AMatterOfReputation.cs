using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1932AMatterOfReputation : AbstractQuestHandler
{
    private static readonly int[] npcs = { 203893, 203946 };

    public _1932AMatterOfReputation() : base(1932)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203893).AddOnQuestStart(questId);
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (SendQuestNoneDialog(env, 203893))
            return true;

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203946:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                    }
                    break;
                case 203893:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 2375);
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 1, 2, true, 5, 2716);
                    }
                    break;
            }
        }
        return SendQuestRewardDialog(env, 203893, 0);
    }
}
