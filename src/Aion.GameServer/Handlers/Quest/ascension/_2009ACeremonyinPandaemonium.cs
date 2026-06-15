using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author MrPoke
    /// </summary>
    public class _2009ACeremonyinPandaemonium : AbstractQuestHandler
    {
        public _2009ACeremonyinPandaemonium() : base(2009)
        {
        }

        public override void Register()
        {
            if (CustomConfig.ENABLE_SIMPLE_2NDCLASS)
                return;
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterQuestNpc(203550).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204182).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204075).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204080).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204081).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204082).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204083).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(801220).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(801221).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int var = qs.GetQuestVars().GetQuestVars();
            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 203550)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1013);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            if (var <= 1)
                            {
                                qs.SetQuestVar(1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                                TeleportService.TeleportTo(player, 120010000, 1685f, 1400f, 195f, (byte)0, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                            break;
                    }
                }
                else if (targetId == 204182)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SELECT2_1:
                            if (var == 1)
                            {
                                PlayQuestMovie(env, 121);
                                return false;
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                    }
                }
                else if (targetId == 204075)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.SELECT3_1:
                            if (var == 2)
                            {
                                PlayQuestMovie(env, 122);
                                return false;
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            if (var == 2)
                            {
                                switch (player.GetPlayerClass().GetStartingClass())
                                {
                                    case PlayerClass.WARRIOR:
                                        qs.SetQuestVar(10);
                                        qs.SetRewardGroup(0);
                                        break;
                                    case PlayerClass.SCOUT:
                                        qs.SetQuestVar(20);
                                        qs.SetRewardGroup(1);
                                        break;
                                    case PlayerClass.MAGE:
                                        qs.SetQuestVar(30);
                                        qs.SetRewardGroup(2);
                                        break;
                                    case PlayerClass.PRIEST:
                                        qs.SetQuestVar(40);
                                        qs.SetRewardGroup(3);
                                        break;
                                    case PlayerClass.ENGINEER:
                                        qs.SetQuestVar(50);
                                        qs.SetRewardGroup(4);
                                        break;
                                    case PlayerClass.ARTIST:
                                        qs.SetQuestVar(60);
                                        qs.SetRewardGroup(5);
                                        break;
                                }
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestSelectionDialog(env);
                            }
                            break;
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204080 && var == 10)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 2034);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 204081 && var == 20)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 2375);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 204082 && var == 30)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 2716);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 204083 && var == 40)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 3057);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 801220 && var == 50)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 3398);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
                else if (targetId == 801221 && var == 60)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 3739);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
            }
            return false;
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 2008);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 2008);
        }
    }
}
