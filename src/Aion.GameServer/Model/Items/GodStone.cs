using System;
using System.Threading;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Items;

/// <summary>Java parity: model/items/GodStone extends ItemStone.</summary>
public class GodStone : ItemStone
{
    private long cooldownExpireTimeMillis; // Java parity: AtomicLong via Volatile
    private readonly Aion.GameServer.Model.Templates.Items.GodstoneInfo godstoneInfo;
    private int activatedCount;

    public GodStone(Item parentItem, int activatedCount, int itemId, Aion.GameServer.Model.Templates.Items.GodstoneInfo godstoneInfo, Aion.GameServer.Model.GameObjects.IPersistable.PersistentState state)
        : base(parentItem.GetObjectId(), itemId, 0, state)
    {
        this.godstoneInfo = godstoneInfo;
        this.activatedCount = activatedCount;
    }

    public Aion.GameServer.Model.Templates.Items.GodstoneInfo GetGodstoneInfo()
    {
        return godstoneInfo;
    }

    public void IncreaseActivatedCount()
    {
        activatedCount++;
        SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public int GetActivatedCount()
    {
        return activatedCount;
    }

    public bool TryActivate(bool isMainHandWeapon, Creature target)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (Aion.GameServer.Configs.Main.CustomConfig.GODSTONE_ACTIVATION_RATE <= 0 || now < Volatile.Read(ref cooldownExpireTimeMillis))
            return false;
        Volatile.Write(ref cooldownExpireTimeMillis, now + Aion.GameServer.Configs.Main.CustomConfig.GODSTONE_EVALUATION_COOLDOWN_MILLIS);

        int procProbability = isMainHandWeapon ? godstoneInfo.GetProbability() : godstoneInfo.GetProbabilityLeft();
        procProbability -= target.GetGameStats().GetStat(StatEnum.PROC_REDUCE_RATE, 0).GetCurrent();
        if (procProbability > 0)
            procProbability = Math.Max(1, (int)Math.Floor(procProbability * Aion.GameServer.Configs.Main.CustomConfig.GODSTONE_ACTIVATION_RATE + 0.5f));

        return Aion.GameServer.Commons.Utils.Rnd.Get(1, 1000) <= procProbability;
    }
}
