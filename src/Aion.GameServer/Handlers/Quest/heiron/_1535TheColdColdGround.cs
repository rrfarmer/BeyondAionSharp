using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas, Neon
/// </summary>
public class _1535TheColdColdGround : AbstractQuestHandler
{
    public _1535TheColdColdGround() : base(1535)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204580).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204580).AddOnTalkEvent(questId);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (targetId != 204580)
            return false;

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                return SendQuestDialog(env, 4762);
            else
                return SendQuestStartDialog(env);
        }

        if (qs.GetStatus() == QuestStatus.START)
        {
            bool abexSkins = player.GetInventory().GetItemCountByItemId(182201818) > 4;
            bool worgSkins = player.GetInventory().GetItemCountByItemId(182201819) > 2;
            bool karnifSkins = player.GetInventory().GetItemCountByItemId(182201820) > 0;

            switch (env.GetDialogActionId())
            {
                case DialogAction.QUEST_SELECT:
                    return SendQuestDialog(env, 1352);
                case DialogAction.SETPRO1:
                    if (abexSkins && RemoveQuestItem(env, 182201818, 5))
                    {
                        qs.SetRewardGroup(0);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 5);
                    }
                    return SendQuestDialog(env, 1693);
                case DialogAction.SETPRO2:
                    if (worgSkins && RemoveQuestItem(env, 182201819, 3))
                    {
                        qs.SetRewardGroup(1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 6);
                    }
                    return SendQuestDialog(env, 1693);
                case DialogAction.SETPRO3:
                    if (karnifSkins && RemoveQuestItem(env, 182201820, 1))
                    {
                        qs.SetRewardGroup(2);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 7);
                    }
                    return SendQuestDialog(env, 1693);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
