using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("quest_use_item")]
public class QuestItemNpcAI : ActionItemNpcAI
{
    public QuestItemNpcAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (!(QuestEngine.QuestEngine.GetInstance().OnCanAct(new QuestEnv(GetOwner(), player, 0), GetObjectTemplate().GetTemplateId(),
            QuestActionType.ACTION_ITEM_USE)))
        {
            return;
        }
        base.HandleDialogStart(player);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        QuestEnv env = new QuestEnv(GetOwner(), player, 0, DialogAction.USE_OBJECT);
        if (!QuestEngine.QuestEngine.GetInstance().OnDialog(env))
        {
            if (GetObjectTemplate().IsDialogNpc()) // show default dialog
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
            return;
        }

        if (QuestService.GetQuestDrop(GetNpcId()).Count == 0)
            return;

        List<Player> registeredPlayers = new List<Player>();
        if (player.IsInGroup())
        {
            registeredPlayers = QuestService.GetEachDropMembersGroup(player.GetPlayerGroup(), GetNpcId(), env.GetQuestId());
            if (registeredPlayers.Count == 0)
            {
                registeredPlayers.Add(player);
            }
        }
        else if (player.IsInAlliance())
        {
            registeredPlayers = QuestService.GetEachDropMembersAlliance(player.GetPlayerAlliance(), GetNpcId(), env.GetQuestId());
            if (registeredPlayers.Count == 0)
            {
                registeredPlayers.Add(player);
            }
        }
        else
        {
            registeredPlayers.Add(player);
        }
        AIActions.RegisterDrop(this, player, registeredPlayers);
        AIActions.Die(this, player);
        DropService.GetInstance().RequestDropList(player, GetObjectId());
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        CreatureEventHandler.OnCreatureSee(this, creature);
    }
}
