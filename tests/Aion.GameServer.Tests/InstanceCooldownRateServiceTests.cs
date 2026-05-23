using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class InstanceCooldownRateServiceTests
{
	[Fact]
	public void GetInstanceRate_MatchesJavaMembershipAndExcludedMapGate()
	{
		var options = new GameServerOptions
		{
			Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
			Instance = new GameServerInstanceOptions
			{
				CooldownRate = 3,
				CooldownRateExcludedMaps = new HashSet<int> { 300030000 },
			},
		};
		var regular = new Player { AccountMembership = 9 };
		var premium = new Player { AccountMembership = 10 };

		Assert.Equal(1, InstanceCooldownRateService.GetInstanceRate(regular, 300040000, options));
		Assert.Equal(1, InstanceCooldownRateService.GetInstanceRate(premium, 300030000, options));
		Assert.Equal(3, InstanceCooldownRateService.GetInstanceRate(premium, 300040000, options));
	}

	[Fact]
	public void GetInstanceRate_UsesConfiguredRateEvenWhenRateIsZeroLikeJava()
	{
		var options = new GameServerOptions
		{
			Membership = new GameServerMembershipOptions { InstancesCooldown = 1 },
			Instance = new GameServerInstanceOptions { CooldownRate = 0 },
		};
		var player = new Player { AccountMembership = 1 };

		Assert.Equal(0, InstanceCooldownRateService.GetInstanceRate(player, 300040000, options));
	}
}
