using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>
/// Includes Dredgion, Engulfed Ophidan Bridge, Idgel Dome, Iron Wall Warfront and Kamar Battlefield (Java parity:
/// model/autogroup/AutoPvpInstance, Estrayl) : AutoInstance. synchronized→lock(this); Map.putAll→foreach add;
/// List.getFirst()→[0]; NOTE Java `TemporaryPlayerTeam&lt;?&gt; team` (group OR alliance) → C# `AionObject team`
/// since only GetObjectId() is used (GeneralTeam : AionObject) — avoids the wildcard. PlayerGroup/AllianceService red-tolerated.
/// </summary>
public class AutoPvpInstance : AutoInstance
{
    public AutoPvpInstance(AutoGroupType agt) : base(agt)
    {
    }

    public override AGQuestion AddLookingForParty(LookingForParty lookingForParty)
    {
        lock (this)
        {
            if (IsRegistrationDisabled(lookingForParty) || registeredAGPlayers.Count >= GetMaxPlayers())
                return AGQuestion.FAILED;

            List<AGPlayer> playersByRace = GetAGPlayersByRace(lookingForParty.GetRace());
            if (lookingForParty.GetMembers().Count + playersByRace.Count > GetMaxPlayers(lookingForParty.GetRace()))
                return AGQuestion.FAILED;

            foreach (KeyValuePair<int, AGPlayer> kv in lookingForParty.GetMembers())
                registeredAGPlayers[kv.Key] = kv.Value;
            return instance == null && registeredAGPlayers.Count == GetMaxPlayers() ? AGQuestion.READY : AGQuestion.ADDED;
        }
    }

    public override void OnEnterInstance(Player player)
    {
        base.OnEnterInstance(player);
        List<Player> playersByRace = GetPlayersByRace(player.GetRace());
        playersByRace.Remove(player);
        if (playersByRace.Count == 0)
        {
            AionObject team;
            if (GetMaxPlayers(player.GetRace()) <= 6)
                team = PlayerGroupService.CreateGroup(player, player, TeamType.AUTO_GROUP, 0);
            else
                team = PlayerAllianceService.CreateAlliance(player, player, TeamType.AUTO_ALLIANCE);
            int teamId = team.GetObjectId();
            if (!instance.IsRegistered(teamId))
                instance.Register(teamId);
        }
        else
        {
            if (playersByRace[0].IsInGroup())
                PlayerGroupService.AddPlayer(playersByRace[0].GetPlayerGroup(), player);
            else
                PlayerAllianceService.AddPlayer(playersByRace[0].GetPlayerAlliance(), player);
        }
        int objectId = player.GetObjectId();
        if (!instance.IsRegistered(objectId))
            instance.Register(objectId);
    }

    public override void OnPressEnter(Player player)
    {
        base.OnPressEnter(player);
        instance.GetInstanceHandler().PortToStartPosition(player);
    }

    public override void OnLeaveInstance(Player player)
    {
        base.Unregister(player);
        if (player.IsInGroup())
            PlayerGroupService.RemovePlayer(player);
        else if (player.IsInAlliance())
            PlayerAllianceService.RemovePlayer(player);
    }

    private int GetMaxPlayers(Race race)
    {
        return DataManager.INSTANCE_COOLTIME_DATA.GetMaxMemberCount(agt.GetTemplate().GetInstanceMapId(), race);
    }

    public override int GetMaxPlayers()
    {
        return instance == null ? GetMaxPlayers(Race.ASMODIANS) + GetMaxPlayers(Race.ELYOS) : instance.GetMaxPlayers();
    }
}
