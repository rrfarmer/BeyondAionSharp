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
    public class _14042ARescueOperation : AbstractQuestHandler
    {
        private static readonly int[] npcs = { 278502, 278517, 278590, 253623 };

        public _14042ARescueOperation() : base(14042)
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
                    case 278502: // Sakmis
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
                    case 278517: // Nereus
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
                    case 278590: // Dactyl
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
                    case 253623: // Captured Elyos Prisoner
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 3)
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                return false;
                            case DialogAction.SELECT4_1:
                                PlayQuestMovie(env, 269);
                                return SendQuestDialog(env, 2035);
                            case DialogAction.SETPRO4:
                                return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 1295.1139f, 1498.6543f, 1571.1763f, 3, 4); // 4
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 278517) // Nereus
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
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
            return DefaultFollowEndEvent(env, 4, 4, true, 270); // reward
        }

        public override bool OnNpcLostTargetEvent(QuestEnv env)
        {
            return DefaultFollowEndEvent(env, 4, 3, false); // 3
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14040);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14040);
        }
    }
}
