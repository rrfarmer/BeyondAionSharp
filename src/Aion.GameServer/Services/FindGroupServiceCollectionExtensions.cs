using Aion.GameServer.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.GameServer.Services;

public static class FindGroupServiceCollectionExtensions
{
	public static IServiceCollection AddFindGroupSingletonGraphWithAllianceOfflineTimeoutScheduler(
		this IServiceCollection services)
	{
		// Java parity: PlayerAllianceService.createAlliance starts initializeOfflineCheck once,
		// and initializeOfflineCheck schedules OfflinePlayerAllianceChecker on ThreadPoolManager.
		return services.AddFindGroupSingletonGraph(
			createGroupOfflineTimeoutStartCallback: null,
			createAllianceOfflineTimeoutStartCallback: serviceProvider => () => serviceProvider.GetRequiredService<PlayerAllianceOfflineTimeoutScheduler>().Start());
	}

	public static IServiceCollection AddFindGroupSingletonGraphWithOfflineTimeoutSchedulers(
		this IServiceCollection services)
	{
		// Java parity: PlayerGroupService.createGroup and PlayerAllianceService.createAlliance each start
		// their own offline checker once, using the same ThreadPoolManager cadence.
		return services.AddFindGroupSingletonGraph(
			serviceProvider => () => serviceProvider.GetRequiredService<PlayerGroupOfflineTimeoutScheduler>().Start(),
			serviceProvider => () => serviceProvider.GetRequiredService<PlayerAllianceOfflineTimeoutScheduler>().Start());
	}

	public static IServiceCollection AddFindGroupSingletonGraph(
		this IServiceCollection services,
		Func<IServiceProvider, Action?>? createGroupOfflineTimeoutStartCallback = null,
		Func<IServiceProvider, Action?>? createAllianceOfflineTimeoutStartCallback = null)
	{
		// Java parity: services/findgroup/FindGroupService uses SingletonHolder and is shared by
		// CM_FIND_GROUP, logout cleanup, group/alliance joined-team cleanup, and disband cleanup.
		// These registrations create the shared C# graph while live CM_FIND_GROUP dispatch remains disabled.
		services.AddSingleton<FindGroupRecruitmentPlanService>();
		services.AddSingleton<FindGroupClientActionPlanService>();
		services.AddSingleton<PlayerLeagueRuntime>();
		services.AddSingleton<PlayerGroupOfflineTimeoutDispatchService>();
		services.AddSingleton<PlayerGroupOfflineTimeoutScheduler>();
		services.AddSingleton<PlayerAllianceOfflineTimeoutDispatchService>();
		services.AddSingleton<PlayerAllianceOfflineTimeoutScheduler>();
		services.AddSingleton<FindGroupJoinedTeamLifecycleRecorder>(
			serviceProvider => new FindGroupJoinedTeamLifecycleRecorder(
				serviceProvider.GetRequiredService<FindGroupRecruitmentPlanService>(),
				() => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				GetServerIdByte(serviceProvider.GetRequiredService<GameServerOptions>())));
		services.AddSingleton<PlayerGroupRuntime>(
			serviceProvider => new PlayerGroupRuntime(
				serviceProvider.GetRequiredService<FindGroupRecruitmentPlanService>(),
				GetServerIdByte(serviceProvider.GetRequiredService<GameServerOptions>()),
				createGroupOfflineTimeoutStartCallback?.Invoke(serviceProvider)));
		services.AddSingleton<PlayerAllianceRuntime>(
			serviceProvider => new PlayerAllianceRuntime(
				serviceProvider.GetRequiredService<FindGroupRecruitmentPlanService>(),
				GetServerIdByte(serviceProvider.GetRequiredService<GameServerOptions>()),
				createAllianceOfflineTimeoutStartCallback?.Invoke(serviceProvider)));
		services.AddSingleton<PlayerGroupInviteRequestService>();
		services.AddSingleton<PlayerAllianceInviteRequestService>();
		services.AddSingleton<FindGroupInstanceApplicationInviteDispatchPlanService>();
		services.AddSingleton<FindGroupConnectionBoundaryDispatchAdapterService>();
		services.AddSingleton<FindGroupConnectionClientActionCompositionPlanService>();
		return services;
	}

	private static byte GetServerIdByte(GameServerOptions options)
	{
		return (byte)Math.Clamp(options.Network.GameServerId, byte.MinValue, byte.MaxValue);
	}
}
