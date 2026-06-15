using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _1693AreYouMyFather : AbstractQuestHandler
{
    private static readonly int[] npcs = { 798386, 204514, 798388, 203893 };

    public _1693AreYouMyFather() : base(1693)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798386).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        qe.RegisterOnLogOut(questId);
        qe.RegisterOnEnterWorld(questId);
        qe.RegisterAddOnReachTargetEvent(questId);
        qe.RegisterAddOnLostTargetEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npcObj)
            targetId = npcObj.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798386)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204514:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            }
                        case DialogAction.SETPRO1:
                            {
                                TeleportService.TeleportTo(player, 110010000, 1323.37f, 1511.89f, 567.87f, (byte)0);
                                return DefaultCloseDialog(env, 0, 1);
                            }
                    }
                    break;
                case 798388:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 1)
                                    return SendQuestDialog(env, 1352);
                                return false;
                            }
                        case DialogAction.SETPRO2:
                            {
                                return DefaultStartFollowEvent(env, (Npc)env.GetVisibleObject(), 203893, 1, 2);
                            }
                    }
                    break;
                case 203893:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 2)
                                    return SendQuestDialog(env, 1693);
                                return false;
                            }
                        case DialogAction.SELECT_QUEST_REWARD:
                            {
                                return DefaultCloseDialog(env, 2, 2, true, true);
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203893)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 10002);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (player.GetWorldId() == 110010000)
            {
                SpawnForFiveMinutesInFrontOf(798388, player, 1.5f);
                return true;
            }
        }
        return false;
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 2)
            {
                ChangeQuestStep(env, 2, 0);
            }
        }
        return false;
    }

    public override bool OnNpcReachTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 2, 2, true); // reward
    }

    public override bool OnNpcLostTargetEvent(QuestEnv env)
    {
        return DefaultFollowEndEvent(env, 2, 0, false); // 0
    }
}
