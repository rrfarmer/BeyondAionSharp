using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Pad
/// </summary>
public class _13744AgentinNeed : AbstractQuestHandler
{
    public _13744AgentinNeed() : base(13744)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(832840).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("DRAGON_LORDS_SHRINE_600100000"), questId);
        qe.RegisterOnEnterZone(ZoneName.Get("FLAMEBERTH_DOWNS_600100000"), questId);
        qe.RegisterOnKillInZone("DRAGON_LORDS_SHRINE_600100000", questId);
        qe.RegisterOnKillInZone("FLAMEBERTH_DOWNS_600100000", questId);
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        if ((zoneName == ZoneName.Get("DRAGON_LORDS_SHRINE_600100000") || zoneName == ZoneName.Get("FLAMEBERTH_DOWNS_600100000"))
            && SiegeService.GetInstance().IsSiegeInProgress(8011))
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.IsStartable())
            {
                return QuestService.StartQuest(env);
            }
        }
        return false;
    }

    public override bool OnKillInZoneEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        VisibleObject target = env.GetVisibleObject();
        if (target is Player
            && (player.IsInsideZone(ZoneName.Get("DRAGON_LORDS_SHRINE_600100000")) || player.IsInsideZone(ZoneName.Get("FLAMEBERTH_DOWNS_600100000")))
            && SiegeService.GetInstance().IsSiegeInProgress(8011))
        {
            if ((player.GetLevel() >= (((Player)target).GetLevel() - 5)) && (player.GetLevel() <= (((Player)target).GetLevel() + 9)))
            {
                int var1 = qs.GetQuestVarById(1);
                if (var1 >= 0 && var1 < 11)
                {
                    qs.SetQuestVarById(1, var1 + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (var1 == 11)
                {
                    qs.SetQuestVarById(0, 1);
                    qs.SetQuestVarById(1, 0);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
        }
        return false;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
        {
            if (env.GetTargetId() == 832840)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
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
