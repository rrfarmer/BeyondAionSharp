using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
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

	// --- CharName ---

	[Fact]
	public void CharName_ProducesClickableCharNameLink()
	{
		// Java parity: ChatUtil.charName(Player) formats player.getName(true). An access level 0 account never gets a
		// name tag, but Player.GetName(true) still reads AdminConfig.NAME_TAGS, so only default it when nothing loaded it.
		AdminConfig.NAME_TAGS ??= [];
		var common = new PlayerCommonData(1);
		common.SetName("Daeva");
		var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
		typeof(Player).GetField("playerAccountData", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(player, new PlayerAccountData(common, new PlayerAppearance()));
		typeof(Player).GetField("playerAccount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, new Account(2));

		var result = ChatUtil.CharName(player);

		Assert.Equal("[charname:Daeva;1 1 1]", result);
	}

	// --- Split (Java parity: ChatUtil.split, upstream 7ce6c1b8a) ---

	[Fact]
	public void Split_ReturnsShortMessageUnchanged()
	{
		string message = new string('a', 511);

		Assert.Equal(new[] { message }, ChatUtil.Split(message));
	}

	[Fact]
	public void Split_BreaksPlainTextAtLastSpaceBeforeDisplayLimit()
	{
		// 150 x "abcdefghi " = 1500 chars. The estimate reaches 1022 at index 1021; the last space before it is at 1019.
		string message = string.Concat(Enumerable.Repeat("abcdefghi ", 150));

		var parts = ChatUtil.Split(message);

		Assert.Equal(2, parts.Count);
		Assert.Equal(message[..1019], parts[0]);
		// upstream 92da2dd62 prefixes every part after the first with the zero-width continuation mark
		Assert.Equal("\u200B" + message[1020..], parts[1]);
	}

	[Fact]
	public void Split_CountsChatLinksWithTheirRenderedLength()
	{
		// Each "[item:182400001] " adds 31 (link) + 1 (space) = 32. The 32nd link pushes the estimate to 1023,
		// so the first part ends at the space after the 31st link (index 31 * 17 - 1 = 526).
		string message = string.Concat(Enumerable.Repeat("[item:182400001] ", 40));

		var parts = ChatUtil.Split(message);

		Assert.Equal(2, parts.Count);
		Assert.Equal(message[..526], parts[0]);
		Assert.Equal("\u200B" + message[527..], parts[1]);
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
		// Java 25: DecimalFormat(".##") formats 1.0 as "1.0", not "1." or "1"
		var result = ChatUtil.Color("text", 255, 255, 255);

		Assert.Equal("[color:text;1.0 1.0 1.0]", result);
	}

	[Fact]
	public void Color_FormatsComponentsLikeJavaDecimalFormat()
	{
		// Java 25: ChatUtil.color("text", 0, 128, 175) — no leading zero, one or two fraction digits
		var result = ChatUtil.Color("text", 0, 128, 175);

		Assert.Equal("[color:text;.0 .5 .69]", result);
	}

	[Fact]
	public void Color_ContainsMessageAndClosingBracket()
	{
		var result = ChatUtil.Color("hello", 128, 0, 0);

		Assert.StartsWith("[color:hello;", result);
		Assert.EndsWith("]", result);
	}

	// --- java.awt.Color constants (AwtColor) ---

	[Fact]
	public void Color_AwtGreenMatchesJavaEncoding()
	{
		// Java 25: ChatUtil.color("active", java.awt.Color.GREEN). System.Drawing.Color.Green is (0,128,0) and gives ".0 .5 .0".
		Assert.Equal("[color:active;.0 1.0 .0]", ChatUtil.Color("active", AwtColor.GREEN));
	}

	[Fact]
	public void Color_AwtPinkMatchesJavaEncoding()
	{
		// Java 25: ChatUtil.color("ATTENTION:", java.awt.Color.PINK). System.Drawing.Color.Pink is (255,192,203) and gives "1.0 .75 .8".
		Assert.Equal("[color:ATTENTION:;1.0 .69 .69]", ChatUtil.Color("ATTENTION:", AwtColor.PINK));
	}

	[Theory]
	// Java 25: ((Color) Color.class.getField(name).get(null)).getRGB() for every constant
	[InlineData("WHITE", -1)]
	[InlineData("LIGHT_GRAY", -4144960)]
	[InlineData("GRAY", -8355712)]
	[InlineData("DARK_GRAY", -12566464)]
	[InlineData("BLACK", -16777216)]
	[InlineData("RED", -65536)]
	[InlineData("PINK", -20561)]
	[InlineData("ORANGE", -14336)]
	[InlineData("YELLOW", -256)]
	[InlineData("GREEN", -16711936)]
	[InlineData("MAGENTA", -65281)]
	[InlineData("CYAN", -16711681)]
	[InlineData("BLUE", -16776961)]
	public void AwtColor_NameLookupMatchesJavaGetRgb(string fieldName, int javaRgb)
	{
		Assert.True(AwtColor.TryGetByFieldName(fieldName, out var color));
		Assert.Equal(javaRgb, color.ToArgb());
	}

	[Theory]
	[InlineData("gold")]
	[InlineData("GOLD")] // a System.Drawing name, not a java.awt.Color field
	[InlineData("LIGHTGRAY")] // System.Drawing spelling; Java's field is LIGHT_GRAY
	[InlineData("OPAQUE")] // a Transparency int field: Java's cast to Color fails and it falls back to hex parsing
	public void AwtColor_NameLookupRejectsNamesThatAreNotAwtColorConstants(string fieldName)
	{
		Assert.False(AwtColor.TryGetByFieldName(fieldName, out _));
	}
}
