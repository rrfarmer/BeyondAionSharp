using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

public class _1640TeleporterRepairs : AbstractQuestHandler
{
    public _1640TeleporterRepairs() : base(1640)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730033).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(730033).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 730033)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 730033)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO2:
                        if (player.GetInventory().GetItemCountByItemId(182201790) > 0)
                        {
                            RemoveQuestItem(env, 182201790, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            QuestService.FinishQuest(env);
                            TeleportService.TeleportTo(player, WorldMapType.HEIRON.GetId(), 187.71689f, 2712.14870f, 141.91672f, (byte)195,
                                TeleportAnimation.FADE_OUT_BEAM);
                            return CloseDialogWindow(env);
                        }
                        else
                            return SendQuestDialog(env, 1353);
                }
            }
        }
        return false;
    }
}
