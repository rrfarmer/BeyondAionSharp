using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _14046PiecingTheMemory : AbstractQuestHandler
    {
        private static readonly int[] npc_ids = { 278500, 203834, 203786, 203754, 203704 };

        public _14046PiecingTheMemory() : base(14046)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnEnterWorld(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14040);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14040);
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

            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203704)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
                    return SendQuestEndDialog(env);
                }
                return false;
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 278500)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            TeleportService.TeleportTo(player, 110010000, 2014f, 1493f, 581.1387f, (byte)70, TeleportAnimation.FADE_OUT_BEAM);
                            ChangeQuestStep(env, 0, 1);
                            return true;
                        }
                        break;
                }
            }
            else if (targetId == 203834)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1352);
                        else if (var == 3)
                            return SendQuestDialog(env, 2034);
                        else if (var == 5)
                            return SendQuestDialog(env, 2716);
                        return false;
                    case DialogAction.SELECT2_1:
                        PlayQuestMovie(env, 102);
                        break;
                    case DialogAction.SETPRO2:
                        if (var == 1)
                        {
                            ChangeQuestStep(env, 1, 2);
                            return CloseDialogWindow(env);
                        }
                        return false;
                    case DialogAction.SETPRO4:
                        if (var == 3)
                        {
                            TeleportService.TeleportTo(player, 310070000, 214f, 279f, 1387.241f, (byte)69, TeleportAnimation.FADE_OUT_BEAM);
                            ChangeQuestStep(env, 3, 4);
                            return true;
                        }
                        return false;
                    case DialogAction.SETPRO6:
                        if (var == 5)
                        {
                            ChangeQuestStep(env, 5, 6);
                            return CloseDialogWindow(env);
                        }
                        break;
                }
            }
            else if (targetId == 203786)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 2, 3, false, 10000, 10001, 182215354, 1);
                }
            }
            else if (targetId == 203754)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 6)
                            return SendQuestDialog(env, 3057);
                        return false;
                    case DialogAction.SET_SUCCEED:
                        if (var == 6)
                        {
                            RemoveQuestItem(env, 182215354, 1);
                            ChangeQuestStep(env, 6, 6, true);
                            return CloseDialogWindow(env);
                        }
                        break;
                }
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START || qs.GetQuestVarById(0) != 4)
                return false;
            return PlayQuestMovie(env, 170);
        }

        public override void OnMovieEndEvent(QuestEnv env, int movieId)
        {
            if (movieId == 170)
            {
                ChangeQuestStep(env, 4, 5);
                TeleportService.TeleportTo(env.GetPlayer(), 110010000, 2014f, 1493f, 581.1387f, (byte)92, TeleportAnimation.FADE_OUT_BEAM);
            }
        }
    }
}
