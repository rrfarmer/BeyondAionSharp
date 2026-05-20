using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class BrokerItemMaskMatcher
{
	private static readonly IReadOnlyDictionary<int, BrokerPlayerClassFilter> ClassFilters =
		new Dictionary<int, BrokerPlayerClassFilter>
		{
			[6010] = new(1400, "GLADIATOR"),
			[6011] = new(1400, "TEMPLAR"),
			[6012] = new(1400, "ASSASSIN"),
			[6013] = new(1400, "RANGER"),
			[6014] = new(1400, "SORCERER"),
			[6015] = new(1400, "SPIRIT_MASTER"),
			[6016] = new(1400, "CLERIC"),
			[6017] = new(1400, "CHANTER"),
			[6018] = new(1400, "GUNNER"),
			[6019] = new(1400, "BARD"),
			[6048] = new(1400, "RIDER"),
			[6020] = new(1695, "GLADIATOR"),
			[6021] = new(1695, "TEMPLAR"),
			[6022] = new(1695, "ASSASSIN"),
			[6023] = new(1695, "RANGER"),
			[6024] = new(1695, "SORCERER"),
			[6025] = new(1695, "SPIRIT_MASTER"),
			[6026] = new(1695, "CLERIC"),
			[6027] = new(1695, "CHANTER"),
			[6028] = new(1695, "GUNNER"),
			[6029] = new(1695, "BARD"),
			[6049] = new(1695, "RIDER"),
		};

	private static readonly IReadOnlyDictionary<int, BrokerRecipeFilter> RecipeFilters =
		new Dictionary<int, BrokerRecipeFilter>
		{
			[6040] = new(40002, 1522),
			[6041] = new(40003, 1522),
			[6042] = new(40004, 1522),
			[6043] = new(40008, 1522),
			[6044] = new(40007, 1522),
			[6045] = new(40001, 1522),
			[6046] = new(40010, 1522),
		};

	private static readonly IReadOnlyDictionary<int, BrokerTemplateIdFilter> Filters =
		new Dictionary<int, BrokerTemplateIdFilter>
		{
			[9010] = new(1000, 1021),
			[1000] = new([1000]),
			[1001] = new([1001]),
			[1002] = new([1002]),
			[1005] = new([1005]),
			[1006] = new([1006]),
			[1009] = new([1009]),
			[1013] = new([1013]),
			[1015] = new([1015]),
			[1017] = new([1017]),
			[1018] = new([1018]),
			[1019] = new([1019]),
			[1020] = new([1020]),
			[1021] = new([1021]),
			[9020] = new(1100, 1150),
			[8010] = new([1100, 1110, 1120, 1130, 1140]),
			[1100] = new([1100]),
			[1110] = new([1110]),
			[1120] = new([1120]),
			[1130] = new([1130]),
			[1140] = new([1140]),
			[8020] = new([1101, 1111, 1121, 1131, 1141]),
			[1101] = new([1101]),
			[1111] = new([1111]),
			[1121] = new([1121]),
			[1131] = new([1131]),
			[1141] = new([1141]),
			[8030] = new([1103, 1113, 1123, 1133, 1143]),
			[1103] = new([1103]),
			[1113] = new([1113]),
			[1123] = new([1123]),
			[1133] = new([1133]),
			[1143] = new([1143]),
			[8040] = new([1105, 1115, 1125, 1135, 1145]),
			[1105] = new([1105]),
			[1115] = new([1115]),
			[1125] = new([1125]),
			[1135] = new([1135]),
			[1145] = new([1145]),
			[8050] = new([1106, 1116, 1126, 1136, 1146]),
			[1106] = new([1106]),
			[1116] = new([1116]),
			[1126] = new([1126]),
			[1136] = new([1136]),
			[1146] = new([1146]),
			[1150] = new([1150]),
			[9030] = new([1200, 1210, 1220, 1230, 1250, 1871]),
			[1200] = new([1200]),
			[1210] = new([1210]),
			[1220] = new([1220]),
			[1230] = new([1230]),
			[7030] = new([1250]),
			[1871] = new([1871]),
			[9040] = new([1400, 1695]),
			[1400] = new([1400]),
			[1695] = new([1695]),
			[9070] = new([1710, 1711]),
			[1710] = new([1710]),
			[1711] = new([1711]),
			[9080] = new([1700, 1701, 1702, 1703, 1704]),
			[1703] = new([1703]),
			[8070] = new([1700, 1701, 1702]),
			[1700] = new([1700]),
			[1701] = new([1701]),
			[1702] = new([1702]),
			[1704] = new([1704]),
			[9050] = new([1520, 1522]),
			[1520] = new([1520]),
			[6030] = new(ExtraMasks: new HashSet<int> { 15200 }),
			[6031] = new(ExtraMasks: new HashSet<int> { 15201 }),
			[6032] = new(ExtraMasks: new HashSet<int> { 15202 }),
			[1522] = new([1522]),
			[9060] = new([1410, 1600, 1620, 1640, 1660, 1661, 1665, 1670, 1680, 1690, 1692, 1693, 1694, 1696]),
			[1600] = new([1600]),
			[1620] = new([1620]),
			[7060] = new([1640]),
			[8060] = new([1660, 1665, 1670, 1680, 1692, 1691]),
			[1660] = new(ExtraMasks: new HashSet<int> { 16600, 16602 }),
			[1670] = new([1670]),
			[7065] = new(ExtraMasks: new HashSet<int> { 16603 }),
			[1680] = new([1680]),
			[7061] = new([1692]),
			[7064] = new([1691]),
			[1665] = new([1665]),
			[7063] = new([1661]),
			[7062] = new([1410, 1690, 1693, 1694, 1696]),
			[7070] = new([1850, 1860, 1870, 1880, 1881, 1887]),
		};

	public static bool Matches(int brokerMask, ItemTemplateSummary template, RecipeTemplateTable? recipes = null)
	{
		// Java parity: model/broker/BrokerItemMask with BrokerPlayerClassExtraFilter and BrokerRecipeFilter specializations.
		if (ClassFilters.TryGetValue(brokerMask, out var classFilter))
			return classFilter.Matches(template);

		if (RecipeFilters.TryGetValue(brokerMask, out var recipeFilter))
			return recipeFilter.Matches(template, recipes);

		return Filters.TryGetValue(brokerMask, out var filter) && filter.Matches(template.TemplateId);
	}

	private sealed record BrokerPlayerClassFilter(int TemplateMask, string PlayerClass)
	{
		public bool Matches(ItemTemplateSummary template)
		{
			return TemplateMask == template.TemplateId / 100000
				&& template.IsClassSpecific(PlayerClass);
		}
	}

	private sealed record BrokerRecipeFilter(int CraftSkillId, int TemplateMask)
	{
		public bool Matches(ItemTemplateSummary template, RecipeTemplateTable? recipes)
		{
			if (recipes == null || TemplateMask != template.TemplateId / 100000 || template.CraftLearnRecipeId == 0)
				return false;

			var recipe = recipes.GetRecipeTemplateById(template.CraftLearnRecipeId);
			return recipe?.SkillId == CraftSkillId;
		}
	}

	private sealed record BrokerTemplateIdFilter(
		IReadOnlySet<int>? TemplateMasks = null,
		IReadOnlySet<int>? ExtraMasks = null,
		int? MinTemplateMask = null,
		int? MaxTemplateMask = null)
	{
		public BrokerTemplateIdFilter(params int[] templateMasks)
			: this(templateMasks.ToHashSet())
		{
		}

		public BrokerTemplateIdFilter(int minTemplateMask, int maxTemplateMask)
			: this(MinTemplateMask: minTemplateMask, MaxTemplateMask: maxTemplateMask)
		{
		}

		public bool Matches(int templateId)
		{
			var templateMask = templateId / 100000;
			if (TemplateMasks?.Contains(templateMask) == true)
				return true;
			if (MinTemplateMask.HasValue && MaxTemplateMask.HasValue && templateMask >= MinTemplateMask.Value && templateMask <= MaxTemplateMask.Value)
				return true;
			return ExtraMasks?.Contains(templateId / 10000) == true;
		}
	}
}
