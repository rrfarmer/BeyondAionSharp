using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests;

public sealed class PlayerSettingsTests
{
	[Fact]
	public void DeniesFriendRequests_FollowsJavaDeniedStatusMask()
	{
		var settings = new PlayerSettings
		{
			Deny = PlayerSettings.DenyFriendRequests | 2,
		};

		Assert.True(settings.DeniesFriendRequests());
		Assert.True(settings.IsInDeniedStatus(PlayerSettings.DenyFriendRequests));
		Assert.False(new PlayerSettings { Deny = 2 }.DeniesFriendRequests());
	}
}
