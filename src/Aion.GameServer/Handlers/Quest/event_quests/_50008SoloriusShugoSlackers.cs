using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _50008SoloriusShugoSlackers : AbstractQuestHandler
{
    public _50008SoloriusShugoSlackers() : base(50008)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831038).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831038).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(219290).AddOnAttackEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 831038)
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
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 831038)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnAttackEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int targetId = env.GetTargetId();
            int var = qs.GetQuestVarById(0);
            if (targetId == 219290)
            {
                Npc npc = (Npc)env.GetVisibleObject();
                if (!npc.IsSpawned())
                    return false;
                SpawnSearchResult searchResult = DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(npc.GetWorldId(), 831036);
                if (PositionUtil.GetDistance(searchResult.GetSpot().GetX(), searchResult.GetSpot().GetY(), searchResult.GetSpot().GetZ(), npc.GetX(), npc.GetY(),
                    npc.GetZ()) <= 15)
                {
                    npc.GetController().DeleteAndScheduleRespawn();
                    if (var == 0)
                        ChangeQuestStep(env, 0, 1);
                    else
                        ChangeQuestStep(env, 1, 1, true);
                    return true;
                }
            }
        }
        return false;
    }
}
