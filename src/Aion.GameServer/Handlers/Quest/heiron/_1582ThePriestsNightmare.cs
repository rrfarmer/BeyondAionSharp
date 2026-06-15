using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Balthazar
    /// </summary>
    public class _1582ThePriestsNightmare : AbstractQuestHandler
    {
        public _1582ThePriestsNightmare() : base(1582)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(204560).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(204560).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(700196).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204573).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 204560)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 4762);
                    else
                        return SendQuestStartDialog(env);
                }
            }

            if (qs == null)
                return false;

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 700196:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.USE_OBJECT:
                                {
                                    if (qs.GetQuestVarById(0) == 0)
                                    {
                                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                        UpdateQuestStatus(env);
                                        return true;
                                    }
                                    break;
                                }
                        }
                        return false;
                    case 204560:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    if (qs.GetQuestVarById(0) == 1)
                                    {
                                        return SendQuestDialog(env, 1352);
                                    }
                                    return false;
                                }
                            case DialogAction.SETPRO2:
                                {
                                    if (qs.GetQuestVarById(0) == 1)
                                        return DefaultCloseDialog(env, 1, 2);
                                    break;
                                }
                        }
                        return false;
                    case 204573:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (qs.GetQuestVarById(0) == 2)
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                return false;
                            case DialogAction.SETPRO3:
                                if (qs.GetQuestVarById(0) == 2)
                                {
                                    qs.SetRewardGroup(0); // reward green resurrection stone
                                    return DefaultCloseDialog(env, 2, 2, true, false);
                                }
                                return false;
                            case DialogAction.SETPRO4:
                                if (qs.GetQuestVarById(0) == 2)
                                {
                                    qs.SetRewardGroup(1); // reward white resurrection stone
                                    return DefaultCloseDialog(env, 2, 2, true, false);
                                }
                                return false;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 700196)
                {
                    if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        return SendQuestDialog(env, 5);
                    else
                        return SendQuestEndDialog(env);
                }
            }
            return false;
        }
    }
}
