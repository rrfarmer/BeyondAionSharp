namespace Aion.GameServer.Services;

public sealed class QuestBonusItemGroupTable
{
	private readonly IReadOnlyDictionary<string, IReadOnlyList<QuestBonusItemGroupProjection>> _groupsByBonusType;

	public QuestBonusItemGroupTable(IEnumerable<QuestBonusItemGroupProjection> groups)
	{
		ArgumentNullException.ThrowIfNull(groups);

		// Java parity: dataholders/ItemGroupsData supplies ordered groups to
		// services/reward/BonusService#getBonusGroups. This table only wraps the
		// already-projected supported quest bonus groups; global loading remains disabled.
		Groups = groups.ToArray();
		_groupsByBonusType = Groups
			.GroupBy(group => Normalize(group.BonusType), StringComparer.Ordinal)
			.ToDictionary(
				group => group.Key,
				group => (IReadOnlyList<QuestBonusItemGroupProjection>)group.ToArray(),
				StringComparer.Ordinal);
	}

	public IReadOnlyList<QuestBonusItemGroupProjection> Groups { get; }

	public int Count => Groups.Count;

	public int ItemCount => Groups.Sum(group => group.Items.Count);

	public IReadOnlySet<string> BonusTypes => _groupsByBonusType.Keys.ToHashSet(StringComparer.Ordinal);

	public IReadOnlyList<QuestBonusItemGroupProjection> GetGroupsByBonusType(string? bonusType)
	{
		return _groupsByBonusType.GetValueOrDefault(Normalize(bonusType)) ?? [];
	}

	public static QuestBonusItemGroupTable FromXml(string xmlContent)
	{
		ArgumentNullException.ThrowIfNull(xmlContent);

		return new QuestBonusItemGroupTable(new QuestBonusItemGroupXmlProjectionExtractor().ExtractSupportedGroups(xmlContent));
	}

	public static QuestBonusItemGroupTable FromXml(Stream stream)
	{
		ArgumentNullException.ThrowIfNull(stream);

		return new QuestBonusItemGroupTable(new QuestBonusItemGroupXmlProjectionExtractor().ExtractSupportedGroups(stream));
	}

	private static string Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
}
