using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Ritsu, Majka
/// </summary>
public class _24013PoisonInTheWaters : AbstractQuestHandler
{
    private static readonly int[] mobs = { 210455, 210456, 214039, 210458, 214032 };

    public _24013PoisonInTheWaters() : base(24013)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
        qe.RegisterQuestItem(182215359, questId);
        qe.RegisterQuestNpc(203631).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203621).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203631:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            break;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    return false;
                case 203621:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            break;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2, 182215359, 1); // 2
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203631)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        if (player.IsInsideZone(ZoneName.Get("DF1A_ITEMUSEAREA_Q2016_220030000")))
        {
            // Spawns 2 Feral Black Claw Sharpeye [ID: 210457] far from the player
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                float playerX = env.GetPlayer().GetX();
                float playerY = env.GetPlayer().GetY();
                float playerZ = env.GetPlayer().GetZ();
                Spawn(210457, player, playerX + 13.0f, playerY - 3.0f, playerZ, (byte)60); // Right
                Spawn(210457, player, playerX + 13.0f, playerY + 3.0f, playerZ, (byte)60); // Left
                return ValueTask.CompletedTask;
            }, 3000L);

            return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 2, 3, false)); // 3
        }
        return HandlerResult.UNKNOWN;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        if (DefaultOnKillEvent(env, mobs, 7, true))
            return true;
        return DefaultOnKillEvent(env, mobs, 3, 7); // 6
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 24010);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 24010);
    }
}
