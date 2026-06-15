using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author dta3000
/// </summary>
public class _11008LetterOfEncouragement : AbstractQuestHandler
{
    public _11008LetterOfEncouragement() : base(11008)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798927).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798927).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798934).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798997).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc) env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            if (targetId == 798927)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182206710, 1))
                        return SendQuestStartDialog(env);
                    else
                        return true;
                }
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
                case 798934:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        case DialogAction.SETPRO1:
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                    }
                    return false;
                case 798997:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        case DialogAction.SELECT_QUEST_REWARD:
                        {
                            qs.SetQuestVar(2);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestEndDialog(env);
                        }
                        default:
                            return SendQuestEndDialog(env);
                    }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798997)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 11008);
    }
}
