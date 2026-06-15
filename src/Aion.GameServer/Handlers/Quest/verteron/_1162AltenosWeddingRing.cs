using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar, Pad
/// </summary>
public class _1162AltenosWeddingRing : AbstractQuestHandler
{
    public _1162AltenosWeddingRing() : base(1162)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203095).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203095).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203093).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700005).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203095)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 700005:
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.USE_OBJECT:
                            {
                                return SendQuestDialog(env, 3739);
                            }
                            case DialogAction.SETPRO1:
                            {
                                if (player.GetInventory().GetItemCountByItemId(182200563) == 0)
                                {
                                    if (!GiveQuestItem(env, 182200563, 1))
                                        return true;
                                }
                                return DefaultCloseDialog(env, 0, 1);
                            }
                        }
                    }
                    return false;
                case 203093:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.USE_OBJECT:
                                return SendQuestDialog(env, 2034);
                            case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                                qs.SetRewardGroup(1);
                                return CheckQuestItems(env, 1, 1, true, 6, 2375);
                            case DialogAction.SETPRO2:
                                return SendQuestDialog(env, 2375);
                        }
                    }
                    return false;
                case 203095:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.USE_OBJECT:
                                return SendQuestDialog(env, 1352);
                            case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                                qs.SetRewardGroup(0);
                                return CheckQuestItems(env, 1, 1, true, 5, 1693);
                            case DialogAction.SETPRO2:
                                return SendQuestDialog(env, 1693);
                        }
                    }
                    return false;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203093)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 6);
                else
                    return SendQuestEndDialog(env);
            }
            else if (targetId == 203095)
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
