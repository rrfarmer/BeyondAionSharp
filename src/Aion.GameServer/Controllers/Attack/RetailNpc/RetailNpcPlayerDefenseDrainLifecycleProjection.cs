namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline 17860 StatDown vector arithmetic. The native effect
/// manager controls calls, lifetime, recipient and ordering; this class does
/// not start an effect or assert that any effect is active.</summary>
internal static class RetailNpcPlayerDefenseDrainLifecycleProjection
{
    internal enum Invocation { Begin, End, Resume }
    internal readonly record struct Vector(int Physical,int Water,int Wind,int Earth,int Fire,int Light,int Dark,int MagicalResistance);

    internal static Vector ApplyForOfflineFixture(ReadOnlySpan<byte> source17860,Vector current,Invocation invocation)
    {
        int sourcePercent=RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(source17860);
        int delta=invocation switch {Invocation.Begin or Invocation.Resume=>sourcePercent,
            Invocation.End=>unchecked(-sourcePercent),_=>throw new ArgumentOutOfRangeException(nameof(invocation))};
        // Retail PMDefend distributes the same signed accumulator delta to
        // physical defense and all six element channels. Magic resistance is
        // a separate channel and is not changed by this token.
        return new(unchecked(current.Physical+delta),unchecked(current.Water+delta),unchecked(current.Wind+delta),
            unchecked(current.Earth+delta),unchecked(current.Fire+delta),unchecked(current.Light+delta),
            unchecked(current.Dark+delta),current.MagicalResistance);
    }
}
