using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog TODO: siege weapons attacking event, when siege weapons done
/// </summary>
public class _4702GeneralDeath : AbstractQuestHandler
{
    public _4702GeneralDeath() : base(4702)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278016).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(278016).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(256694).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(256693).AddOnKillEvent(questId); // nochsana general
        qe.RegisterQuestNpc(290161).AddOnKillEvent(questId); // nochsana general
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 278016) // Lisya
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env, 182205676, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 278016) // Lisya
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

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 0)
            {
                return DefaultOnKillEvent(env, 256694, 0, 1); // 1
            }
            else if (var == 1)
            {
                return DefaultOnKillEvent(env, new int[] { 256693, 290161 }, 1, true); // reward
            }
        }
        return false;
    }
}
