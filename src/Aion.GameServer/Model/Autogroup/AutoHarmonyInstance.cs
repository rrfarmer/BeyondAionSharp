using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>Java parity: model/autogroup/AutoHarmonyInstance (xTz, Estrayl) : AutoInstance. 2 fixed groups; synchronized→lock(this); Map.Entry→KeyValuePair (nullable for getGroupEntry); List.getFirst→[0]; (byte)7→(sbyte)7; AGPlayer record p.race()/objectId()→Race/ObjectId. HarmonyArenaScore/HarmonyGroupReward/PlayerGroupService red-tolerated.</summary>
public class AutoHarmonyInstance : AutoInstance
{
    private readonly Dictionary<int, List<AGPlayer>> groups = new();

    public AutoHarmonyInstance(AutoGroupType agt) : base(agt)
    {
        groups[0] = new List<AGPlayer>();
        groups[1] = new List<AGPlayer>();
    }

    public override void OnInstanceCreate(WorldMapInstance instance)
    {
        base.OnInstanceCreate(instance);
        HarmonyArenaScore score = (HarmonyArenaScore)instance.GetInstanceHandler().GetInstanceScore();
        score.SetDifficultyId(agt.GetDifficultId());
    }

    public override AGQuestion AddLookingForParty(LookingForParty lookingForParty)
    {
        lock (this)
        {
            if (IsRegistrationDisabled(lookingForParty) || registeredAGPlayers.Count >= GetMaxPlayers())
                return AGQuestion.FAILED;

            AGQuestion question = CanAddParty(groups[0], lookingForParty);
            if (question == AGQuestion.FAILED)
                question = CanAddParty(groups[1], lookingForParty);
            return question;
        }
    }

    public override void OnPressEnter(Player player)
    {
        if (agt.IsHarmonyArena())
        {
            if (!RemoveItem(player, 186000184, 1))
            {
                registeredAGPlayers.TryRemove(player.GetObjectId(), out _);
                PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(agt.GetTemplate().GetMaskId(), 5));
                if (registeredAGPlayers.Count == 0)
                    AutoGroupService.GetInstance().DestroyIfPossible(this);
                return;
            }
        }
        ((HarmonyArenaScore)instance.GetInstanceHandler().GetInstanceScore()).PortToPosition(player);
        instance.Register(player.GetObjectId());
    }

    public override void OnEnterInstance(Player player)
    {
        base.OnEnterInstance(player);
        if (player.IsInGroup())
        {
            return;
        }
        int playerId = player.GetObjectId();
        KeyValuePair<int, List<AGPlayer>>? groupEntry = GetGroupEntry(playerId);
        if (groupEntry == null)
            return;

        HarmonyArenaScore score = (HarmonyArenaScore)instance.GetInstanceHandler().GetInstanceScore();
        List<Player> players = FindPlayersInInstance(groupEntry.Value.Value);
        players.Remove(player);

        if (players.Count == 0) // Create Group
        {
            PlayerGroup newGroup = PlayerGroupService.CreateGroup(player, player, TeamType.AUTO_GROUP, 0);
            int groupId = newGroup.GetObjectId();
            if (!instance.IsRegistered(groupId))
            {
                instance.Register(groupId);
                HarmonyGroupReward reward = new(groupEntry.Value.Key, 12000, (sbyte)7, groupId);
                reward.AddPlayer(registeredAGPlayers.GetValueOrDefault(player.GetObjectId()));
                score.AddHarmonyGroup(reward);
            }
        }
        else // Add To Group
        {
            PlayerGroup pg = players[0].GetPlayerGroup();
            PlayerGroupService.AddPlayer(pg, player);
            HarmonyGroupReward reward = score.GetGroupReward(pg.GetLeader().GetObjectId());
            reward.AddPlayer(registeredAGPlayers.GetValueOrDefault(player.GetObjectId()));
        }

        if (!instance.IsRegistered(playerId))
        {
            instance.Register(playerId);
        }
    }

    public override void OnLeaveInstance(Player player)
    {
        Unregister(player);
        PlayerGroupService.RemovePlayer(player);
    }

    public override void Unregister(Player player)
    {
        AGPlayer agp = registeredAGPlayers.GetValueOrDefault(player.GetObjectId());
        if (agp != null)
        {
            groups[0].Remove(agp);
            groups[1].Remove(agp);
        }
        base.Unregister(player);
    }

    private List<Player> FindPlayersInInstance(List<AGPlayer> group)
    {
        List<Player> players = new();
        foreach (AGPlayer agp in group)
        {
            foreach (Player p in instance.GetPlayersInside())
            {
                if (p.GetObjectId() == agp.ObjectId)
                {
                    players.Add(p);
                    break;
                }
            }
        }
        return players;
    }

    private KeyValuePair<int, List<AGPlayer>>? GetGroupEntry(int playerObjId)
    {
        AGPlayer agp = registeredAGPlayers.GetValueOrDefault(playerObjId);
        if (agp != null)
        {
            foreach (KeyValuePair<int, List<AGPlayer>> entry in groups)
            {
                if (entry.Value.Contains(agp))
                    return entry;
            }
        }
        return null;
    }

    private AGQuestion CanAddParty(List<AGPlayer> group, LookingForParty lfp)
    {
        if (group.Count + lfp.GetMembers().Count > 3)
            return AGQuestion.FAILED;
        if (group.Count != 0 && group[0].Race != lfp.GetRace())
            return AGQuestion.FAILED;

        foreach (KeyValuePair<int, AGPlayer> entry in lfp.GetMembers())
        {
            group.Add(entry.Value);
            registeredAGPlayers[entry.Key] = entry.Value;
        }
        return instance != null ? AGQuestion.ADDED : registeredAGPlayers.Count == GetMaxPlayers() ? AGQuestion.READY : AGQuestion.ADDED;
    }
}
