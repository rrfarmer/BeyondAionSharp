using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu, Pad
/// </summary>
public class _1354PraticalAerobatics : AbstractQuestHandler
{
    // chronological order of the flight rings has been changed
    private readonly string[] rings = { "ERACUS_TEMPLE_210020000_1", "ERACUS_TEMPLE_210020000_4", "ERACUS_TEMPLE_210020000_3", "ERACUS_TEMPLE_210020000_6",
        "ERACUS_TEMPLE_210020000_5", "ERACUS_TEMPLE_210020000_2", "ERACUS_TEMPLE_210020000_7" };

    public _1354PraticalAerobatics() : base(1354)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203983).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203983).AddOnTalkEvent(questId);
        qe.RegisterOnQuestTimerEnd(questId);
        foreach (string ring in rings)
        {
            qe.RegisterOnPassFlyingRings(ring, questId);
        }
    }

    public override bool OnPassFlyingRingEvent(QuestEnv env, string flyingRing)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (rings[0].Equals(flyingRing))
                ChangeQuestStep(env, 1, 2);
            else if (rings[1].Equals(flyingRing))
                ChangeQuestStep(env, 2, 3);
            else if (rings[2].Equals(flyingRing))
                ChangeQuestStep(env, 3, 4);
            else if (rings[3].Equals(flyingRing))
                ChangeQuestStep(env, 4, 5);
            else if (rings[4].Equals(flyingRing))
                ChangeQuestStep(env, 5, 6);
            else if (rings[5].Equals(flyingRing))
                ChangeQuestStep(env, 6, 7);
            else if (rings[6].Equals(flyingRing))
            {
                qs.SetQuestVarById(0, 8);
                ChangeQuestStep(env, 8, 8, true);
                QuestService.QuestTimerEnd(env);
            }
            ApplyFlightRingEffect(env);
            return true;
        }
        return false;
    }

    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        qs.SetQuestVarById(0, 0);
        UpdateQuestStatus(env);
        return true;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        int dialogActionId = env.GetDialogActionId();
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203983)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203983)
            {
                int var0 = qs.GetQuestVarById(0);
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var0 == 0)
                            return SendQuestDialog(env, 1003);
                        else if (var0 == 1)
                            return SendQuestDialog(env, 2717);
                        else if (var0 == 8)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SETPRO1:
                        if (qs.GetQuestVarById(0) == 0)
                        {
                            QuestService.QuestTimerStart(env, 120);
                            return DefaultCloseDialog(env, 0, 1);
                        }
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203983)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    private void ApplyFlightRingEffect(QuestEnv env)
    {
        Player player = env.GetPlayer();
        player.GetLifeStats().IncreaseFp(SmAttackStatus.TYPE.FP_RINGS, 7, 0, SmAttackStatus.LOG.REGULAR);
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(1856, player, player);
    }
}
