using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _24011FunnyFloatingFungus : AbstractQuestHandler
{
    public _24011FunnyFloatingFungus() : base(24011)
    {
    }

    public override void Register()
    {
        int[] talkNpcs = { 203558, 203572, 203558 };
        qe.RegisterQuestNpc(700092).AddOnKillEvent(questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int id in talkNpcs)
            qe.RegisterQuestNpc(id).AddOnTalkEvent(questId);
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        if (qs.GetStatus() != QuestStatus.START)
            return false;
        if (targetId == 700092)
        {
            if (var > 0 && var < 6)
            {
                qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                UpdateQuestStatus(env);
                return true;
            }
            else if (var == 6)
            {
                ChangeQuestStep(env, 6, 6, true); // reward
                return true;
            }
        }
        return false;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203558)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
            else if (targetId == 203572)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SELECT2_1:
                        if (var == 1)
                        {
                            PlayQuestMovie(env, 60);
                            return SendQuestDialog(env, 1353);
                        }
                        return false;
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2); // 2
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203558)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 24010);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 24010);
    }
}
