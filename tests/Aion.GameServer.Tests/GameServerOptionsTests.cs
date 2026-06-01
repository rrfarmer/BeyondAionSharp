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
		Assert.Equal(10, options.Membership.StigmaSlotQuest);
		Assert.Equal(10, options.Membership.StigmaAutoLearn);
		Assert.Equal(10, options.Membership.InstancesCooldown);
		Assert.Equal(1, options.Instance.CooldownRate);
		Assert.Empty(options.Instance.CooldownRateExcludedMaps);
		Assert.False(options.Instance.FormInstanceGroupAnywhere);
		Assert.Equal(1, options.Administration.UnrestrictedItemTradeAccessLevel);
		Assert.Equal(2, options.Administration.GmPanelAccessLevel);
		Assert.Equal(1, options.Administration.FreeFlightAccessLevel);
		Assert.Contains(10000001, options.Administration.OperationalItemIds);
		Assert.Contains(10000002, options.Administration.OperationalItemIds);
		Assert.Equal([75f, 75f], options.Rates.ManastoneChances);
		Assert.Equal([65f, 65f], options.Rates.EnchantmentStoneBaseChances);
		Assert.Equal([50f, 50f], options.Rates.EnchantmentStoneAmplifiedChances);
		Assert.Equal([1f, 2f], options.Rates.ApPvpGainRates);
		Assert.Equal([1f, 1f], options.Rates.ApPvpLossRates);
		Assert.Equal([1f, 2f], options.Rates.ApPveRates);
		Assert.Equal([1f, 2f], options.Rates.ApQuestRates);
		Assert.Equal([1f, 2f], options.Rates.ApDredgionRates);
		Assert.Equal([1f, 2f], options.Rates.GpRates);
		Assert.Equal([1f, 2f], options.Rates.XpQuestRates);
		Assert.Equal([1f, 2f], options.Rates.QuestKinahRates);
		Assert.Equal([1f, 2f], options.Rates.DropRates);
		Assert.Equal(100, options.Prices.DefaultPrices);
		Assert.Equal(100, options.Prices.DefaultModifier);
		Assert.Equal(100, options.Prices.DefaultTaxes);
		Assert.Equal(100, options.Prices.VendorBuyModifier);
		Assert.Equal(20, options.Prices.VendorSellModifier);

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

		Assert.True(options.Ai.NpcMovementEnabled);
		Assert.Equal(3, options.Ai.NpcMovementMinimumDelaySeconds);
		Assert.Equal(15, options.Ai.NpcMovementMaximumDelaySeconds);
		Assert.False(options.Ai.NpcShoutsEnabled);
		Assert.Equal("./data/handlers/ai", options.Ai.HandlerDirectory);

		Assert.True(options.Housing.AuctionsEnabled);
		Assert.True(options.Housing.PayEnabled);
		Assert.Equal(200f, options.Housing.VisibilityDistance, 0.001f);
		Assert.Equal("0 0 12 ? * SUN", options.Housing.AuctionEndTime);
		Assert.Equal([1, 5], options.Housing.AuctionRegisterDays);
		Assert.Equal("0 0 0 ? * MON", options.Housing.MaintenanceTime);
		Assert.Equal(0.3f, options.Housing.AuctionRegistrationFeePercent, 0.001f);
		Assert.Equal(0.1f, options.Housing.AuctionSalesCommissionPercent, 0.001f);
		Assert.Equal(100f, options.Housing.AuctionBidStepLimit, 0.001f);
		Assert.Equal(0, options.Housing.HouseMinBidLevel);
		Assert.Equal(0, options.Housing.MansionMinBidLevel);
		Assert.Equal(0, options.Housing.EstateMinBidLevel);
		Assert.Equal(0, options.Housing.PalaceMinBidLevel);

		Assert.True(options.Custom.ChallengeTasksEnabled);
		Assert.True(options.Custom.EnableEnchantAnnounce);
		Assert.False(options.Custom.SpeakingBetweenFactions);
		Assert.Equal(8, options.Custom.BrokerRegistrationExpirationDays);
		Assert.Equal(2, options.Custom.VortexDuration);
		Assert.Contains(210020000, options.Custom.ConquerorAndProtectorWorlds);
		Assert.Equal(8, options.Custom.ConquerorAndProtectorWorlds.Count);
		Assert.Equal(14, options.Custom.TopRankingXformMinRank);
		Assert.Equal(2f, options.Custom.PvpMapApMultiplier, 0.001f);
		Assert.Equal(1f, options.Custom.PvpMapPveApMultiplier, 0.001f);
		Assert.Empty(options.Custom.DisabledEventNames);
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
				gameserver.topranking.xform.min_rank = COMMANDER
				gameserver.event.service.disabled_events = Broken Hearts, Ice Festival
				gameserver.rates.ap.pvp.gain = 1.25, 2.25, 3.25
				gameserver.rates.ap.pvp.loss = 0.75, 1.25
				gameserver.rates.ap.pve = 1.5, 2.5
				gameserver.rates.ap.quest = 2.0, 4.0
				gameserver.rates.ap.dredgion = 3.0, 6.0
				gameserver.rates.gp.gain = 1.25, 2.25
				gameserver.rates.xp.quest = 1.75, 2.75
				gameserver.rates.kinah.quest = 1.5, 2.5, 3.5
				gameserver.rates.drop = 0.5, 1.5, 2.5
				gameserver.prices.default.prices = 110
				gameserver.prices.default.modifier = 95
				gameserver.prices.default.taxes = 105
				gameserver.prices.vendor.buymod = 125
				gameserver.prices.vendor.sellmod = 22
				gameserver.instance_group.form_anywhere = true
				gameserver.timezone = UTC
				"""
			);

			var options = GameServerOptions.LoadFromJavaConfig(root);

			Assert.Equal(42, options.Network.GameServerId);
			Assert.Equal(new IPEndPoint(IPAddress.Loopback, 8888), options.Network.ClientEndPoint);
			Assert.Equal(new IPEndPoint(IPAddress.Loopback, 8888), options.Network.ClientConnectEndPoint);
			Assert.Equal(17, options.Custom.TopRankingXformMinRank);
			Assert.Equal(2, options.Custom.DisabledEventNames.Count);
			Assert.Contains("Broken Hearts", options.Custom.DisabledEventNames);
			Assert.Contains("ice festival", options.Custom.DisabledEventNames);
			Assert.Equal([1.25f, 2.25f, 3.25f], options.Rates.ApPvpGainRates);
			Assert.Equal([0.75f, 1.25f], options.Rates.ApPvpLossRates);
			Assert.Equal([1.5f, 2.5f], options.Rates.ApPveRates);
			Assert.Equal([2f, 4f], options.Rates.ApQuestRates);
			Assert.Equal([3f, 6f], options.Rates.ApDredgionRates);
			Assert.Equal([1.25f, 2.25f], options.Rates.GpRates);
			Assert.Equal([1.75f, 2.75f], options.Rates.XpQuestRates);
			Assert.Equal([1.5f, 2.5f, 3.5f], options.Rates.QuestKinahRates);
			Assert.Equal([0.5f, 1.5f, 2.5f], options.Rates.DropRates);
			Assert.Equal(110, options.Prices.DefaultPrices);
			Assert.Equal(95, options.Prices.DefaultModifier);
			Assert.Equal(105, options.Prices.DefaultTaxes);
			Assert.Equal(125, options.Prices.VendorBuyModifier);
			Assert.Equal(22, options.Prices.VendorSellModifier);
			Assert.True(options.Instance.FormInstanceGroupAnywhere);
			Assert.Equal("UTC", options.Core.TimeZoneId);
			Assert.Equal(TimeZoneInfo.Utc, options.Core.GetTimeZone());
		}
		finally
		{
			if (Directory.Exists(root))
				Directory.Delete(root, recursive: true);
		}
	}
}
