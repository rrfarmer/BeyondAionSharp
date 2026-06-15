using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2841CleansingtheAsteriaChamber : AbstractQuestHandler
{
    public _2841CleansingtheAsteriaChamber() : base(2841)
    {
    }

    public override void Register()
    {
        int[] mobs = { 214752, 214753, 214754, 214755, 214756, 214757, 214758, 214759, 214760, 214761, 214762, 214763, 214764, 214765, 214766, 214767,
            214768, 214769, 214770, 215439, 215440, 215441, 215442, 215443, 215444 };
        qe.RegisterQuestNpc(271068).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(271068).AddOnTalkEvent(questId);
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
            if (targetId == 271068)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 271068)
                return true;
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 271068)
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
            if (player.GetPosition().GetMapId() == 300050000)
            {
                if (qs.GetQuestVarById(0) < 43)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (qs.GetQuestVarById(0) == 43 || qs.GetQuestVarById(0) > 43)
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
