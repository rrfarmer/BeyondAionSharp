using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Collect Bloodwing Meat and lure Vison (798214). Take Bloodwing Meat to Tityus (798191).
/// </summary>
public class _3092VisonTheDrakie : AbstractQuestHandler
{
    public _3092VisonTheDrakie() : base(3092)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798191).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798191).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798214).AddOnTalkEvent(questId);
        qe.RegisterOnLogOut(questId);
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
            if (targetId == 798191) // Tityus
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798214: // Vison
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    return false;
                case 798191:
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 2375);
                    if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    {
                        return CheckQuestItems(env, 1, 2, true, 5, 2716); // reward
                    }
                    if (env.GetDialogActionId() == DialogAction.FINISH_DIALOG)
                        return DefaultCloseDialog(env, 1, 1);
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798191)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
