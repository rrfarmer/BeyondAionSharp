using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _21105CoweringRefugee : AbstractQuestHandler
{
    public _21105CoweringRefugee() : base(21105)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799276).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799276).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700812).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799366).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799276)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env, 182207857, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799366)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    Npc npc = (Npc)env.GetVisibleObject();
                    npc.GetController().Delete();
                    RemoveQuestItem(env, 182207857, 1);
                    return DefaultCloseDialog(env, 0, 1, true, false);
                }
            }
            else if (targetId == 700812)
            {
                Npc npc = (Npc)env.GetVisibleObject();
                SpawnForFiveMinutes(Rnd.NextBoolean() ? 799366 : 216086, npc.GetPosition());
                npc.GetController().DeleteAndScheduleRespawn();
                return true;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799276)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
