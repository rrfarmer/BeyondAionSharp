using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author dta
/// </summary>
public class _11005TheLimitsofGenius : AbstractQuestHandler
{
    public _11005TheLimitsofGenius() : base(11005)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798942).AddOnQuestStart(questId); // Harknes
        qe.RegisterQuestNpc(798942).AddOnTalkEvent(questId); // Harknes
        qe.RegisterQuestNpc(798950).AddOnTalkEvent(questId); // Veloso
        qe.RegisterQuestNpc(798951).AddOnTalkEvent(questId); // Ambred
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc) env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798942)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
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
                case 798950:
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
                case 798951:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        case DialogAction.SETPRO2:
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                    }
                    return false;
                case 798942:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        case DialogAction.SELECT_QUEST_REWARD:
                        {
                            qs.SetQuestVar(4);
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
            if (targetId == 798942)
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
}
