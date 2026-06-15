using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2842BalaurintheUndergroundFortress : AbstractQuestHandler
{
    public _2842BalaurintheUndergroundFortress() : base(2842)
    {
    }

    public override void Register()
    {
        int[] mobs = { 214771, 214772, 214773, 214774, 214775, 214776, 214777, 214778, 214779, 214780, 214781, 214782, 214783, 214784, 214785, 214786,
            214787, 214788, 214789, 215445, 215446, 215447, 215448, 215449, 215450 };
        qe.RegisterQuestNpc(266568).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(266568).AddOnTalkEvent(questId);
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
        qe.RegisterOnEnterWorld(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 266568)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 266568)
                return true;
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 266568)
        {
            qs.SetQuestVarById(0, 0);
            UpdateQuestStatus(env);
            return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (player.GetPosition().GetMapId() == 300070000)
            {
                if (qs.GetQuestVarById(0) < 38)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (qs.GetQuestVarById(0) == 38 || qs.GetQuestVarById(0) > 38)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
        }
        return false;
    }
}
