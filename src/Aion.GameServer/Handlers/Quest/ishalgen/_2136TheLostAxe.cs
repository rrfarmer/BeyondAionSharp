using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rhys2002, Hellboy
/// </summary>
public class _2136TheLostAxe : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 700146, 790009 };

    public _2136TheLostAxe() : base(2136)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(182203130, questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
            {
                QuestService.StartQuest(env);
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
                return true;
            }
            else
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 790009)
            {
                Npc npc = (Npc)env.GetVisibleObject();
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    npc.GetController().Delete();
                    return ValueTask.CompletedTask;
                }, 10000L);
                return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() != QuestStatus.START)
            return false;

        if (targetId == 790009)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 1)
                        return SendQuestDialog(env, 1011);
                    return false;
                case DialogAction.SETPRO1:
                    if (var == 1)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        RemoveQuestItem(env, 182203130, 1);
                        qs.SetRewardGroup(1);
                        return SendQuestDialog(env, 6);
                    }
                    return false;
                case DialogAction.SETPRO2:
                    if (var == 1)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        RemoveQuestItem(env, 182203130, 1);
                        qs.SetRewardGroup(0);
                        return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (targetId == 700146)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.USE_OBJECT:
                    if (var == 0)
                    {
                        PlayQuestMovie(env, 59);
                        qs.SetQuestVarById(0, 1);
                        UpdateQuestStatus(env);
                        SpawnForFiveMinutes(790009, player.GetWorldMapInstance(), 1088.5f, 2371.8f, 258.375f, (byte)87);
                        return true;
                    }
                    break;
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

        if (id != 182203130)
            return HandlerResult.UNKNOWN;
        PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 20, 1, 0), true);
        if (qs == null || qs.IsStartable())
        {
            QuestService.StartQuest(env);
        }
        return HandlerResult.SUCCESS;
    }
}
