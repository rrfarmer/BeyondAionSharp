using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke
/// </summary>
public class _1123WheresTutty : AbstractQuestHandler
{
    public _1123WheresTutty() : base(1123)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(790001).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(790001).AddOnQuestStart(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("QUEST_1123_210010000"), questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 790001)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 1352);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName != ZoneName.Get("QUEST_1123_210010000"))
            return false;
        Player player = env.GetPlayer();
        if (player == null)
            return false;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;
        env.SetQuestId(questId);
        PlayQuestMovie(env, 11);
        qs.SetStatus(QuestStatus.REWARD);
        UpdateQuestStatus(env);
        return true;
    }
}
