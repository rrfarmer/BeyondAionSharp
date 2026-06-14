using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke
/// </summary>
public class _1000Prologue : AbstractQuestHandler
{
    public _1000Prologue() : base(1000)
    {
    }

    public override void Register()
    {
        qe.RegisterOnEnterWorld(questId);
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        if (player.GetRace() == Race.ELYOS && !player.GetQuestStateList().HasQuest(questId))
        {
            env.SetQuestId(questId);
            if (QuestService.StartQuest(env) || player.GetQuestStateList().GetQuestState(questId).GetStatus() == QuestStatus.START)
            {
                PlayQuestMovie(env, 1, true);
                return true;
            }
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId != 1)
            return;
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return;
        qs.SetStatus(QuestStatus.REWARD);
        QuestService.FinishQuest(env);
    }
}
