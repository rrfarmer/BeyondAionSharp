using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Nephis and quest helper team
/// </summary>
public class _1371FlowersForIsson : AbstractQuestHandler
{
    public _1371FlowersForIsson() : base(1371)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203949).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203949).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730039).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        long itemCount = 0;
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203949)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 203949)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        break;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (var == 0)
                            itemCount = player.GetInventory().GetItemCountByItemId(152000601);
                        if (itemCount > 4)
                        {
                            return SendQuestDialog(env, 1353);
                        }
                        else
                        {
                            return SendQuestDialog(env, 1438);
                        }
                    case DialogAction.SETPRO1:
                        RemoveQuestItem(env, 152000601, 5);
                        qs.SetQuestVar(2);
                        UpdateQuestStatus(env);
                        return SendQuestSelectionDialog(env);
                }
            }
            else if (targetId == 730039)
            {
                if (qs.GetStatus() == QuestStatus.START)
                {
                    return UseQuestObject(env, 2, 2, true, false); // reward
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203949)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
