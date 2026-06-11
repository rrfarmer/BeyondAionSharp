using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Playerreward;

namespace Aion.GameServer.Model.Instance.Instancescore;

/// <summary>Java parity: model/instance/instancescore/InstanceScore.</summary>
public class InstanceScore<T> where T : InstancePlayerReward
{
    private readonly ConcurrentDictionary<int, T> playerRewards = new ConcurrentDictionary<int, T>();
    private InstanceProgressionType instanceProgressionType = InstanceProgressionType.START_PROGRESS;

    public ICollection<T> GetPlayerRewards()
    {
        return playerRewards.Values;
    }

    public bool ContainsPlayer(int objectId)
    {
        return playerRewards.ContainsKey(objectId);
    }

    public void RemovePlayerReward(T reward)
    {
        playerRewards.TryRemove(reward.GetOwnerId(), out _);
    }

    public T GetPlayerReward(int objectId)
    {
        return playerRewards.TryGetValue(objectId, out var v) ? v : default;
    }

    public void AddPlayerReward(T reward)
    {
        playerRewards[reward.GetOwnerId()] = reward;
    }

    public void SetInstanceProgressionType(InstanceProgressionType instanceProgressionType)
    {
        this.instanceProgressionType = instanceProgressionType;
    }

    public InstanceProgressionType GetInstanceProgressionType()
    {
        return instanceProgressionType;
    }

    public bool IsRewarded()
    {
        return instanceProgressionType.IsEndProgress();
    }

    public bool IsReinforcing()
    {
        return instanceProgressionType.IsReinforcing();
    }

    public bool IsPreparing()
    {
        return instanceProgressionType.IsPreparing();
    }

    public bool IsStartProgress()
    {
        return instanceProgressionType.IsStartProgress();
    }

    public virtual void Clear()
    {
        playerRewards.Clear();
    }
}
