using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author VladimirZ
/// </summary>
public class _2630TheDutifulSpy : AbstractQuestHandler
{
    public _2630TheDutifulSpy() : base(2630)
    {
    }

    public override void Register()
    {
        int[] npcs = { 204799, 204777 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204799).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 204799))
            return true;
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 204777)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1, true, false);
                }
            }
        }
        return SendQuestRewardDialog(env, 204799, 2375);
    }
}
