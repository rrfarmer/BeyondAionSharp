using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1626LightThePath : AbstractQuestHandler
{
    public _1626LightThePath() : base(1626)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204592).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204592).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700221).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700222).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700223).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700224).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700225).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700226).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700227).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npcObj)
            targetId = npcObj.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204592)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, env.IsDialogContinuationFromPreQuest() ? 1011 : 4762);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env, 182201788, 1);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 700221:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                long itemCount1 = player.GetInventory().GetItemCountByItemId(182201788);
                                if (itemCount1 == 1)
                                {
                                    return UseQuestObject(env, 0, 1, false, 0); // 1
                                }
                                break;
                            }
                    }
                    break;
                case 700222:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                long itemCount1 = player.GetInventory().GetItemCountByItemId(182201788);
                                if (itemCount1 == 1)
                                {
                                    return UseQuestObject(env, 1, 2, false, 0); // 2
                                }
                                break;
                            }
                    }
                    break;
                case 700223:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                long itemCount1 = player.GetInventory().GetItemCountByItemId(182201788);
                                if (itemCount1 == 1)
                                {
                                    return UseQuestObject(env, 2, 3, false, 0); // 3
                                }
                                break;
                            }
                    }
                    return false;
                case 700224:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                long itemCount1 = player.GetInventory().GetItemCountByItemId(182201788);
                                if (itemCount1 == 1)
                                {
                                    return UseQuestObject(env, 3, 4, false, 0); // 4
                                }
                                break;
                            }
                    }
                    return false;
                case 700225:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                long itemCount1 = player.GetInventory().GetItemCountByItemId(182201788);
                                if (itemCount1 == 1)
                                {
                                    return UseQuestObject(env, 4, 5, false, 0); // 5
                                }
                                break;
                            }
                    }
                    return false;
                case 700226:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                long itemCount1 = player.GetInventory().GetItemCountByItemId(182201788);
                                if (itemCount1 == 1)
                                {
                                    return UseQuestObject(env, 5, 6, false, 0); // 6
                                }
                                break;
                            }
                    }
                    return false;
                case 700227:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                long itemCount1 = player.GetInventory().GetItemCountByItemId(182201788);
                                if (itemCount1 == 1)
                                {
                                    return UseQuestObject(env, 6, 6, true, 0); // reward
                                }
                                break;
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204592)
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
