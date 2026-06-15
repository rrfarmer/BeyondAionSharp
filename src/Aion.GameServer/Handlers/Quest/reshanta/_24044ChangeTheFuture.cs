using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _24044ChangeTheFuture : AbstractQuestHandler
    {
        public _24044ChangeTheFuture() : base(24044)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestNpc(278036).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(203550).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204207).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(798067).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(279029).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(700355).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int dialogActionId = env.GetDialogActionId();
            if (qs == null)
                return false;
            int targetId = env.GetTargetId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                switch (targetId)
                {
                    case 278036: // Scoda
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                return false;
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    case 203550: // Munin
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                return false;
                            case DialogAction.SETPRO2:
                                return DefaultCloseDialog(env, 1, 2); // 2
                        }
                        break;
                    case 204207: // Kasir
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                return false;
                            case DialogAction.SETPRO3:
                                return DefaultCloseDialog(env, 2, 3); // 3
                        }
                        break;
                    case 798067: // Lyeanenerk
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 3)
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                return false;
                            case DialogAction.SETPRO4:
                                return DefaultCloseDialog(env, 3, 4); // 4
                        }
                        break;
                    case 279029: // Lugbug
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 4)
                                {
                                    return SendQuestDialog(env, 2375);
                                }
                                else if (var == 6)
                                {
                                    return SendQuestDialog(env, 3057);
                                }
                                return false;
                            case DialogAction.SETPRO5:
                                return DefaultCloseDialog(env, 4, 5); // 5
                            case DialogAction.SET_SUCCEED:
                                return DefaultCloseDialog(env, 6, 6, true, false); // reward
                        }
                        break;
                    case 700355: // Artifact of the Inception
                        switch (dialogActionId)
                        {
                            case DialogAction.USE_OBJECT:
                                if (var == 5)
                                {
                                    if (player.GetInventory().GetItemCountByItemId(188020000) > 0)
                                    {
                                        return UseQuestObject(env, 5, 6, false, 0, 0, 0, 188020000, 1, 291, false); // 6
                                    }
                                    PacketSendUtility.SendMonologue(player, 1111203); // I need an Artifact Activation Stone!
                                }
                                break;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 278036) // Scoda
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    else
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
            return false;
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
