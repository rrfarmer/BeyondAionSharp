using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcEventDropRuleServiceTests
{
	[Fact]
	public void GetActiveEventDropRules_ReturnsRulesForActiveEventsOnly()
	{
		var activeRule = CreateRule("active-drop");
		var inactiveRule = CreateRule("future-drop");
		var service = new WorldNpcEventDropRuleService(
			new EventDropTable(
			[
				new EventTemplateSummary(
					"Active Event",
					new DateTime(2026, 5, 1, 0, 0, 0),
					new DateTime(2026, 5, 30, 0, 0, 0),
					"",
					[activeRule]),
				new EventTemplateSummary(
					"Future Event",
					new DateTime(2026, 6, 1, 0, 0, 0),
					new DateTime(2026, 6, 30, 0, 0, 0),
					"",
					[inactiveRule]),
			]),
			now: () => new DateTime(2026, 5, 23, 12, 0, 0));

		var rules = service.GetActiveEventDropRules();

		var rule = Assert.Single(rules);
		Assert.Equal("active-drop", rule.RuleName);
	}

	[Fact]
	public void GetActiveEventDropRules_AppliesDisabledEventNamesAndWildcard()
	{
		var events = new EventDropTable(
		[
			new EventTemplateSummary(
				"Active Event",
				new DateTime(2026, 5, 1, 0, 0, 0),
				new DateTime(2026, 5, 30, 0, 0, 0),
				"",
				[CreateRule("active-drop")]),
		]);

		var namedDisabled = new WorldNpcEventDropRuleService(
			events,
			now: () => new DateTime(2026, 5, 23, 12, 0, 0),
			disabledEventNames: ["Active Event"]);
		var wildcardDisabled = new WorldNpcEventDropRuleService(
			events,
			now: () => new DateTime(2026, 5, 23, 12, 0, 0),
			disabledEventNames: ["*"]);

		Assert.Empty(namedDisabled.GetActiveEventDropRules());
		Assert.Empty(wildcardDisabled.GetActiveEventDropRules());
	}

	private static GlobalDropRuleSummary CreateRule(string name)
	{
		return new GlobalDropRuleSummary(
			name,
			Chance: 100,
			DynamicChance: false,
			MinDiff: -99,
			MaxDiff: 99,
			RestrictionRace: "",
			UseLevelBasedChanceReduction: false,
			MemberLimit: 1,
			MaxDropRule: 1,
			Items: [],
			WorldTypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			Races: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			Ratings: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			MapIds: new HashSet<int>(),
			Tribes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			NpcIds: new HashSet<int>(),
			NpcNames: [],
			NpcGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			ExcludedNpcIds: new HashSet<int>(),
			Zones: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
	}
}
