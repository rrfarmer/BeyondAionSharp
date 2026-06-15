using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _3329DinnersonMe : AbstractQuestHandler
{
    private static readonly int[] mob_ids = { 210887, 210912, 210914, 210932 };

    public _3329DinnersonMe() : base(3329)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203909).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203909).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203956).AddOnTalkEvent(questId);
        foreach (int mob_id in mob_ids)
            qe.RegisterQuestNpc(mob_id).AddOnKillEvent(questId);
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        int[] mobs1 = { 210887, 210912 };
        int[] mobs2 = { 210914, 210932 };
        if (DefaultOnKillEvent(env, mobs1, 0, 4, 0) || DefaultOnKillEvent(env, mobs2, 0, 6, 1))
            return true;
        else
            return false;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        int dialogActionId = env.GetDialogActionId();
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203909)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203956)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203956)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
