using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Majka
/// </summary>
public class _20033DranaSolution : AbstractQuestHandler
{
    private static readonly int[] mobs = { 216444, 216445, 216446, 216447 };

    public _20033DranaSolution() : base(20033)
    {
    }

    public override void Register()
    {
        // Nunglark Drana Growing Device ID: 700701
        // Skira ID: 799270
        int[] npcs = { 700701, 799270 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
        qe.RegisterQuestItem(182215614, questId);
        qe.RegisterQuestItem(182215594, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            return false;
        }

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799270)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1011);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        GiveQuestItem(env, 182215614, 1);
                        GiveQuestItem(env, 182215594, 1);
                        return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
            else if (targetId == 700701)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return UseQuestObject(env, 2, 3, false, 0, 0, 0, 182215614, 1); // 3
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799270)
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

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 1)
            {
                int var1 = qs.GetQuestVarById(1);
                if (var1 >= 0 && var1 < 4)
                {
                    return DefaultOnKillEvent(env, mobs, var1, var1 + 1, 1);
                }
                else if (var1 == 4)
                {
                    qs.SetQuestVar(2); // 2
                    UpdateQuestStatus(env);
                    return true;
                }
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.GetStatus() != QuestStatus.START || id != 182215594)
        {
            return HandlerResult.UNKNOWN;
        }

        int var = qs.GetQuestVarById(0);
        if (var != 3)
            return HandlerResult.UNKNOWN;

        PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            PlayQuestMovie(env, 553);
            RemoveQuestItem(env, 182215594, 1);
            qs.SetQuestVar(4);
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
            return ValueTask.CompletedTask;
        }, 3000L);

        return HandlerResult.SUCCESS;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 20031);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 20031);
    }
}
