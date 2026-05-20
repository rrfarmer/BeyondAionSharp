namespace Aion.ChatServer.Models.Channels;

public sealed class JobChannel : RaceChannel
{
	private static readonly List<IReadOnlySet<string>> AliasSets =
	[
		NewOrderedSet("Gladiator", "Gladiador", "Gladiatore", "Gladiateur", "Gladyatör", "Гладиатор", "剑星", "검성"),
		NewOrderedSet("Templar", "Templer", "Templario", "Templare", "Templier", "Templariusz", "Tapınakçı", "Страж", "守护星", "수호성"),
		NewOrderedSet("Assassin", "Assassine", "Asesino", "Assassino", "Asasyn", "Suikastçı", "Убийца", "杀星", "살성"),
		NewOrderedSet("Ranger", "Jäger", "Cazador", "Cacciatore", "Rôdeur", "Łowca", "Avcı", "Стрелок", "弓星", "궁성"),
		NewOrderedSet("Sorcerer", "Zauberer", "Hechicero", "Fattucchiere", "Sorcier", "Czarodziej", "Sihirbaz", "Волшебник", "魔道星", "마도성"),
		NewOrderedSet("Spiritmaster", "Beschwörer", "Invocador", "Incantatore", "Spiritualiste", "Zaklinacz", "Ruh Çağırıcı", "Заклинатель", "精灵星", "정령성"),
		NewOrderedSet("Cleric", "Kleriker", "Clérigo", "Chierico", "Clerc", "Kleryk", "Ruhban", "Целитель", "治愈星", "치유성"),
		NewOrderedSet("Chanter", "Kantor", "Cantor", "Cantore", "Aède", "Чародей", "护法星", "호법성"),
		NewOrderedSet("Aethertech", "Äthertech", "Técnico del éter", "Tecnico dell'etere", "Éthertech", "EterTech", "Etertek", "Пилот", "机甲星", "기갑성"),
		NewOrderedSet("Gunslinger", "Gunner", "Schütze", "Tirador", "Tiratore", "Pistolero", "Strzelec", "Nişancı", "Снайпер", "枪炮星", "사격성"),
		NewOrderedSet("Songweaver", "Bard", "Barde", "Bardo", "Ozan", "Бард", "吟游星", "음유성")
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
