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
/// So the source is a hook. Nothing but the harness sets it, and it sets it to the same clock its
/// scheduler runs on, which makes engine time and scheduled time the same thing inside a test.
/// <para>
/// Named <c>SystemClock</c> rather than <c>GameTime</c> because this port already has a
/// <c>Utils.Time.Gametime.GameTime</c>, which is the in-game day and night rather than the wall clock.
/// </para>
/// </para>
/// </remarks>
public static class SystemClock
{
    /// <summary>
    /// The replacement clock, per execution context rather than per process.
    /// </summary>
    /// <remarks>
    /// <b>A plain static field was tried first and had to be reverted.</b> One test's harness could
    /// leave the source pointing at its own scheduler after another had taken over, and a gargoyle pin
    /// failed about one full-suite run in five while passing every isolated run. <c>AsyncLocal</c> scopes
    /// the override to the flow that set it, so a harness cannot reach outside its own test.
    /// <para>
    /// Null means "nobody overrode it", which is the production case and every non-harness test.
    /// </para>
    /// </remarks>
    private static readonly AsyncLocal<Func<long>?> Source = new();

    /// <summary>Milliseconds since the epoch, as Java's <c>System.currentTimeMillis()</c> reports them.</summary>
    public static long CurrentMillis() =>
        Source.Value?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Points the clock at another source, for this execution context. Tests only.</summary>
    public static void UseSource(Func<long> replacement) => Source.Value = replacement;

    /// <summary>Puts the real clock back for this execution context.</summary>
    public static void UseSystemClock() => Source.Value = null;
}
