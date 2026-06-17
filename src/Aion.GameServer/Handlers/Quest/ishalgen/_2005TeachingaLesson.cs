using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, Majka
/// </summary>
public class _2005TeachingaLesson : AbstractQuestHandler
{
    public _2005TeachingaLesson() : base(2005)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203540).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203540:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            else if (var == 1)
                                return SendQuestDialog(env, 1352);
                            break;
                        case DialogAction.SELECT1_1:
                            PlayQuestMovie(env, 54);
                            break;
                        case DialogAction.SETPRO1:
                            if (var == 0)
                            {
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                            }
                            break;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            if (var == 1)
                            {
                                if (QuestService.CollectItemCheck(env, true))
                                {
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 5);
                                }
                                else
                                    return SendQuestDialog(env, 1693);
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203540)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 2100);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 2100);
    }
}
