using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The one thing about <c>set_idle_timer</c> this port has not settled, guarded until it is.
/// </summary>
/// <remarks>
/// Retail uses <c>set_idle_timer</c> 6,093 times and <b>1,090 of those carry <c>delay=0</c> — 1,006 of
/// them inside <c>on_idle_timer</c> itself</b>, re-arming the very timer that just fired.
/// <para>
/// <c>PatternAi.SetIdleTimer</c> documents zero as "next tick" and schedules rather than running inline.
/// <b>Nothing has ever exercised that reading</b>: six classes use the timer and every one passes a real
/// delay. If zero does mean next tick, each of those 1,006 rungs is a loop that fires every tick
/// forever — and the ones in the port's own backlog spawn an npc each time round, so the first class to
/// port one would spin the server rather than fail a test.
/// </para>
/// <para>
/// Zero could as easily mean "stop", or "fall back to the engine's own idle period". The dump does not
/// say, and Panesterra's rebirth doors — the largest family waiting on it — are unportable either way
/// until it does. This test fails the moment someone passes zero, so the question gets answered
/// deliberately instead of discovered in a running server.
/// </para>
/// </remarks>
public sealed class IdleTimerSemanticsTests
{
	[Fact]
	public void NoHandlerArmsTheIdleTimerWithZero()
	{
		string root = Path.Combine(BossAiHarness.RepoRoot(), "src", "Aion.GameServer", "Handlers", "AI");
		List<string> offenders = new List<string>();

		foreach (string file in Directory.EnumerateFiles(root, "*.cs"))
		{
			string text = File.ReadAllText(file);
			foreach (Match armed in Regex.Matches(text, @"SetIdleTimer\(\s*(-?\d+)\s*[,)]"))
			{
				if (int.Parse(armed.Groups[1].Value) <= 0)
					offenders.Add($"{Path.GetFileName(file)}: {armed.Value}");
			}
		}

		Assert.Empty(offenders);
	}

	/// <summary><b>And the timer still works for the delays that are settled.</b></summary>
	[Fact]
	public void TheBeaconsStillArmARealDelay()
	{
		foreach ((int _, TiamatBeacons.Breath breath) in TiamatBeacons.ByBeacon)
			Assert.True(breath.DelayMillis > 0);
	}
}
