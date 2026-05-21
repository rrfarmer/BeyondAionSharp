using System.Net;
using Aion.GameServer.Configuration;

namespace Aion.GameServer.Tests;

public class GameServerOptionsTests
{
	[Fact]
	public void LoadFromJavaConfig_ReadsCoreAndNetworkDefaults()
	{
		var options = GameServerOptions.LoadFromJavaConfig(AppContext.BaseDirectory);

		Assert.Equal(99, options.Core.ServerCountryCode);
		Assert.Equal(65, options.Core.PlayerMaxLevel);
		Assert.False(string.IsNullOrWhiteSpace(options.Core.TimeZoneId));
		Assert.False(options.Core.EnableChatServer);
		Assert.Equal(10, options.Core.CharacterReentryTimeSeconds);
		Assert.Equal("./data/handlers/quest", options.Core.QuestHandlerDirectory);
		Assert.Equal(10, options.Membership.CharacterAdditionalEnable);
		Assert.Equal(8, options.Membership.CharacterAdditionalCount);
		Assert.Equal(1, options.Administration.UnrestrictedItemTradeAccessLevel);
		Assert.Contains(10000001, options.Administration.OperationalItemIds);
		Assert.Contains(10000002, options.Administration.OperationalItemIds);

		Assert.Equal(new IPEndPoint(IPAddress.Any, 7777), options.Network.ClientEndPoint);
		Assert.Equal(new IPEndPoint(IPAddress.Any, 7777), options.Network.ClientConnectEndPoint);
		Assert.Equal(9014, options.Network.LoginEndPoint.Port);
		Assert.Equal(9021, options.Network.ChatEndPoint.Port);
		Assert.Equal(1, options.Network.GameServerId);
		Assert.Equal("1234", options.Network.LoginPassword);
		Assert.Equal(1, options.Network.NioReadWriteThreads);
		Assert.False(options.Network.NioReadWriteThreadsUnsafeAllow);
		Assert.Equal(4, options.Network.PacketProcessorMinThreads);
		Assert.Equal(4, options.Network.PacketProcessorMaxThreads);
	}

	[Fact]
	public void LoadFromJavaConfig_ReadsGeoCustomCleaningAndThreadDefaults()
	{
		var options = GameServerOptions.LoadFromJavaConfig(AppContext.BaseDirectory);

		Assert.True(options.GeoData.Enabled);
		Assert.True(options.GeoData.CanSeeEnabled);
		Assert.True(options.GeoData.MaterialsEnabled);
		Assert.False(options.GeoData.MaterialsShowDetails);

		Assert.True(options.Custom.ChallengeTasksEnabled);
		Assert.True(options.Custom.EnableEnchantAnnounce);
		Assert.False(options.Custom.SpeakingBetweenFactions);
		Assert.Equal(8, options.Custom.BrokerRegistrationExpirationDays);
		Assert.Equal(2, options.Custom.VortexDuration);
		Assert.Contains(210020000, options.Custom.ConquerorAndProtectorWorlds);
		Assert.Equal(8, options.Custom.ConquerorAndProtectorWorlds.Count);
		Assert.Equal(2f, options.Custom.PvpMapApMultiplier, 0.001f);
		Assert.Equal(1f, options.Custom.PvpMapPveApMultiplier, 0.001f);
		Assert.False(options.Custom.CountSummonEffectsForCumulativeResist);

		Assert.False(options.Cleaning.Enabled);
		Assert.Equal(365, options.Cleaning.MinimumAccountInactivityDays);
		Assert.Equal(25, options.Cleaning.MaxDeletableCharacterLevel);

		Assert.Equal(0, options.Threads.BaseThreadPoolSize);
		Assert.Equal(0, options.Threads.ScheduledThreadPoolSize);
		Assert.Equal(5000, options.Threads.MaximumRuntimeWithoutWarningMillis);
		Assert.False(options.Threads.UsePriorities);
		Assert.True(options.LoadedPropertyCount > 0);
	}

	[Fact]
	public void LoadDatabaseOptionsFromJavaConfig_ReadsGameDatabaseDefaults()
	{
		var options = GameServerOptions.LoadDatabaseOptionsFromJavaConfig(AppContext.BaseDirectory);

		Assert.Equal("localhost", options.Server);
		Assert.Equal(3306, options.Port);
		Assert.Equal("aion_gs", options.Database);
		Assert.Equal("root", options.UserId);
		Assert.Equal(5, options.MaxPoolSize);
		Assert.Equal(5000, options.ConnectionTimeout);
		Assert.False(string.IsNullOrEmpty(options.Password));
	}

	[Fact]
	public void LoadFromJavaConfig_AppliesMyGsOverridesLast()
	{
		var root = Path.Combine(Path.GetTempPath(), $"AionGameServerConfig_{Guid.NewGuid()}");
		try
		{
			var configRoot = Path.Combine(root, "game-server", "config");
			Directory.CreateDirectory(Path.Combine(configRoot, "administration"));
			Directory.CreateDirectory(Path.Combine(configRoot, "main"));
			Directory.CreateDirectory(Path.Combine(configRoot, "network"));
			File.WriteAllText(Path.Combine(configRoot, "network", "network.properties"), "gameserver.network.login.gsid = 1");
			File.WriteAllText(
				Path.Combine(configRoot, "mygs.properties"),
				"""
				gameserver.network.login.gsid = 42
				gameserver.network.client.socket_address = 127.0.0.1:8888
				gameserver.network.client.connect_address = ${gameserver.network.client.socket_address}
				"""
			);

			var options = GameServerOptions.LoadFromJavaConfig(root);

			Assert.Equal(42, options.Network.GameServerId);
			Assert.Equal(new IPEndPoint(IPAddress.Loopback, 8888), options.Network.ClientEndPoint);
			Assert.Equal(new IPEndPoint(IPAddress.Loopback, 8888), options.Network.ClientConnectEndPoint);
		}
		finally
		{
			if (Directory.Exists(root))
				Directory.Delete(root, recursive: true);
		}
	}
}
