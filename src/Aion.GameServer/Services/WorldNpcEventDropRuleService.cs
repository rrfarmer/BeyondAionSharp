using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class WorldNpcEventDropRuleService
{
	private static readonly EventDropTable EmptyEvents = new([]);
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly EventDropTable? _eventDrops;
	private readonly Func<DateTime> _now;
	private readonly IReadOnlySet<string> _disabledEventNames;

	public WorldNpcEventDropRuleService(GameServerRuntimeContext runtimeContext)
		: this(runtimeContext, null, null, null)
	{
	}

	public WorldNpcEventDropRuleService(GameServerRuntimeContext runtimeContext, GameServerOptions options)
		: this(runtimeContext, null, null, options.Custom.DisabledEventNames)
	{
	}

	public WorldNpcEventDropRuleService(
		EventDropTable eventDrops,
		Func<DateTime>? now = null,
		IEnumerable<string>? disabledEventNames = null)
		: this(null, eventDrops, now, disabledEventNames)
	{
	}

	private WorldNpcEventDropRuleService(
		GameServerRuntimeContext? runtimeContext,
		EventDropTable? eventDrops,
		Func<DateTime>? now,
		IEnumerable<string>? disabledEventNames)
	{
		_runtimeContext = runtimeContext;
		_eventDrops = eventDrops;
		_now = now ?? (() => DateTime.Now);
		_disabledEventNames = disabledEventNames?.ToHashSet(StringComparer.OrdinalIgnoreCase)
			?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	}

	public IReadOnlyList<GlobalDropRuleSummary> GetActiveEventDropRules(DateTime? now = null)
	{
		// Java parity: services/event/EventService.getActiveEventDropRules after checkActiveEvents.
		return GetEventDrops().GetActiveDropRules(now ?? _now(), _disabledEventNames);
	}

	private EventDropTable GetEventDrops()
	{
		return _eventDrops ?? _runtimeContext?.DataManager?.StaticData.EventDrops ?? EmptyEvents;
	}
}
