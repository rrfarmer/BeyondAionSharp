using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

public class _1345BearerOfBadNews : AbstractQuestHandler
{
    public _1345BearerOfBadNews() : base(1345)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204006).AddOnQuestStart(questId); // Demokritos
        qe.RegisterQuestNpc(204006).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203765).AddOnTalkEvent(questId); // Kreon
        qe.RegisterQuestItem(182201320, questId); // Tarnished Ring
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (player.IsInsideItemUseZone(ZoneName.Get("LC1_ITEMUSEAREA_Q1345")))
                return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 1, 2, true, 0, 0, 0));
        }
        return HandlerResult.SUCCESS; // ??
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
            if (targetId == 204006) // Demokritos
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 203765) // Kreon
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            RemoveQuestItem(env, 186000015, 1);
                            GiveQuestItem(env, 182201320, 1);
                            return DefaultCloseDialog(env, 0, 1);
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204006)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
