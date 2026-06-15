using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _30157VilisMind : AbstractQuestHandler
{
    public _30157VilisMind() : base(30157)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204304).AddOnQuestStart(questId); // Vili
        qe.RegisterQuestNpc(204304).AddOnTalkEvent(questId); // Vili
        qe.RegisterQuestNpc(799234).AddOnTalkEvent(questId); // Nep
        qe.RegisterQuestNpc(700570).AddOnTalkEvent(questId); // Statue Sinigalla
        qe.RegisterQuestNpc(799339).AddOnTalkEvent(questId); // Sinigalla
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204304)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    if (!GiveQuestItem(env, 182209254, 1))
                        return true;
                    return SendQuestStartDialog(env);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799234)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 700570)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 0)
                        {
                            SpawnForFiveMinutes(799339, player.GetWorldMapInstance(), (float)545.3877, (float)1232.0298, (float)304.3357, (byte)76);
                            return UseQuestObject(env, 0, 0, false, 0, 0, 0, 182209254, 1);
                        }
                        break;
                }
            }
            if (targetId == 799339)
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
                            DefaultCloseDialog(env, 0, 0, true, false);
                            Npc npc = (Npc)env.GetVisibleObject();
                            ThreadPoolManager.GetInstance().Schedule(ct =>
                            {
                                npc.GetController().Delete();
                                return ValueTask.CompletedTask;
                            }, 40000L);
                            return true;
                        }
                        break;
                }
            }
        }
        return false;
    }
}
