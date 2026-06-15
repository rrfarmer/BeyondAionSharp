using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3031Pirates : AbstractQuestHandler
{
    public _3031Pirates() : base(3031)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730144).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(730144).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798172).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(214219).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(214220).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(214222).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(214223).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 730144)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.SETPRO1:
                        QuestService.StartQuest(env);
                        PacketSendUtility.SendPacket(player, new SmDialogWindow(0, 0));
                        return true;
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798172)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }

        int targetId = 0;

        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (targetId == 214219 || targetId == 214220)
        {
            switch (qs.GetQuestVarById(1))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                case 13:
                case 14:
                    qs.SetQuestVarById(1, qs.GetQuestVarById(1) + 1);
                    UpdateQuestStatus(env);

                    if (qs.GetQuestVarById(1) == 15 && qs.GetQuestVarById(2) == 12)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return true;
                    }
                    return true;
            }
        }
        else if (targetId == 214222 || targetId == 214223)
        {
            switch (qs.GetQuestVarById(2))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                    qs.SetQuestVarById(2, qs.GetQuestVarById(2) + 1);
                    UpdateQuestStatus(env);

                    if (qs.GetQuestVarById(1) == 15 && qs.GetQuestVarById(2) == 12)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return true;
                    }
                    return true;
            }
        }
        return false;
    }
}
