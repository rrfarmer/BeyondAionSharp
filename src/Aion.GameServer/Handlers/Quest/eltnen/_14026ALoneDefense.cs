using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur
/// </summary>
public class _14026ALoneDefense : AbstractQuestHandler
{
    private static readonly int[] mobs = { 211628, 211630, 213575 };

    public _14026ALoneDefense() : base(14026)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnDie(questId);
        qe.RegisterOnLogOut(questId);
        qe.RegisterOnQuestTimerEnd(questId);
        qe.RegisterQuestNpc(203901).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204020).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204044).AddOnTalkEvent(questId);
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
        qe.RegisterQuestNpc(700141).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203901:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 0)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                return false;
                            }
                        case DialogAction.SETPRO1:
                            {
                                qs.SetQuestVar(1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                TeleportService.TeleportTo(player, WorldMapType.ELTNEN.GetId(), 1596.1948f, 1529.9152f, 317, (byte) 120,
                                    TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                    }
                    return false;
                case 204020:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 1)
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                return false;
                            }
                        case DialogAction.SETPRO2:
                            {
                                qs.SetQuestVar(2);
                                UpdateQuestStatus(env);
                                GiveQuestItem(env, 182215324, 1);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                TeleportService.TeleportTo(player, WorldMapType.ELTNEN.GetId(), 2500.15f, 780.9f, 409, (byte) 15, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                    }
                    return false;
                case 204044:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                switch (qs.GetQuestVarById(0))
                                {
                                    case 2:
                                        {
                                            return SendQuestDialog(env, 1693);
                                        }
                                    case 4:
                                        {
                                            return SendQuestDialog(env, 2034);
                                        }
                                }
                                return false;
                            }
                        case DialogAction.SETPRO3:
                            {
                                qs.SetQuestVar(3);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                QuestService.QuestTimerStart(env, 180);
                                Spawn(player);
                                return true;
                            }
                        case DialogAction.SETPRO4:
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                TeleportService.TeleportTo(player, WorldMapType.ELTNEN.GetId(), 271.69f, 2787.04f, 272.47f, (byte) 50, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203901)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    default:
                        qs.SetRewardGroup(0); // group 0 and 1 are identical in templates, set anyway to mute warning
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        int[] quests = { 14020, 14021, 14022, 14023, 14024, 14025 };
        DefaultOnQuestCompletedEvent(env, quests);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        int[] quests = { 14020, 14021, 14022, 14023, 14024, 14025 };
        DefaultOnLevelChangedEvent(player, quests);
    }

    public override bool OnQuestTimerEndEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 3)
            {
                qs.SetQuestVar(4);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }

    public override bool OnDieEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 3)
            {
                ChangeQuestStep(env, var, 2);
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
            if (var == 3)
            {
                ChangeQuestStep(env, var, 2);
                return true;
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 3)
            {
                int targetId = env.GetTargetId();
                if (mobs.Any(npcId => npcId == targetId))
                {
                    Spawn(player);
                    return true;
                }
            }
        }
        return false;
    }

    private void Spawn(Player player)
    {
        int mobToSpawn = Rnd.Get(mobs);
        float x = 0;
        float y = 0;
        const float z = 217.48f;
        switch (mobToSpawn)
        {
            case 211628:
                x = 254.74f;
                y = 236.72f;
                break;
            case 211630:
                x = 257.92f;
                y = 237.39f;
                break;
            case 213575:
                x = 261.86f;
                y = 237.5f;
                break;
        }
        Npc spawn = (Npc) Spawn(mobToSpawn, player, x, y, z, (byte) 95);
        VisibleObject target = spawn.GetKnownList().FindObject(o => o.Get() is Npc npc && npc.GetNpcId() == 204020);
        if (target != null)
        {
            spawn.SetTarget(target);
            spawn.GetAi().SetStateIfNot(AIState.WALKING);
            spawn.SetState(CreatureState.ACTIVE, true);
            spawn.GetMoveController().MoveToTargetObject();
            PacketSendUtility.BroadcastPacket(spawn, new SM_EMOTION(spawn, EmotionType.CHANGE_SPEED, 0, spawn.GetObjectId()));
        }
    }
}
