using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu
/// </summary>
[AIName("nightmare_crate")]
public class NightmareCrateAI : ActionItemNpcAI
{
    public NightmareCrateAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        int npcId = GetOwner().GetNpcId();
        if ((npcId == 831745)
            && (player.GetInventory().GetItemCountByItemId(185000187) > 0 || player.GetInventory().GetItemCountByItemId(185000184) > 0))
        {
            if (player.GetInventory().DecreaseByItemId(185000187, 1))
            {
                AnalyzeOpening(player);
            }
            else if (player.GetInventory().DecreaseByItemId(185000184, 1))
            {
                AnalyzeOpening(player);
            }
        }
        else if (npcId == 831575
            && (player.GetInventory().GetItemCountByItemId(185000187) > 2 || player.GetInventory().GetItemCountByItemId(185000184) > 2))
        {
            if (player.GetInventory().DecreaseByItemId(185000187, 3))
            {
                AnalyzeOpening(player);
            }
            else if (player.GetInventory().DecreaseByItemId(185000184, 3))
            {
                AnalyzeOpening(player);
            }
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_IDEVENT01_TREASUREBOX());
        }
    }

    private void AnalyzeOpening(Player player)
    {
        if (GetOwner().IsInState(CreatureState.DEAD))
        {
            AuditLogger.Log(player, "attempted multiple chest looting!");
            return;
        }

        ICollection<Player> players = new HashSet<Player>();
        if (player.IsInGroup())
        {
            foreach (Player member in player.GetPlayerGroup().GetOnlineMembers())
            {
                if (PositionUtil.IsInRange(member, GetOwner(), GroupConfig.GROUP_MAX_DISTANCE))
                {
                    players.Add(member);
                }
            }
        }
        else if (player.IsInAlliance())
        {
            foreach (Player member in player.GetPlayerAlliance().GetOnlineMembers())
            {
                if (PositionUtil.IsInRange(member, GetOwner(), GroupConfig.GROUP_MAX_DISTANCE))
                {
                    players.Add(member);
                }
            }
        }
        else
        {
            players.Add(player);
        }
        DropRegistrationService.GetInstance().RegisterDrop(GetOwner(), player, GetHighestLevel(players), players);
        AIActions.Die(this, player);
        DropService.GetInstance().RequestDropList(player, GetObjectId());
        base.HandleUseItemFinish(player);
    }

    private int GetHighestLevel(ICollection<Player> players)
    {
        return players.Max(p => p.GetLevel());
    }
}
