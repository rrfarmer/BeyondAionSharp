using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>Java parity: model/autogroup/LookingForParty (xTz). implements Comparable→IComparable; stream map/collect(toMap)→Select/ToDictionary; Map.of→single-entry Dictionary; ert.ordinal()→(int)ert; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds. EntryRequestType/AGPlayer green; player.GetCurrentTeam red-tolerated.</summary>
public class LookingForParty : IComparable<LookingForParty>
{
    private readonly Dictionary<int, AGPlayer> members;
    private readonly EntryRequestType ert;
    private readonly Race race;
    private readonly long registrationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private readonly int maskId;
    private long startEnterTime;
    private int leaderObjId;

    public LookingForParty(Player player, EntryRequestType ert, int maskId)
    {
        this.members = CreateMembers(player);
        this.ert = ert;
        this.race = player.GetRace();
        this.maskId = maskId;
        this.leaderObjId = player.GetObjectId();
    }

    private Dictionary<int, AGPlayer> CreateMembers(Player player)
    {
        if (player.IsInTeam())
        {
            return player.GetCurrentTeam().GetOnlineMembers().Select(p => new AGPlayer(p))
                .ToDictionary(a => a.ObjectId, a => a);
        }
        return new Dictionary<int, AGPlayer> { [player.GetObjectId()] = new AGPlayer(player) };
    }

    public Dictionary<int, AGPlayer> GetMembers()
    {
        return members;
    }

    public bool IsMember(int objectId)
    {
        return members.GetValueOrDefault(objectId) != null;
    }

    public void UnregisterMember(int objectId)
    {
        members.Remove(objectId);
    }

    public EntryRequestType GetEntryRequestType()
    {
        return ert;
    }

    public Race GetRace()
    {
        return race;
    }

    public long GetRegistrationTime()
    {
        return registrationTime;
    }

    public int GetMaskId()
    {
        return maskId;
    }

    public int GetLeaderObjId()
    {
        return leaderObjId;
    }

    public void SetLeaderObjId(int leaderObjId)
    {
        this.leaderObjId = leaderObjId;
    }

    public bool IsLeader(int objectId)
    {
        return objectId == leaderObjId;
    }

    public void SetStartEnterTime()
    {
        startEnterTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public bool IsOnStartEnterTask()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startEnterTime <= 120000;
    }

    public int CompareTo(LookingForParty lfp)
    {
        if (ert != lfp.ert)
            return (int)lfp.ert - (int)ert;

        int memberDiff = lfp.GetMembers().Count - members.Count;
        if (memberDiff != 0)
            return memberDiff;

        return (int)(registrationTime - lfp.GetRegistrationTime());
    }
}
