using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Estrayl
/// </summary>
public class _30722CheckTheGate : AbstractQuestHandler
{
    private const string ZONE_NAME = "DRAGONFALLS_GLARE_220080000";
    private const int START_NPC_ID = 804868; // Rosalee
    private const int END_NPC_ID = 804898; // Werine

    public _30722CheckTheGate() : base(30722)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(START_NPC_ID).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(END_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get(ZONE_NAME), questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == START_NPC_ID)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == END_NPC_ID)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
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

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName != ZoneName.Get(ZONE_NAME))
            return false;

        Player player = env.GetPlayer();
        if (player == null)
            return false;

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (qs.GetQuestVarById(0) == 0)
            {
                ChangeQuestStep(env, 0, 1, true);
                return true;
            }
        }
        return false;
    }
}
