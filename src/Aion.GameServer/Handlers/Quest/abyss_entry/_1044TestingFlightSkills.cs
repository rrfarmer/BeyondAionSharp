using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Speak to Telemachus (203901). Talk with Daedalus (203930) about your flight test. Go through all the rings within the time limit. Talk with
/// Daedalus again. Report to Telemachus.
///
/// @author Hellboy, aion4Free, Hilgert, vlog
/// </summary>
public class _1044TestingFlightSkills : AbstractQuestHandler
{
    private readonly string[] rings = { "ELTNEN_FORTRESS_210020000_1", "ELTNEN_FORTRESS_210020000_2", "ELTNEN_FORTRESS_210020000_3",
        "ELTNEN_FORTRESS_210020000_4", "ELTNEN_FORTRESS_210020000_5", "ELTNEN_FORTRESS_210020000_6" };

    public _1044TestingFlightSkills() : base(1044)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterQuestNpc(203901).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203930).AddOnTalkEvent(questId);
        foreach (string ring in rings)
        {
            qe.RegisterOnPassFlyingRings(ring, questId);
        }
        qe.RegisterOnQuestTimerEnd(questId);
        qe.RegisterOnDie(questId);
        qe.RegisterOnEnterWorld(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203901: // Telemachus
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, 0, 0);
                    }
                    break;
                case 203930: // Daedalus
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            else if (var >= 2 && var <= 7)
                            {
                                return SendQuestDialog(env, 3143);
                            }
                            else if (var == 9)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            else if (var == 10)
                            {
                                return SendQuestDialog(env, 3057);
                            }
                            return false;
                        case DialogAction.SELECT2_1_1:
                            if (var == 1 || var == 10)
                            {
                                PlayQuestMovie(env, 40);
                                return SendQuestDialog(env, 1354);
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            if (var == 1)
                            {
                                QuestService.QuestTimerStart(env, 110);
                                return DefaultCloseDialog(env, 1, 2); // 2
                            }
                            else if (var == 10)
                            {
                                QuestService.QuestTimerStart(env, 110);
                                return DefaultCloseDialog(env, 10, 2); // 2
                            }
                            return false;
                        case DialogAction.SET_SUCCEED:
                            if (var == 9)
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestSelectionDialog(env);
                            }
                            return false;
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203901) // Telemachus
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 1922);
    }

    public override bool OnPassFlyingRingEvent(QuestEnv env, string flyingRing)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (rings[0].Equals(flyingRing))
            {
                ChangeQuestStep(env, 2, 3); // 3
                return true;
            }
            else if (rings[1].Equals(flyingRing))
            {
                ChangeQuestStep(env, 3, 4); // 4
                return true;
            }
            else if (rings[2].Equals(flyingRing))
            {
                ChangeQuestStep(env, 4, 5); // 5
                return true;
            }
            else if (rings[3].Equals(flyingRing))
            {
                ChangeQuestStep(env, 5, 6); // 6
                return true;
            }
            else if (rings[4].Equals(flyingRing))
            {
                ChangeQuestStep(env, 6, 7); // 7
                return true;
            }
            else if (rings[5].Equals(flyingRing))
            {
                ChangeQuestStep(env, 7, 9); // 9
                if (qs.GetQuestVarById(0) == 9)
                    QuestService.QuestTimerEnd(env);
                return true;
            }
        }
        return false;
    }

    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var > 1 && var < 8)
            {
                ChangeQuestStep(env, var, 10); // 10
                return true;
            }
        }
        return false;
    }

    public override bool OnDieEvent(QuestEnv env)
    {
        QuestService.QuestTimerEnd(env);
        return OnQuestTimerEndEvent(env);
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        QuestService.QuestTimerEnd(env);
        return OnQuestTimerEndEvent(env);
    }
}
