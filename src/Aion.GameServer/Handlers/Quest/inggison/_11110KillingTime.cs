using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Kill Enemies of the Abandoned Jotun Studio (217039, 217040) (10). Talk with Suleion (799075).
///
/// @author vlog
/// </summary>
public class _11110KillingTime : AbstractQuestHandler
{
    public _11110KillingTime() : base(11110)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(217039).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(217040).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(799075).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799075).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (env.GetTargetId() == 799075)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (env.GetTargetId() == 799075)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 1352);
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
            int[] mobs = { 217039, 217040 };
            if (var >= 0 && var < 9)
            {
                return DefaultOnKillEvent(env, mobs, 0, 9);
            }
            else if (var == 9)
            {
                return DefaultOnKillEvent(env, mobs, 9, true);
            }
        }
        return false;
    }
}
