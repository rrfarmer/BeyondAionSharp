namespace Aion.GameServer.Model.Items;

/// <summary>
/// Reuse cooldown for an item (next-reuse time + use delay).
/// Java parity: model/items/ItemCooldown.
/// </summary>
public class ItemCooldown
{
    private readonly long _time;
    private readonly int _useDelay;

    public ItemCooldown(long time, int useDelay)
    {
        _time = time;
        _useDelay = useDelay;
    }

    // Java parity: getReuseTime()
    public long GetReuseTime() => _time;

    // Java parity: getUseDelay()
    public int GetUseDelay() => _useDelay;
}
