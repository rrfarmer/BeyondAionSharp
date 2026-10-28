namespace Aion.GameServer.Utils;

/// <summary>
/// The wall clock the combat model reads for "how long ago did that happen".
/// </summary>
/// <remarks>
/// Java calls <c>System.currentTimeMillis()</c> at each of these sites and this port copied it — four
/// private <c>CurrentTimeMillis()</c> helpers, one each in <c>Creature</c>, <c>NpcGameStats</c>,
/// <c>PlayerController</c> and <c>Skill</c>. In production that is exactly right and this changes
/// nothing: the default is the same call.
/// <para>
/// <b>It matters under a virtual clock.</b> <c>BossAiHarness</c> advances a scheduler by minutes in a
/// few milliseconds of real time, so every one of those timestamps stayed where it was: cooldowns never
/// elapsed, <c>CanUseNextSkill</c> stayed false once a skill had set a delay, and an npc's own skill
/// rotation could not run. Pins that drove a clock past a cooldown were quietly testing a world where
/// no cooldown ever passes — which is how two attempts to pin a cast cadence came back saying nothing.
/// </para>
/// <para>
/// So the source is a hook. Harness/SIM infrastructure sets it to the same clock its scheduler runs on,
/// which makes engine time and scheduled time the same thing inside a deterministic host.
/// <para>
/// Named <c>SystemClock</c> rather than <c>GameTime</c> because this port already has a
/// <c>Utils.Time.Gametime.GameTime</c>, which is the in-game day and night rather than the wall clock.
/// </para>
/// </para>
/// </remarks>
public static class SystemClock
{
    /// <summary>
    /// The highest-priority replacement clock, scoped to the current execution context.
    /// </summary>
    /// <remarks>
    /// <b>A plain static field was tried first and had to be reverted.</b> One test's harness could
    /// leave the source pointing at its own scheduler after another had taken over, and a gargoyle pin
    /// failed about one full-suite run in five while passing every isolated run. <c>AsyncLocal</c> scopes
    /// the override to the flow that set it, so a harness cannot reach outside its own test.
    /// <para>
    /// Null means "this flow did not override it"; the process-wide source is considered next.
    /// </para>
    /// </remarks>
    private static readonly AsyncLocal<Func<long>?> Source = new();

    /// <summary>
    /// The host-wide fallback used by deterministic work which does not inherit the caller's execution context.
    /// </summary>
    private static Func<long>? _processSource;

    /// <summary>Milliseconds since the epoch, as Java's <c>System.currentTimeMillis()</c> reports them.</summary>
    public static long CurrentMillis() =>
        Source.Value?.Invoke()
        ?? Volatile.Read(ref _processSource)?.Invoke()
#pragma warning disable RS0030 // The clock seam is the single production owner of wall-clock reads.
        ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
#pragma warning restore RS0030

    /// <summary>The current UTC instant derived from <see cref="CurrentMillis"/>.</summary>
    public static DateTimeOffset UtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(CurrentMillis());

    /// <summary>Whole seconds since the epoch, matching Java's millisecond timestamp truncation.</summary>
    public static long CurrentSeconds() => CurrentMillis() / 1000;

    /// <summary>Retail NPC GetTickCount's unsigned32 monotonic clock. Deterministic
    /// hosts still supply their one scheduler clock; legacy wall-clock callers
    /// and unrelated player/PvP behavior retain CurrentMillis.</summary>
    public static uint RetailTick32() => unchecked((uint)(
        Source.Value?.Invoke()
        ?? Volatile.Read(ref _processSource)?.Invoke()
        ?? Environment.TickCount64));

    /// <summary>Points the clock at another source, for this execution context. Tests only.</summary>
    public static void UseSource(Func<long> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Source.Value = replacement;
    }

    /// <summary>Clears the execution-context override, revealing the process source or real clock beneath it.</summary>
    public static void UseSystemClock() => Source.Value = null;

    /// <summary>Sets the fallback source for the whole process; an execution-context override still wins.</summary>
    public static void SetProcessSource(Func<long> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Volatile.Write(ref _processSource, replacement);
    }

    /// <summary>Clears the process-wide override without changing any execution-context override.</summary>
    public static void UseSystemClockProcessWide() => Volatile.Write(ref _processSource, null);
}
