using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Pad
/// </summary>
public class _24220WelcometoPanesterra : AbstractQuestHandler
{
    private const int startEndNpcId = 802542;
    private const int talkNpcId = 802543;
    private static readonly List<int> belusNpcIds = new List<int> { 802544, 804080, 804081, 804082, 804689 };
    private static readonly List<int> aspidaNpcIds = new List<int> { 802545, 804083, 804084, 804085, 804690 };
    private static readonly List<int> atanatosNpcIds = new List<int> { 802546, 804086, 804087, 804088, 804691 };
    private static readonly List<int> disillonNpcIds = new List<int> { 802547, 804089, 804090, 804091, 804692 };

    public _24220WelcometoPanesterra() : base(24220)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(startEndNpcId).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(startEndNpcId).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(talkNpcId).AddOnTalkEvent(questId);
        foreach (int belusNpcId in belusNpcIds)
            qe.RegisterQuestNpc(belusNpcId).AddOnTalkEvent(questId);
        foreach (int aspidaNpcId in aspidaNpcIds)
            qe.RegisterQuestNpc(aspidaNpcId).AddOnTalkEvent(questId);
        foreach (int atanatosNpcId in atanatosNpcIds)
            qe.RegisterQuestNpc(atanatosNpcId).AddOnTalkEvent(questId);
        foreach (int disillonNpcId in disillonNpcIds)
            qe.RegisterQuestNpc(disillonNpcId).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == startEndNpcId)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var0 = qs.GetQuestVarById(0);
            if (var0 == 0)
            {
                if (targetId == talkNpcId)
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                    {
                        return SendQuestDialog(env, 1011);
                    }
                    else if (dialogActionId == DialogAction.SETPRO1)
                    {
                        return DefaultCloseDialog(env, 0, 1);
                    }
                }
            }
            else if (var0 == 1)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (belusNpcIds.Contains(targetId))
                    {
                        return SendQuestDialog(env, 1352);
                    }
                    else if (aspidaNpcIds.Contains(targetId))
                    {
                        return SendQuestDialog(env, 1693);
                    }
                    else if (atanatosNpcIds.Contains(targetId))
                    {
                        return SendQuestDialog(env, 2034);
                    }
                    else if (disillonNpcIds.Contains(targetId))
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 1, 1, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == startEndNpcId)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
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
}
