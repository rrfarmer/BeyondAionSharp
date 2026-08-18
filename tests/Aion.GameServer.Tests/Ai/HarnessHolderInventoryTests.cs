using System.Reflection;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Which of <see cref="StaticData"/>'s holders the AI harness actually provides, and which it leaves
/// null.
/// </summary>
/// <remarks>
/// <c>BossAiHarness</c> builds its <see cref="StaticData"/> through <c>GetUninitializedObject</c>, which
/// skips the field initializers. That keeps the fixture cheap and leaves <b>most holders null where the
/// real server has them empty</b>. Server code reads holders without null checks — reasonably, because on
/// a real boot they never are — so a class that touches an unprovided holder throws a
/// <c>NullReferenceException</c> whose stack points at the class under test rather than at the fixture.
/// <para>
/// That cost a full pass to diagnose once: <c>WalkManager</c> dereferences <c>WALKER_DATA</c> directly,
/// and the resulting throw looked like a bug in Celestius. Four sound explanations were checked and
/// discarded before the fixture was suspected at all.
/// </para>
/// <para>
/// <b>This is a lookup, not a rule.</b> It does not assert that any particular holder is provided — the
/// fixture provides what the AI tests need and should not carry the rest. It fails only when the list
/// changes, so the count is visible in review, and its message names every null holder so the next
/// mystery NRE can be checked against it in one step.
/// </para>
/// <para>
/// <b>Filling them all was tried and is wrong.</b> Giving every null holder a parameterless instance
/// turns 2,090 passing tests into 1,267 failures: many holders are unusable until their
/// <c>AfterUnmarshal</c> has run, so a blank instance fails <i>differently</i> from a null one rather than
/// more gently. <b>Empty is only safe for a holder whose empty state is meaningful</b>, which has to be
/// decided one at a time — as it was for the walker, material, zone and world-map holders.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class HarnessHolderInventoryTests
{
	/// <summary>Holders the fixture sets, and which every AI test therefore depends on.</summary>
	private static readonly string[] Provided =
	[
		"AbsoluteStatsDataDh", "AiDataDh", "Materials", "NpcDataDh", "NpcSkillDataDh",
		"SkillDataDh", "TribeRelations", "WalkerDataDh", "WorldMaps2", "ZoneInfo",
	];

	[Fact]
	public void TheHarnessProvidesExactlyTheHoldersItIsKnownTo()
	{
		using BossAiHarness harness = BossAiHarness.For(300190000).WithWorldSize(1024)
			.WithAi(typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

		StaticData data = DataManager.GetRegisteredInstance()!.StaticData;

		List<string> provided = [];
		List<string> missing = [];
		foreach (PropertyInfo property in typeof(StaticData)
			.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.OrderBy(p => p.Name, StringComparer.Ordinal))
		{
			if (!property.CanRead || property.GetIndexParameters().Length != 0)
				continue;

			object? value;
			try
			{
				value = property.GetValue(data);
			}
			catch
			{
				continue;
			}

			(value is null ? missing : provided).Add(property.Name);
		}

		Assert.True(
			Provided.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(provided),
			"the harness's provided holders changed.\n"
			+ $"provided: {string.Join(", ", provided)}\n"
			+ $"null ({missing.Count}): {string.Join(", ", missing)}");
	}
}
