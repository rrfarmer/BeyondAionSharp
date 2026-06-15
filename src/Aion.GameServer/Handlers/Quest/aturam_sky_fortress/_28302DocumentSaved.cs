using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Luzien, Pad
/// </summary>
public class _28302DocumentSaved : AbstractQuestHandler
{
    private int[] npcIds = { 799530, 730375 };
    private int[] generatorIds = { 702650, 702651, 702652, 702653, 702654 };
    private int popuchinId = 217373;

    public _28302DocumentSaved() : base(28302)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcIds[0]).AddOnQuestStart(questId);
        foreach (int npcId in npcIds)
            qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
        foreach (int generatorId in generatorIds)
            qe.RegisterQuestNpc(generatorId).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(popuchinId).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == npcIds[0])
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    PlayQuestMovie(env, 469);
                    return SendQuestStartDialog(env);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == npcIds[1])
            {
                if (qs.GetQuestVarById(0) == 5)
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SET_SUCCEED:
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                        default:
                            return SendQuestDialog(env, 2716);
                    }
                }
            }
            else if (targetId == npcIds[0])
            {
                return SendQuestDialog(env, 1004);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcIds[0])
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    RemoveQuestItem(env, 182212101, 1);
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }

        int targetId = env.GetTargetId();
        int var0 = qs.GetQuestVarById(0);

        if (var0 <= 4)
        {
            foreach (int generatorId in generatorIds)
            {
                if (targetId == generatorId)
                {
                    qs.SetQuestVarById(0, var0 + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
        }
        else if (var0 == 5)
        {
            if (targetId == popuchinId)
                Spawn(730375, player, 374.9f, 424.3f, 653.53f, (byte)0);
        }

        return false;
    }
}
