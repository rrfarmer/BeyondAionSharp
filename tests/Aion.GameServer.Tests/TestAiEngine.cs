using Aion.GameServer.Ai;

namespace Aion.GameServer.Tests;

/// <summary>
/// Coordinates the two ways tests populate the process-global <see cref="AIEngine"/> registry.
/// </summary>
/// <remarks>
/// <para>
/// <c>AIEngine</c> is a singleton with no unregister, and <c>RegisterAI</c> throws on a duplicate name, so the
/// two strategies in this suite collide head-on: <c>BossAiHarness</c> hand-registers the handful of handlers an
/// encounter needs (fast, and it skips the whole-assembly scan), while the boot tests run the production
/// <c>AIEngine.Init()</c>, which binds every <c>[AIName]</c> handler there is. Whichever ran second used to blow
/// up — <c>Init()</c> on "Duplicate AIs with name adjutantanuhart", a later harness on a name <c>Init()</c> had
/// already taken — and the failure landed in whatever test xUnit happened to order last, not in the one that
/// caused it.
/// </para>
/// <para>
/// Routing both through here removes the ordering question: once the full registry is loaded, hand-registration
/// is a no-op because the full set is a superset of any handful; and a full load that arrives after some
/// hand-registration goes through <c>Reload()</c>, which clears before re-binding, rather than <c>Init()</c>,
/// which would trip over its own predecessors.
/// </para>
/// <para>
/// A full load needs the real NPC templates: <c>ValidateScripts</c> checks every <c>ai_name</c> in npc_templates
/// against the registry, so register a real <see cref="Dataholders.DataManager"/> before calling
/// <see cref="EnsureAllRegistered"/>.
/// </para>
/// <para>
/// What this cannot do is take handlers back out — <c>AIEngine</c> has no unregister. So the set a harness
/// declares is a floor, not a ceiling: once a boot test has loaded the full registry, a harness test that
/// declared two handlers can still resolve any of the rest. A test that means to prove an encounter needs a
/// particular handler therefore cannot lean on the declaration alone.
/// </para>
/// </remarks>
internal static class TestAiEngine
{
	private static readonly object Gate = new();
	private static readonly HashSet<Type> HandRegistered = new();
	private static bool _allRegistered;

	/// <summary>Binds just <paramref name="aiHandlerTypes"/>, unless the whole registry is already loaded.</summary>
	internal static void Register(IEnumerable<Type> aiHandlerTypes)
	{
		lock (Gate)
		{
			if (_allRegistered)
				return;

			foreach (Type type in aiHandlerTypes)
			{
				if (HandRegistered.Add(type))
					AIEngine.GetInstance().RegisterAI(type);
			}
		}
	}

	/// <summary>Binds every <c>[AIName]</c> handler, as the production boot does, at most once per test process.</summary>
	internal static void EnsureAllRegistered()
	{
		lock (Gate)
		{
			if (_allRegistered)
				return;

			if (HandRegistered.Count > 0)
				AIEngine.GetInstance().Reload(); // clears first, so the hand-registered handful is not a duplicate
			else
				AIEngine.GetInstance().Init();

			_allRegistered = true;
		}
	}
}
