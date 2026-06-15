using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _30222AGuardsCurse : AbstractQuestHandler
{
    public _30222AGuardsCurse() : base(30222)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798979).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798979).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(216739).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(216239).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798979) // Gelon
            {
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
            int var = qs.GetQuestVarById(0);
            if (targetId == 798979) // Gelon
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 1, 1, true); // reward
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798979) // Gelon
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
        int targetId = env.GetTargetId();

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                // case 216739: { // Warrior Monument
                // Npc npc = (Npc) env.GetVisibleObject();
                // Spawn(216239, player, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                // return true;
                // }
                case 216239: // Ahbana the Wicked
                    return DefaultOnKillEvent(env, 216239, 0, 1); // 1
            }
        }
        return false;
    }
}
