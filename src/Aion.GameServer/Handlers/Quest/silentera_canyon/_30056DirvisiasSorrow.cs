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
public class _30056DirvisiasSorrow : AbstractQuestHandler
{
    public _30056DirvisiasSorrow() : base(30056)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798929).AddOnQuestStart(questId); // Gellius
        qe.RegisterQuestNpc(798929).AddOnTalkEvent(questId); // Gellius
        qe.RegisterQuestNpc(203901).AddOnTalkEvent(questId); // Telemachus
        qe.RegisterQuestNpc(700569).AddOnTalkEvent(questId); // Statue Dirvisia
        qe.RegisterQuestNpc(799034).AddOnTalkEvent(questId); // Dirvisia
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798929)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    if (!GiveQuestItem(env, 182209223, 1))
                        return true;
                    return SendQuestStartDialog(env);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203901)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        RemoveQuestItem(env, 182209224, 1);
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 700569)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 0)
                        {
                            SpawnForFiveMinutes(799034, player.GetWorldMapInstance(), 555.8842f, 307.8092f, 310.24997f, (byte)0);
                            return UseQuestObject(env, 0, 0, false, 0, 0, 0, 182209223, 1);
                        }
                        break;
                }
            }
            if (targetId == 799034)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
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
                            }, 400L);
                            return true;
                        }
                        break;
                }
            }
        }
        return false;
    }
}
