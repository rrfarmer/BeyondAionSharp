using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author VladimirZ
/// </summary>
public class _21027FearlessKantele : AbstractQuestHandler
{
    public _21027FearlessKantele() : base(21027)
    {
    }

    public override void Register()
    {
        int[] npcs = { 799254, 799255 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799254).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 799254, 4762))
            return true;

        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 799255)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        else if (var == 1)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 1, 2, true, 10000, 10001);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
        }
        return SendQuestRewardDialog(env, 799254, 10002);
    }
}
