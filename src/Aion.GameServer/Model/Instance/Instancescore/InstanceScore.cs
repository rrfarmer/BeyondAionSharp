using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Playerreward;

namespace Aion.GameServer.Model.Instance.Instancescore;

/// <summary>
/// Java parity: model/instance/instancescore/InstanceScore. Non-generic base holding the
/// progression-state surface used through Java's raw <c>InstanceScore</c> (C# has no raw generic,
/// so the raw-typed surface lives here and the player-reward-typed surface in InstanceScore&lt;T&gt;).
/// </summary>
public abstract class InstanceScore
{
    protected InstanceProgressionType instanceProgressionType = InstanceProgressionType.START_PROGRESS;

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
    }
}

/// <summary>Java parity: InstanceScore&lt;T extends InstancePlayerReward&gt; — the player-reward-typed surface.</summary>
public class InstanceScore<T> : InstanceScore where T : InstancePlayerReward
{
    private readonly ConcurrentDictionary<int, T> playerRewards = new ConcurrentDictionary<int, T>();

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

    public override void Clear()
    {
        playerRewards.Clear();
    }
}
