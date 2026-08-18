using System.Runtime.CompilerServices;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

/// <summary>
/// Initialises <see cref="SiegeService"/> once, harmlessly, before any test in this assembly runs.
/// </summary>
/// <remarks>
/// <b>This is the flake that took five attempts.</b> <c>SiegeService</c> is a singleton built in a
/// static field initialiser, and its constructor takes the live path when <c>SiegeConfig.SIEGE_ENABLED</c>
/// is true — which is the default. That path reads <c>DataManager.SIEGE_LOCATION_DATA</c> and calls
/// <c>SiegeDAO</c>, so whether it succeeds depends on which <c>DataManager</c> happens to be registered
/// at the moment the type is first touched.
/// <para>
/// A static type initialiser runs <b>once per process</b>, and if it throws, the type is poisoned for
/// the rest of that process: every later access throws <c>TypeInitializationException</c>. So one
/// unlucky ordering breaks every test that afterwards reaches <c>NpcAI.Ask(ALLOW_RESPAWN)</c> — a
/// different test each run, with no random branch on its own path, unaffected by parallelism, and
/// green whenever the suite is run alone.
/// </para>
/// <para>
/// Four earlier explanations were wrong: a probabilistic window, a race against an aggro scan, two
/// classes outside the serialising collection, and parallelism itself. The first of those was right in
/// <em>kind</em> and applied to one genuinely under-powered pin; none of them was this. What finally
/// found it was reading the exception text instead of the assertion — the failure had been reporting
/// its own cause all along, in a message the earlier runs filtered out with <c>grep Assert</c>.
/// </para>
/// <para>
/// The fix is to make the ordering irrelevant: turn sieges off, touch the type so its initialiser runs
/// on the harmless branch, and put the flag back. A module initialiser is the only hook that reliably
/// beats every test in the assembly to it.
/// </para>
/// <para>
/// <b>Not a production fix.</b> On a real server <c>SiegeService</c> is touched after the data and the
/// database are up, which is why this has never been seen outside the suite. That the type is
/// permanently poisoned by one bad early access is still a sharp edge, and worth revisiting on its own
/// terms — see docs/retail-ai-fidelity.md.
/// </para>
/// </remarks>
internal static class SiegeServiceTestInit
{
	[ModuleInitializer]
	internal static void PrimeSiegeService()
	{
		bool previous = SiegeConfig.SIEGE_ENABLED;
		SiegeConfig.SIEGE_ENABLED = false;
		try
		{
			// Touching it is the point: this runs the static initialiser now, on the branch that
			// needs neither the data nor the database.
			_ = SiegeService.GetInstance();
		}
		finally
		{
			SiegeConfig.SIEGE_ENABLED = previous;
		}
	}
}
