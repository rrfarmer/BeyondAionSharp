using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Nephis
    /// </summary>
    public class _4042MessageinaBottle : AbstractQuestHandler
    {
        public _4042MessageinaBottle() : base(4042)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestItem(182209024, questId);
            qe.RegisterQuestNpc(730150).AddOnQuestStart(questId); // Bottle
            qe.RegisterQuestNpc(730150).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(205192).AddOnTalkEvent(questId); // Sahnu
            qe.RegisterQuestNpc(204225).AddOnTalkEvent(questId); // Gunter
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
                if (targetId == 0)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    {
                        QuestService.StartQuest(env);
                        return CloseDialogWindow(env);
                    }
                }
                else if (targetId == 730150)
                {
                    return GiveQuestItem(env, 182209024, 1);
                }
            }

            switch (targetId)
            {
                case 205192:
                    if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);
                        else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                        {
                            if (!GiveQuestItem(env, 182209025, 1))
                                return true;
                            RemoveQuestItem(env, 182209024, 1);
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        else
                            return SendQuestStartDialog(env);
                    }
                    else if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 5);
                        }
                        else
                            return SendQuestStartDialog(env);
                    }
                    else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
                    {
                        return SendQuestEndDialog(env);
                    }
                    return false;
                case 204225:
                    if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1693);
                        else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                        {
                            RemoveQuestItem(env, 182209025, 1);
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        else
                            return SendQuestStartDialog(env);
                    }
                    break;
            }
            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.IsStartable())
            {
                return HandlerResultExtensions.FromBoolean(SendQuestDialog(env, 4));
            }
            return HandlerResult.FAILED;
        }
    }
}
