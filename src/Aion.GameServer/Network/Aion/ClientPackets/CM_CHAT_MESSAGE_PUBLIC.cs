using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Stats;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHAT_MESSAGE_PUBLIC (SoulKeeper). Reads normal chat messages and broadcasts by ChatType (group/alliance/legion/league/normal/shout/command). ChatProcessor/PlayerChatService/SM_MESSAGE red-tolerated.</summary>
public class CM_CHAT_MESSAGE_PUBLIC : AionClientPacket
{
    /// <summary>Chat type</summary>
    private ChatType type;

    /// <summary>Chat message</summary>
    private string message;

    public CM_CHAT_MESSAGE_PUBLIC(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        type = ChatType.GetChatType(ReadC());
        message = ReadS();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        if (ChatProcessor.GetInstance().HandleChatCommand(player, message))
            return;

        if (!PlayerRestrictions.CanChat(player))
            return;

        PlayerChatService.LogMessage(player, type, message);
        message = NameRestrictionService.FilterMessage(message);

        switch (type)
        {
            case ChatType.GROUP:
                if (!player.IsInTeam())
                    return;
                BroadcastToGroupMembers(player);
                break;
            case ChatType.ALLIANCE:
                if (!player.IsInAlliance())
                    return;
                BroadcastToAllianceMembers(player);
                break;
            case ChatType.GROUP_LEADER:
                if (!player.IsInTeam())
                    return;
                // Alert must go to entire group or alliance.
                if (player.IsInGroup())
                    BroadcastToGroupMembers(player);
                else
                    BroadcastToAllianceMembers(player);
                break;
            case ChatType.LEGION:
                if (!player.IsLegionMember())
                    return;
                BroadcastToLegionMembers(player);
                break;
            case ChatType.LEAGUE:
            case ChatType.LEAGUE_ALERT:
                if (!player.IsInLeague())
                    return;
                BroadcastToLeagueMembers(player);
                break;
            case ChatType.NORMAL:
            case ChatType.SHOUT:
                BroadcastToPlayers(player);
                break;
            case ChatType.COMMAND:
                if (player.GetAbyssRank().GetRank() == AbyssRankEnum.COMMANDER || player.GetAbyssRank().GetRank() == AbyssRankEnum.SUPREME_COMMANDER)
                    BroadcastFromCommander(player);
                break;
            default:
                if (!player.IsStaff())
                    return;
                BroadcastToPlayers(player);
                break;
        }
    }

    private void BroadcastFromCommander(Player player)
    {
        int senderRace = player.GetRace().GetRaceId();
        PacketSendUtility.BroadcastPacket(player, new SM_MESSAGE(player, message, type), true,
            p => senderRace == p.GetRace().GetRaceId() || player.IsStaff() || p.IsStaff());
    }

    /// <summary>Sends message to all players that are not in blocklist (except GMs)</summary>
    private void BroadcastToPlayers(Player player)
    {
        PacketSendUtility.BroadcastPacket(player, new SM_MESSAGE(player, message, type), true,
            p => !p.GetBlockList().Contains(player.GetObjectId()) || player.IsStaff() || p.IsStaff());
    }

    /// <summary>Sends message to all group members.</summary>
    private void BroadcastToGroupMembers(Player player)
    {
        player.GetCurrentGroup().SendPackets(new SM_MESSAGE(player, message, type));
    }

    /// <summary>Sends message to all alliance members</summary>
    private void BroadcastToAllianceMembers(Player player)
    {
        player.GetPlayerAlliance().SendPackets(new SM_MESSAGE(player, message, type));
    }

    /// <summary>Sends message to all league members</summary>
    private void BroadcastToLeagueMembers(Player player)
    {
        player.GetPlayerAlliance().GetLeague().SendPackets(new SM_MESSAGE(player, message, type));
    }

    /// <summary>Sends message to all legion members</summary>
    private void BroadcastToLegionMembers(Player player)
    {
        PacketSendUtility.BroadcastToLegion(player.GetLegion(), new SM_MESSAGE(player, message, type));
    }
}
