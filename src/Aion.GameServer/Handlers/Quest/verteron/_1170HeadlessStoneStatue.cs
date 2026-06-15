using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas, vlog
/// </summary>
public class _1170HeadlessStoneStatue : AbstractQuestHandler
{
    public _1170HeadlessStoneStatue() : base(1170)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730000).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(730000).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700033).AddOnTalkEvent(questId);
        qe.RegisterOnGetItem(182200504, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 730000)
            { // Headless Stone Statue
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 700033)
            { // Head of Stone Statue
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return GiveQuestItem(env, 182200504, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 730000)
            { // Headless Stone Statue
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    PlayQuestMovie(env, 16);
                    return CloseDialogWindow(env);
                }
            }
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            ChangeQuestStep(env, 0, 0, true); // reward
            return true;
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId == 16)
            QuestService.FinishQuest(env);
    }
}
