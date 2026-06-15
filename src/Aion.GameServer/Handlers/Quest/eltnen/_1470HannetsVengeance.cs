using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Atomics
/// </summary>
public class _1470HannetsVengeance : AbstractQuestHandler
{
    public _1470HannetsVengeance() : base(1470)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(790004).AddOnQuestStart(questId); // Hannet
        qe.RegisterQuestNpc(790004).AddOnTalkEvent(questId); // Hannet
        qe.RegisterQuestNpc(212846).AddOnKillEvent(questId); // Kromede
        qe.RegisterQuestNpc(214621).AddOnKillEvent(questId); // Kromede
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;

        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 790004)
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
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        switch (targetId)
        {
            case 212846:
            case 214621:
                qs.SetQuestVarById(0, var + 1);
                UpdateQuestStatus(env);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                return true;
        }
        return false;
    }
}
