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
    private static Func<long> source = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Milliseconds since the epoch, as Java's <c>System.currentTimeMillis()</c> reports them.</summary>
    public static long CurrentMillis() => source();

    /// <summary>Points the clock at another source. Tests only; production never calls this.</summary>
    public static void UseSource(Func<long> replacement) => source = replacement;

    /// <summary>Puts the real clock back.</summary>
    public static void UseSystemClock() =>
        source = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
