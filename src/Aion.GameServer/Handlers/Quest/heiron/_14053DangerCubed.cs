using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _14053DangerCubed : AbstractQuestHandler
    {
        public _14053DangerCubed() : base(14053)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestNpc(204020).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204602).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14050);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14050);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();

            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204602)
                    return SendQuestEndDialog(env);
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 204020)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1352);
                        break;
                    case DialogAction.SETPRO2:
                        if (var == 1)
                        {
                            ChangeQuestStep(env, 1, 2); // 2
                            TeleportService.TeleportTo(player, WorldMapType.HEIRON.GetId(), 2010.1975f, 1395.4108f, 118.125f, (byte)62,
                                TeleportAnimation.FADE_OUT_BEAM);
                            return CloseDialogWindow(env);
                        }
                        break;
                }
            }
            else if (targetId == 204602)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        else if (var == 2)
                            return SendQuestDialog(env, 1693);
                        else if (var == 3)
                            return SendQuestDialog(env, 2034);
                        break;
                    case DialogAction.SELECT3_1:
                        PlayQuestMovie(env, 191);
                        break;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (QuestService.CollectItemCheck(env, true))
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            qs.SetRewardGroup(0);
                            UpdateQuestStatus(env);
                            return SendQuestEndDialog(env);
                        }
                        else
                            return SendQuestDialog(env, 10001);
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            ChangeQuestStep(env, 0, 1); // 1
                            TeleportService.TeleportTo(player, WorldMapType.ELTNEN.GetId(), 1596.1948f, 1529.9152f, 317, (byte)120,
                                TeleportAnimation.FADE_OUT_BEAM);
                            return CloseDialogWindow(env);
                        }
                        break;
                    case DialogAction.SETPRO3:
                        return DefaultCloseDialog(env, 2, 3); // 3
                }
            }
            return false;
        }
    }
}
