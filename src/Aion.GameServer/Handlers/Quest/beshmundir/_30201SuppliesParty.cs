using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _30201SuppliesParty : AbstractQuestHandler
{
    public _30201SuppliesParty() : base(30201)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798926).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798926).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        if (targetId == 798926)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                long itemCount;
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetQuestVarById(0) == 0)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM && qs.GetQuestVarById(0) == 0)
                {
                    itemCount = player.GetInventory().GetItemCountByItemId(182209601);
                    if (itemCount > 0)
                    {
                        RemoveQuestItem(env, 182209601, 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 5);
                    }
                    else
                        return SendQuestDialog(env, 2716);
                }
                else
                    return SendQuestEndDialog(env);
            }
            else
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
