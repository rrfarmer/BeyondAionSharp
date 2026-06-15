using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _4542TheSecretoftheSeirenTreasure : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 204768, 204743, 204808 };

    public _4542TheSecretoftheSeirenTreasure() : base(4542)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204768).AddOnQuestStart(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
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
            if (targetId == 204768) // Sleipnir
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env, 182215327, 1);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 204743: // Rubelik
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            return false;
                        case DialogAction.SELECT1_1:
                            return SendQuestDialog(env, 1012);
                        case DialogAction.SELECT1_2:
                            return SendQuestDialog(env, 1097);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 204768: // Sleipnir
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            if (var == 5)
                                return SendQuestDialog(env, 2716);
                            return false;
                        case DialogAction.SELECT2_1:
                            return SendQuestDialog(env, 1353);
                        case DialogAction.SELECT2_2:
                            return SendQuestDialog(env, 1438);
                        case DialogAction.SETPRO2:
                            RemoveQuestItem(env, 182215327, 1);
                            return DefaultCloseDialog(env, 1, 2, 182215328, 1); // 2
                        case DialogAction.SELECT_QUEST_REWARD:
                            RemoveQuestItem(env, 182215330, 1);
                            return DefaultCloseDialog(env, 5, 5, true, true); // reward
                        case DialogAction.SELECT6_1:
                            return SendQuestDialog(env, 2717);
                        case DialogAction.SETPRO6:
                            ChangeQuestStep(env, 5, 6, true); // 6
                            RemoveQuestItem(env, 182215330, 1);
                            return SendQuestDialog(env, 5); // reward
                    }
                    break;
                case 204808: // Esnu
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                                return SendQuestDialog(env, 1693);
                            if (var == 3)
                                return SendQuestDialog(env, 2034);
                            if (var == 4)
                                return SendQuestDialog(env, 2375);
                            return false;
                        case DialogAction.SELECT3_1:
                            return SendQuestDialog(env, 1694);
                        case DialogAction.SELECT3_2:
                            return SendQuestDialog(env, 1779);
                        case DialogAction.SETPRO3:
                            RemoveQuestItem(env, 182215328, 1);
                            return DefaultCloseDialog(env, 2, 3);
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 3, 4, false, 10000, 10001); // 4
                        case DialogAction.FINISH_DIALOG:
                            if (var == 3)
                                DefaultCloseDialog(env, 3, 3); // 3
                            return false;
                        case DialogAction.SELECT5_1:
                            return SendQuestDialog(env, 2376);
                        case DialogAction.SETPRO5:
                            RemoveQuestItem(env, 182215329, 1);
                            return DefaultCloseDialog(env, 4, 5, 182215330, 1); // 5
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204768) // Sleipnir
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
