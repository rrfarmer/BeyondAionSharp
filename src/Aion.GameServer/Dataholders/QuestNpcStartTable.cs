namespace Aion.GameServer.Dataholders;

public sealed class QuestNpcStartTable
{
	private readonly Dictionary<int, QuestNpcStartRegistration> _registrations = new();
	private readonly List<QuestNpcStartRegistrationSource> _sources = new();

	public IReadOnlyList<QuestNpcStartRegistrationSource> Sources => _sources;

	public QuestNpcStartRegistration RegisterQuestNpc(int npcId, int questRange = QuestNpcStartRegistration.DefaultQuestRange)
	{
		// Java parity: questEngine/QuestEngine.registerQuestNpc creates or reuses a QuestNpc.
		if (!_registrations.TryGetValue(npcId, out var registration))
		{
			registration = new QuestNpcStartRegistration(npcId, questRange);
			_registrations.Add(npcId, registration);
		}

		return registration;
	}

	public bool RegisterOnQuestStart(QuestNpcStartRegistrationSource source)
	{
		// Java parity: QuestEngine handler registration eventually calls QuestNpc.addOnQuestStart.
		_sources.Add(source);
		return RegisterQuestNpc(source.NpcId, source.QuestRange).AddOnQuestStart(source.QuestId);
	}

	public QuestNpcStartRegistration GetQuestNpc(int npcId)
	{
		// Java parity: questEngine/QuestEngine.getQuestNpc returns an unregistered empty QuestNpc.
		return _registrations.TryGetValue(npcId, out var registration)
			? registration
			: new QuestNpcStartRegistration(npcId, QuestNpcStartRegistration.DefaultQuestRange);
	}

	public IReadOnlyDictionary<int, QuestNpcStartRegistration> Registrations => _registrations;
}

public sealed class QuestNpcStartRegistration
{
	public const int DefaultQuestRange = 20;

	private readonly HashSet<int> _onQuestStart = new();

	public QuestNpcStartRegistration(int npcId, int questRange)
	{
		NpcId = npcId;
		QuestRange = questRange;
	}

	public int NpcId { get; }

	public int QuestRange { get; }

	public IReadOnlySet<int> OnQuestStart => _onQuestStart;

	public bool AddOnQuestStart(int questId)
	{
		// Java parity: model/templates/quest/QuestNpc.addOnQuestStart stores each quest id once.
		return _onQuestStart.Add(questId);
	}
}

public sealed record QuestNpcStartRegistrationSource(
	int NpcId,
	int QuestId,
	QuestNpcStartRegistrationSourceKind SourceKind,
	string SourcePath,
	int QuestRange = QuestNpcStartRegistration.DefaultQuestRange);

public enum QuestNpcStartRegistrationSourceKind
{
	JavaHandler = 0,
	XmlQuest = 1,
	Manual = 2,
}
