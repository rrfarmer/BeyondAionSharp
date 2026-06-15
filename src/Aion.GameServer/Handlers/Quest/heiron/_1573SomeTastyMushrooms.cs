using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _1573SomeTastyMushrooms : AbstractQuestHandler
{
    public _1573SomeTastyMushrooms() : base(1573)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182201784, questId);
        qe.RegisterQuestNpc(730025).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(730025).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700194).AddOnTalkEvent(questId);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (player.IsInsideItemUseZone(ZoneName.Get("LF3_ITEMUSEAREA_Q1573")))
            {
                RemoveQuestItem(env, 182201784, 1);
                return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 1, 2, false, 182201735, 1, 0));
            }
        }
        return HandlerResult.SUCCESS;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 730025)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 700194)
            {
                return true;
            }
            if (targetId == 730025)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        if (var == 2)
                        {
                            RemoveQuestItem(env, 182201735, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 10002);
                        }
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 0, 1, false, 0, 10001, 182201784, 1);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 730025)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
