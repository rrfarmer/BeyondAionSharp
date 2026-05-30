using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

public sealed class ChatUtilTests
{
	// --- Existing L10n ---

	[Fact]
	public void L10n_ProducesExpectedEncoding()
	{
		// Java: l10nId << 1 | 1 = l10nId * 2 + 1
		var result = ChatUtil.L10n(1300050);
		Assert.StartsWith("$", result);
	}

	[Fact]
	public void L10n_ZeroReturnsNull()
	{
		// Java: if (l10nId == 0) return null
		Assert.Null(ChatUtil.L10n(0));
	}

	// --- Name ---

	[Fact]
	public void Name_ProducesClickableCharNameLink()
	{
		var result = ChatUtil.Name("Daeva");

		Assert.Equal("[charname:Daeva;1 1 1]", result);
	}

	// --- Genderize ---

	[Fact]
	public void Genderize_ProducesGenderTag()
	{
		var result = ChatUtil.Genderize("Soldier", "Soldier");

		Assert.Equal("Soldier[f:\"Soldier\"]", result);
	}

	// --- Color from RGB int ---

	[Fact]
	public void Color_FromPackedRgb_ExtractsComponents()
	{
		// 0xFF0000 = red; (255,0,0) → r=1.0, g=0, b=0
		var result = ChatUtil.Color("Hello", 0xFF0000);

		Assert.Contains("[color:Hello;", result);
		Assert.Contains("]", result);
	}

	// --- Color from R,G,B ---

	[Fact]
	public void Color_WhiteProducesOnesForAllComponents()
	{
		// r=255, g=255, b=255 → all = 1.0 → "1." format
		var result = ChatUtil.Color("text", 255, 255, 255);

		Assert.Contains("[color:text;", result);
	}

	[Fact]
	public void Color_ContainsMessageAndClosingBracket()
	{
		var result = ChatUtil.Color("hello", 128, 0, 0);

		Assert.StartsWith("[color:hello;", result);
		Assert.EndsWith("]", result);
	}
}
