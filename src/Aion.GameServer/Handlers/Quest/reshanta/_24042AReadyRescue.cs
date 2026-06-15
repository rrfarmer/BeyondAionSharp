using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _24042AReadyRescue : AbstractQuestHandler
    {
        private static readonly int[] npcs = { 278002, 278019, 278088, 253626 };

        public _24042AReadyRescue() : base(24042)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
            qe.RegisterOnLogOut(questId);
            qe.RegisterAddOnReachTargetEvent(questId);
            qe.RegisterAddOnLostTargetEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;
            int var = qs.GetQuestVarById(0);
            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 278002: // Jebal
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    case 278019: // Lakadi
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                    return SendQuestDialog(env, 1352);
                                return false;
                            case DialogAction.SETPRO2:
                                return DefaultCloseDialog(env, 1, 2); // 2
                        }
                        break;
                    case 278088: // Glati
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                    return SendQuestDialog(env, 1693);
                                return false;
                            case DialogAction.SETPRO3:
                                return DefaultCloseDialog(env, 2, 3); // 3
                        }
                        break;
                    case 253626: // Captured Asmodian Prisoner
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 3)
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                return false;
                            case DialogAction.SELECT4_1:
                                PlayQuestMovie(env, 294);
                                return SendQuestDialog(env, 2035);
                            case DialogAction.SETPRO4:
                                return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 1295.0565f, 1499.0419f, 1571.1864f, 3, 4); // 4
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 278019) // Lakadi
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
                    else
                        return SendQuestEndDialog(env);
                }
            }
            return false;
        }

        public override bool OnLogOutEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var == 4)
                {
                    ChangeQuestStep(env, 4, 3);
                }
            }
            return false;
        }

        public override bool OnNpcReachTargetEvent(QuestEnv env)
        {
            return DefaultFollowEndEvent(env, 4, 4, true, 290); // reward
        }

        public override bool OnNpcLostTargetEvent(QuestEnv env)
        {
            return DefaultFollowEndEvent(env, 4, 3, false); // 3
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 24040);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 24040);
        }
    }
}
