using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1194ReducingTursinStrength : AbstractQuestHandler
{
    public _1194ReducingTursinStrength() : base(1194)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203098).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203098).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(210185).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(210186).AddOnKillEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("TURSIN_GARRISON_210030000"), questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203098)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
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
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203098)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if (zoneName != ZoneName.Get("TURSIN_GARRISON_210030000"))
            return false;

        Player player = env.GetPlayer();
        if (player == null)
            return false;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null)
            return false;

        if (qs.GetQuestVarById(0) == 0)
        {
            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
            UpdateQuestStatus(env);
            return true;
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

        int targetId = 0;
        int questVar = qs.GetQuestVarById(0);

        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (targetId == 210185 || targetId == 210186)
        {
            if (questVar >= 1 && questVar < 10)
            {
                if (questVar == 9)
                    qs.SetStatus(QuestStatus.REWARD);
                else
                    qs.SetQuestVarById(0, questVar + 1);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }
}
