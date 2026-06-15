using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, Majka
/// </summary>
public class _2213PoisonRootPotentFruit : AbstractQuestHandler
{
    public _2213PoisonRootPotentFruit() : base(2213)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203604).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203604).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700057).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203604).AddOnTalkEvent(questId);
        qe.AddHandlerSideQuestDrop(questId, 700057, 182203208, 1, 100);
        qe.RegisterOnGetItem(182203208, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203604)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 700057: // Okaru Tree
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return true; // loot
                    return false;
                case 203604:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        {
                            RemoveQuestItem(env, 182203208, 1);
                            player.GetEffectController().RemoveEffect(255); // Remove Okaru Log Poison effect [ID: 255]
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestEndDialog(env);
                        }
                        else
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203604)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(255, player, player); // Add Okaru Log Poison effect [ID: 255]
        return DefaultOnGetItemEvent(env, 0, 1, false); // 1
    }
}
