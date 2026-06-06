using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests;

public sealed class PlayerEnterWorldRepositoryHouseScriptRestoreTests
{
	[Fact]
	public void RestoreHouseScripts_CompressesUtf16XmlIntoMatchingPlayerScriptSlot()
	{
		var house = new PlayerHouse(5001, 700001, 353000, DateTime.UtcNow, null, IsInactive: false);
		var scriptXml = "<?xml version=\"1.0\" encoding=\"UTF-16\" ?><lboxes><lbox><id>3</id></lbox></lboxes>";

		MySqlPlayerEnterWorldRepository.RestoreHouseScripts(
			[house],
			[new MySqlPlayerEnterWorldRepository.HouseScriptRestoreRow(5001, 3, scriptXml)]);

		var script = house.Scripts.Get(3);
		Assert.NotNull(script);
		Assert.True(script.HasData);
		Assert.Equal(3, script.Id);
		Assert.True(PlayerScripts.TryDecodeXml(script.CompressedBytes, script.UncompressedSize, out var restoredXml));
		Assert.Equal(scriptXml, restoredXml);
	}

	[Fact]
	public void RestoreHouseScripts_EmptyXmlRestoresJavaEmptyScriptSlot()
	{
		var house = new PlayerHouse(5001, 700001, 353000, DateTime.UtcNow, null, IsInactive: false);

		MySqlPlayerEnterWorldRepository.RestoreHouseScripts(
			[house],
			[new MySqlPlayerEnterWorldRepository.HouseScriptRestoreRow(5001, 4, string.Empty)]);

		var script = house.Scripts.Get(4);
		Assert.NotNull(script);
		Assert.False(script.HasData);
		Assert.Equal(0, script.UncompressedSize);
		Assert.Empty(script.CompressedBytes);
	}

	[Fact]
	public void RestoreHouseScripts_IgnoresUnknownHouseAndInvalidScriptIdLikeJava()
	{
		var house = new PlayerHouse(5001, 700001, 353000, DateTime.UtcNow, null, IsInactive: false);

		MySqlPlayerEnterWorldRepository.RestoreHouseScripts(
			[house],
			[
				new MySqlPlayerEnterWorldRepository.HouseScriptRestoreRow(9999, 2, "<unknown />"),
				new MySqlPlayerEnterWorldRepository.HouseScriptRestoreRow(5001, PlayerScripts.ScriptLimit, "<invalid />"),
			]);

		for (var i = 0; i < PlayerScripts.ScriptLimit; i++)
		{
			var script = house.Scripts.Get(i);
			Assert.NotNull(script);
			Assert.False(script.HasData);
		}
	}
}
