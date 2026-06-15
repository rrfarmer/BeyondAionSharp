using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1917ALingeringMystery : AbstractQuestHandler
{
    public _1917ALingeringMystery() : base(1917)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203835).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203835).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203075).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (SendQuestNoneDialog(env, 203835))
            return true;

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203075) // Namus
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1352);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1, false, false);
                }
            }
            else if (targetId == 203835) // Seirenia
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1693);
                }
                else
                {
                    int? rewardGroup = env.GetDialogActionId() switch
                    {
                        DialogAction.SETPRO1 => 0,
                        DialogAction.SETPRO2 => 1,
                        DialogAction.SETPRO3 => 2,
                        _ => (int?)null
                    };
                    if (rewardGroup != null)
                    {
                        qs.SetRewardGroup(rewardGroup);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, DialogPageExtensions.GetRewardPageByIndex(qs.GetRewardGroup()).Id());
                    }
                }
            }
        }
        return SendQuestRewardDialog(env, 203835, 0);
    }
}
