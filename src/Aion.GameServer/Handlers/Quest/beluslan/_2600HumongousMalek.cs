using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author VladimirZ
/// </summary>
public class _2600HumongousMalek : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 204734, 798119, 700512 };

    public _2600HumongousMalek() : base(2600)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204734).AddOnQuestStart(questId);
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 204734)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204734)
                return SendQuestEndDialog(env);
        }
        else if (qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        if (targetId == 798119)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if (var == 0)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                    else if (var == 1)
                    {
                        if (player.GetInventory().GetItemCountByItemId(182204528) > 0)
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        else
                        {
                            GiveQuestItem(env, 182204528, 1);
                            return SendQuestDialog(env, 1779);
                        }
                    }
                    return false;
                case DialogAction.SETPRO1:
                    return DefaultCloseDialog(env, 0, 1, 182204528, 1);
            }
        }
        else if (targetId == 700512)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.USE_OBJECT:
                    if (var == 1)
                    {
                        if (player.GetInventory().GetItemCountByItemId(182204528) > 0)
                        {
                            RemoveQuestItem(env, 182204528, 1);
                            SpawnForFiveMinutes(215383, player.GetWorldMapInstance(), (float)1140.78, (float)432.85, (float)341.0825, (byte)0);
                            return true;
                        }
                    }
                    return false;
            }
        }
        else if (targetId == 204734)
        {
            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    if ((var == 1) && (player.GetInventory().GetItemCountByItemId(182204529) > 0))
                    {
                        return SendQuestDialog(env, 2375);
                    }
                    else
                    {
                        return SendQuestDialog(env, 2716);
                    }
                case DialogAction.SELECT_QUEST_REWARD:
                    return RemoveQuestItem(env, 182204529, 1) && DefaultCloseDialog(env, 1, 1, true, true);
            }
        }
        return false;
    }
}
