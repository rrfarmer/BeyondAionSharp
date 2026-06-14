using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1376AMountaineOfTrouble : AbstractQuestHandler
{
    public _1376AMountaineOfTrouble() : base(1376)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203947).AddOnQuestStart(questId); // Beramones
        qe.RegisterQuestNpc(203947).AddOnTalkEvent(questId); // Beramones
        qe.RegisterQuestNpc(203964).AddOnTalkEvent(questId); // Agrips
        qe.RegisterQuestNpc(210976).AddOnKillEvent(questId); // Kerubien Hunter
        qe.RegisterQuestNpc(210986).AddOnKillEvent(questId); // Kerubien Hunter
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;

        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203947) // Beramones
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203964) // Agrips
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 1352);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        int[] mobs = { 210976, 210986 };
        if (DefaultOnKillEvent(env, mobs, 0, 6) || DefaultOnKillEvent(env, mobs, 6, true))
            return true;
        else
            return false;
    }
}
