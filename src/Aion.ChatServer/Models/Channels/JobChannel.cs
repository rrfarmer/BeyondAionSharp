namespace Aion.ChatServer.Models.Channels;

public sealed class JobChannel : RaceChannel
{
	private static readonly List<IReadOnlySet<string>> AliasSets =
	[
		NewOrderedSet("Gladiator"),
		NewOrderedSet("Templar"),
		NewOrderedSet("Assassin"),
		NewOrderedSet("Ranger"),
		NewOrderedSet("Sorcerer"),
		NewOrderedSet("Spiritmaster"),
		NewOrderedSet("Cleric"),
		NewOrderedSet("Chanter"),
		NewOrderedSet("Aethertech"),
		NewOrderedSet("Gunslinger", "Gunner"),
		NewOrderedSet("Songweaver", "Bard"),
	];

	private readonly IReadOnlySet<string> _classIdentifiers;

	public JobChannel(int gameServerId, Race race, string classIdentifier)
		: base(ChannelType.Job, gameServerId, race)
	{
		_classIdentifiers = WithAliases(classIdentifier.Split("[f:", StringSplitOptions.None)[0]);
	}

	public bool HasAliases => _classIdentifiers.Count > 1;

	public override bool Matches(ChannelType channelType, int gameServerId, Race race, string classIdentifier)
	{
		return _classIdentifiers.Contains(classIdentifier) && base.Matches(channelType, gameServerId, race, classIdentifier);
	}

	public override string Name()
	{
		return $"{_classIdentifiers.First()} ({Race.ToString()[0]})";
	}

	private static IReadOnlySet<string> WithAliases(string classIdentifier)
	{
		return AliasSets.FirstOrDefault(aliases => aliases.Contains(classIdentifier)) ?? NewOrderedSet(classIdentifier);
	}

	private static IReadOnlySet<string> NewOrderedSet(params string[] values)
	{
		return new LinkedHashSet<string>(values);
	}

	private sealed class LinkedHashSet<T> : List<T>, IReadOnlySet<T>
	{
		public LinkedHashSet(IEnumerable<T> values)
		{
			foreach (var value in values)
			{
				if (!Contains(value))
					Add(value);
			}
		}

		bool IReadOnlySet<T>.Contains(T item) => Contains(item);

		public bool IsProperSubsetOf(IEnumerable<T> other) => this.ToHashSet().IsProperSubsetOf(other);

		public bool IsProperSupersetOf(IEnumerable<T> other) => this.ToHashSet().IsProperSupersetOf(other);

		public bool IsSubsetOf(IEnumerable<T> other) => this.ToHashSet().IsSubsetOf(other);

		public bool IsSupersetOf(IEnumerable<T> other) => this.ToHashSet().IsSupersetOf(other);

		public bool Overlaps(IEnumerable<T> other) => this.ToHashSet().Overlaps(other);

		public bool SetEquals(IEnumerable<T> other) => this.ToHashSet().SetEquals(other);
	}
}
