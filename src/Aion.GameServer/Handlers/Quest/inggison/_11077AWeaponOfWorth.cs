using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _11077AWeaponOfWorth : AbstractQuestHandler
{
    public _11077AWeaponOfWorth() : base(11077)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798926).AddOnQuestStart(questId); // Outremus
        qe.RegisterQuestNpc(798926).AddOnTalkEvent(questId); // Outremus
        qe.RegisterQuestNpc(799028).AddOnTalkEvent(questId); // Brontes
        qe.RegisterQuestNpc(798918).AddOnTalkEvent(questId); // Pilipides
        qe.RegisterQuestNpc(798903).AddOnTalkEvent(questId); // Drenia
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798926)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                        if (GiveQuestItem(env, 182214016, 1))
                            return SendQuestStartDialog(env);
                        return true;
                    default:
                    {
                        return SendQuestStartDialog(env);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 799028: // Brontes
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1353);
                        case DialogAction.SELECT2_1:
                            return SendQuestDialog(env, 1353);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                    }
                    return false;
                }
                case 798918: // Pilipides
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1693);
                        case DialogAction.SELECT3_1:
                            return SendQuestDialog(env, 1694);
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2);
                    }
                    return false;
                }
                case 798903: // Drenia
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                            return DefaultCloseDialog(env, 2, 3, true, true);
                    }
                    break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798903) // Drenia
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
