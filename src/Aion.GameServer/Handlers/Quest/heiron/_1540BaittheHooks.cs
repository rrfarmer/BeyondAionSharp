using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1540BaittheHooks : AbstractQuestHandler
{
    public _1540BaittheHooks() : base(1540)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204588).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204588).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730189).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730190).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730191).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204588)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env, 182201822, 1);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 730189:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (player.GetInventory().GetItemCountByItemId(182201822) == 1)
                            {
                                return UseQuestObject(env, 0, 1, false, 0); // 1
                            }
                            break;
                        }
                    }
                    return false;
                case 730190:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (player.GetInventory().GetItemCountByItemId(182201822) == 1)
                            {
                                return UseQuestObject(env, 1, 2, false, 0); // 2
                            }
                            break;
                        }
                    }
                    return false;
                case 730191:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (qs.GetQuestVarById(0) == 2 && player.GetInventory().GetItemCountByItemId(182201822) == 1)
                            {
                                qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                RemoveQuestItem(env, 182201822, 1);
                                return true;
                            }
                            break;
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204588)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
