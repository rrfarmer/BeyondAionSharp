using System.Runtime.CompilerServices;
using System.Globalization;
using System.Reflection;
using Aion.Commons.Nio;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Tests;

public sealed class ChatCommandExceptionParityTests
{
	[Fact]
	public void Run_FormatsMalformedIntegerLikeJavaNumberFormatException()
	{
		var command = new ThrowingCommand(() => ThrowingCommand.ParseIntValue("not-a-number"));

		Assert.True(command.Run(OfflinePlayer()));
		Assert.Equal("Invalid number: \"not-a-number\"", command.LastErrorMessage);
	}

	[Fact]
	public void Run_FormatsOverflowingIntegerLikeJavaNumberFormatException()
	{
		var command = new ThrowingCommand(() => ThrowingCommand.ParseIntValue("999999999999999999999"));

		Assert.True(command.Run(OfflinePlayer()));
		Assert.Equal("Invalid number: \"999999999999999999999\"", command.LastErrorMessage);
	}

	[Fact]
	public void Run_FormatsOverflowingLongWithOriginalToken()
	{
		const string input = "9223372036854775808";
		var command = new ThrowingCommand(() => ThrowingCommand.ParseLongValue(input));

		Assert.True(command.Run(OfflinePlayer()));
		Assert.Equal("Invalid number: \"9223372036854775808\"", command.LastErrorMessage);
	}

	[Fact]
	public void ConsoleCommand_Run_FormatsOverflowWithOriginalToken()
	{
		var command = new ThrowingConsoleCommand("2147483648");

		Assert.True(command.Run(OfflinePlayer()));
		Assert.Equal("Invalid number: \"2147483648\"", command.LastErrorMessage);
	}

	[Theory]
	[InlineData("+42", 42)]
	[InlineData("-0", 0)]
	[InlineData("2147483647", int.MaxValue)]
	[InlineData("-2147483648", int.MinValue)]
	[InlineData("１２", 12)]
	public void ParseInt_UsesJavaSignRangeAndUnicodeDigitRules(string input, int expected)
	{
		Assert.Equal(expected, ThrowingCommand.ParseIntValue(input));
	}

	[Theory]
	[InlineData(" 42")]
	[InlineData("42 ")]
	[InlineData("+")]
	[InlineData("1,000")]
	public void ParseInt_RejectsNonJavaIntegerSyntax(string input)
	{
		var exception = Assert.ThrowsAny<FormatException>(() => ThrowingCommand.ParseIntValue(input));
		Assert.Equal($"For input string: \"{input}\"", exception.Message);
	}

	[Fact]
	public void ParseFloat_IsCultureIndependentAndAcceptsJavaHexAndSuffixSyntax()
	{
		CultureInfo previousCulture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			Assert.Equal(1.5f, ThrowingCommand.ParseFloatValue("+1.5f"));
			Assert.Equal(3f, ThrowingCommand.ParseFloatValue("0x1.8p1"));
			Assert.Equal(float.PositiveInfinity, ThrowingCommand.ParseFloatValue("1e400"));
			Assert.ThrowsAny<FormatException>(() => ThrowingCommand.ParseFloatValue("1,5"));
		}
		finally
		{
			CultureInfo.CurrentCulture = previousCulture;
		}
	}

	[Theory]
	[InlineData(".5", 0.5f)]
	[InlineData("1.", 1f)]
	[InlineData("1.e1", 10f)]
	[InlineData(" 1.25F ", 1.25f)]
	public void ParseFloat_AcceptsJavaDecimalGrammar(string input, float expected)
	{
		Assert.Equal(expected, ThrowingCommand.ParseFloatValue(input));
	}

	[Theory]
	[InlineData(16, "-7F", -127)]
	[InlineData(16, "７Ｆ", 127)]
	[InlineData(8, "17", 15)]
	[InlineData(36, "z", 35)]
	public void ParseInt_WithRadixUsesJavaCharacterDigitRules(int radix, string input, int expected)
	{
		Assert.Equal(expected, ThrowingCommand.ParseIntRadixValue(input, radix));
	}

	[Theory]
	[InlineData("0x7fffffff", int.MaxValue)]
	[InlineData("-0x80000000", int.MinValue)]
	[InlineData("#10", 16)]
	[InlineData("010", 8)]
	public void DecodeInt_UsesJavaPrefixAndMinimumValueRules(string input, int expected)
	{
		Assert.Equal(expected, ThrowingCommand.DecodeIntValue(input));
	}

	[Theory]
	[InlineData("0x80000000", "For input string: \"80000000\" under radix 16")]
	[InlineData("08", "For input string: \"8\" under radix 8")]
	[InlineData("-08", "For input string: \"-8\" under radix 8")]
	[InlineData("0x-1", "Sign character in wrong position")]
	public void DecodeInt_ReportsJavaFailures(string input, string expectedMessage)
	{
		var exception = Assert.ThrowsAny<FormatException>(() => ThrowingCommand.DecodeIntValue(input));
		Assert.Equal(expectedMessage, exception.Message);
	}

	[Theory]
	[InlineData("0x7fffffffffffffff", long.MaxValue)]
	[InlineData("-0x8000000000000000", long.MinValue)]
	[InlineData("#10", 16L)]
	[InlineData("010", 8L)]
	public void DecodeLong_UsesJavaPrefixAndMinimumValueRules(string input, long expected)
	{
		Assert.Equal(expected, ThrowingCommand.DecodeLongValue(input));
	}

	[Fact]
	public void DecodeLong_RejectsPositiveMagnitudePastJavaLongMax()
	{
		var exception = Assert.ThrowsAny<FormatException>(() => ThrowingCommand.DecodeLongValue("0x8000000000000000"));
		Assert.Equal("For input string: \"8000000000000000\" under radix 16", exception.Message);
	}

	[Fact]
	public void ParseDouble_AcceptsJavaDecimalHexSuffixAndBoundarySyntax()
	{
		Assert.Equal(3d, ThrowingCommand.ParseDoubleValue("0x1.8p1"));
		Assert.Equal(double.MaxValue, ThrowingCommand.ParseDoubleValue("0x1.fffffffffffffp1023"));
		Assert.Equal(double.Epsilon, ThrowingCommand.ParseDoubleValue("0x0.0000000000001p-1022"));
		Assert.Equal(double.PositiveInfinity, ThrowingCommand.ParseDoubleValue("1e400D"));
		Assert.Equal(0x3FF0000000000000, BitConverter.DoubleToInt64Bits(ThrowingCommand.ParseDoubleValue("0x1.00000000000008p0")));
		Assert.Equal(0x3FF0000000000002, BitConverter.DoubleToInt64Bits(ThrowingCommand.ParseDoubleValue("0x1.00000000000018p0")));
		Assert.Equal(0d, ThrowingCommand.ParseDoubleValue("0x1p-1000000000"));
	}

	[Theory]
	[InlineData("NaN")]
	[InlineData("+NaN")]
	[InlineData("-NaN")]
	public void ParseDouble_NaNUsesJavaCanonicalBits(string input)
	{
		Assert.Equal(0x7FF8000000000000, BitConverter.DoubleToInt64Bits(ThrowingCommand.ParseDoubleValue(input)));
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("\t\r\n")]
	public void ParseFloatingPoint_ReportsJavaEmptyStringMessage(string input)
	{
		Assert.Equal("empty String", Assert.ThrowsAny<FormatException>(() => ThrowingCommand.ParseFloatValue(input)).Message);
		Assert.Equal("empty String", Assert.ThrowsAny<FormatException>(() => ThrowingCommand.ParseDoubleValue(input)).Message);
	}

	[Fact]
	public void ParseFloatingPoint_NullMatchesJavaNullPointerFailureCategory()
	{
		Assert.Throws<NullReferenceException>(() => ThrowingCommand.ParseFloatValue(null!));
		Assert.Throws<NullReferenceException>(() => ThrowingCommand.ParseDoubleValue(null!));
	}

	[Fact]
	public void ChatUtilIdParsing_UsesJavaAsciiDigitRegexSemantics()
	{
		Assert.Equal(1006, Aion.GameServer.Utils.ChatUtil.GetQuestId("1006१"));
		Assert.Equal(110900785, Aion.GameServer.Utils.ChatUtil.GetItemId("110900785１"));
	}

	[Theory]
	[InlineData("07", true)]
	[InlineData("012", true)]
	[InlineData("08", false)]
	[InlineData("09", false)]
	[InlineData("09L", false)]
	[InlineData("00L", false)]
	[InlineData("1L", true)]
	[InlineData("09.0", true)]
	[InlineData("+0x1", true)]
	[InlineData("0x", false)]
	[InlineData("1e1", true)]
	[InlineData("1e", false)]
	public void IsCreatableNumber_MatchesCommonsLangLeadingZeroAndSuffixRules(string input, bool expected)
	{
		Assert.Equal(expected, ThrowingCommand.IsCreatableNumberValue(input));
	}

	[Fact]
	public void ParseEnumName_RejectsDefinedNumericUnderlyingValues()
	{
		Assert.Equal(NumericEnum.Named, ThrowingCommand.ParseEnumNameValue<NumericEnum>(nameof(NumericEnum.Named)));
		Assert.False(ThrowingCommand.TryParseEnumNameValue("7", out NumericEnum parsed));
		Assert.Equal(default, parsed);
		var exception = Assert.Throws<ArgumentException>(() => ThrowingCommand.ParseEnumNameValue<NumericEnum>("7"));
		Assert.StartsWith("No enum constant ", exception.Message);
		Assert.EndsWith(".7", exception.Message);
	}

	[Theory]
	[InlineData("NaN")]
	[InlineData("+NaN")]
	[InlineData("-NaN")]
	public void ParseFloat_NaNUsesJavaCanonicalBits(string input)
	{
		Assert.Equal(0x7FC00000, BitConverter.SingleToInt32Bits(ThrowingCommand.ParseFloatValue(input)));
	}

	[Fact]
	public void ParseByte_PreservesSignedJavaByteRangeAsUnsignedBits()
	{
		Assert.Equal(byte.MaxValue, ThrowingCommand.ParseByteValue("-1"));
		Assert.Equal(127, ThrowingCommand.ParseByteValue("+127"));
		var exception = Assert.ThrowsAny<FormatException>(() => ThrowingCommand.ParseByteValue("128"));
		Assert.Equal("Value out of range. Value:\"128\" Radix:10", exception.Message);
	}

	[Fact]
	public void Run_FormatsJavaByteRangeFailureWithoutInputSuffix()
	{
		var command = new ThrowingCommand(() => ThrowingCommand.ParseByteValue("128"));

		Assert.True(command.Run(OfflinePlayer()));
		Assert.Equal("Invalid number.", command.LastErrorMessage);
	}

	[Fact]
	public void Run_PreservesDomainArgumentExceptionMessage()
	{
		var command = new ThrowingCommand(() => throw new ArgumentException("Domain validation failed."));

		Assert.True(command.Run(OfflinePlayer()));
		Assert.Equal("Domain validation failed.", command.LastErrorMessage);
	}

	[Fact]
	public void CommandHandlers_DoNotBypassJavaPrimitiveParsers()
	{
		string root = FindRepositoryRoot();
		string[] handlerDirectories =
		{
			"AdminCommands",
			"ConsoleCommands",
			"PlayerCommands"
		};
		var rawParser = new System.Text.RegularExpressions.Regex(
			@"\b(?:sbyte|byte|short|ushort|int|uint|long|ulong|float|double|decimal)\.(?:Parse|TryParse)\s*\(|\bConvert\.To(?:Byte|SByte|Int16|UInt16|Int32|UInt32|Int64|UInt64|Single|Double|Decimal)\s*\(");

		foreach (string directory in handlerDirectories)
		{
			string path = Path.Combine(root, "src", "Aion.GameServer", "Handlers", directory);
			foreach (string file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
			{
				string source = File.ReadAllText(file);
				Assert.DoesNotMatch(rawParser, source);
				Assert.DoesNotContain("\\\\d", source); // Java Pattern's default digit class is ASCII; C# Regex's is Unicode.
			}
		}

		string[] downstreamFiles =
		{
			Path.Combine(root, "src", "Aion.GameServer", "Utils", "ChatUtil.cs"),
			Path.Combine(root, "src", "Aion.GameServer", "Network", "Aion", "ServerPackets", "SM_CUSTOM_PACKET.cs")
		};
		foreach (string file in downstreamFiles)
			Assert.DoesNotMatch(rawParser, File.ReadAllText(file));
	}

	[Fact]
	public void CustomPacket_NumericElementsWriteJavaDecodedBytes()
	{
		var packet = new SM_CUSTOM_PACKET(0);
		packet.AddElement(SM_CUSTOM_PACKET.PacketElementType.D, "-0x80000000");
		packet.AddElement(SM_CUSTOM_PACKET.PacketElementType.H, "-0x8000");
		packet.AddElement(SM_CUSTOM_PACKET.PacketElementType.C, "-1");
		packet.AddElement(SM_CUSTOM_PACKET.PacketElementType.F, "0x1.8p1f");
		packet.AddElement(SM_CUSTOM_PACKET.PacketElementType.DF, "0x1.8p1D");
		packet.AddElement(SM_CUSTOM_PACKET.PacketElementType.Q, "-0x8000000000000000");
		packet.AddElement(SM_CUSTOM_PACKET.PacketElementType.B, "3");

		Assert.Equal(new byte[]
		{
			0x00, 0x00, 0x00, 0x80,
			0x00, 0x80,
			0xFF,
			0x00, 0x00, 0x40, 0x40,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x40,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80,
			0x00, 0x00, 0x00
		}, CaptureWriteImplPayload(packet));
	}

	[Theory]
	[InlineData('d', " 1")]
	[InlineData('d', "0xFFFFFFFF")]
	[InlineData('b', " 1")]
	[InlineData('q', " 1")]
	[InlineData('q', "0xFFFFFFFFFFFFFFFF")]
	public void CustomPacket_IntegerElementsRejectFormsJavaRejects(char typeCode, string input)
	{
		var packet = new SM_CUSTOM_PACKET(0);
		packet.SetBuf(ByteBuffer.Allocate(32).Order(ByteOrder.LITTLE_ENDIAN));
		SM_CUSTOM_PACKET.PacketElementType type = SM_CUSTOM_PACKET.PacketElementType.GetByCode(typeCode);
		Assert.ThrowsAny<FormatException>(() => type.Write(packet, input));
	}

	[Fact]
	public void NumericEnumCommandSites_UseNameOnlyJavaParsing()
	{
		string root = FindRepositoryRoot();
		string[] relativePaths =
		{
			"Handlers/ConsoleCommands/Attrbonus.cs",
			"Handlers/ConsoleCommands/Changeclass.cs",
			"Handlers/ConsoleCommands/Classup.cs",
			"Handlers/AdminCommands/Auction.cs",
			"Handlers/AdminCommands/BaseCommand.cs",
			"Handlers/AdminCommands/AlterNpc.cs",
			"Handlers/AdminCommands/Quest.cs",
			"Handlers/AdminCommands/Set.cs",
			"Handlers/AdminCommands/Stat.cs",
			"Handlers/AdminCommands/State.cs"
		};

		foreach (string relativePath in relativePaths)
		{
			string source = File.ReadAllText(Path.Combine(root, "src", "Aion.GameServer", relativePath.Replace('/', Path.DirectorySeparatorChar)));
			Assert.DoesNotMatch(@"\bEnum\.(?:Parse|TryParse)\s*(?:<|\()", source);
		}
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AionServer.slnx")))
			directory = directory.Parent;
		return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate AionServer.slnx.");
	}

	private static byte[] CaptureWriteImplPayload(AionServerPacket packet)
	{
		var buffer = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN);
		packet.SetBuf(buffer);
		MethodInfo writeImpl = typeof(AionServerPacket).GetMethod("WriteImpl",
			BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(AionConnection) })!;
		writeImpl.Invoke(packet, new object?[] { null });
		byte[] payload = new byte[buffer.Position()];
		buffer.Flip();
		buffer.Get(payload);
		return payload;
	}

	private static Player OfflinePlayer()
	{
		return (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
	}

	private enum NumericEnum
	{
		Zero = 0,
		Named = 7
	}

	private sealed class ThrowingCommand : ChatCommand
	{
		private readonly Action _action;

		public ThrowingCommand(Action action)
			: base("//", "throw", "")
		{
			_action = action;
		}

		public string? LastErrorMessage { get; private set; }

		public static int ParseIntValue(string value) => ParseInt(value);

		public static int ParseIntRadixValue(string value, int radix) => ParseInt(value, radix);

		public static long ParseLongValue(string value) => ParseLong(value);

		public static float ParseFloatValue(string value) => ParseFloat(value);

		public static double ParseDoubleValue(string value) => ParseDouble(value);

		public static byte ParseByteValue(string value) => ParseByte(value);

		public static int DecodeIntValue(string value) => DecodeInt(value);

		public static long DecodeLongValue(string value) => DecodeLong(value);

		public static bool IsCreatableNumberValue(string value) => IsCreatableNumber(value);

		public static TEnum ParseEnumNameValue<TEnum>(string value) where TEnum : struct, Enum => ParseEnumName<TEnum>(value);

		public static bool TryParseEnumNameValue<TEnum>(string value, out TEnum result) where TEnum : struct, Enum => TryParseEnumName(value, out result);

		public override bool ValidateAccess(Player player) => true;

		internal override bool Process(Player player, params string[] paramsArr) => Run(player, paramsArr);

		public override void Execute(Player player, params string[] paramsArr) => _action();

		protected override string ToErrorMessage(Exception e)
		{
			LastErrorMessage = base.ToErrorMessage(e);
			return LastErrorMessage;
		}
	}

	private sealed class ThrowingConsoleCommand : ConsoleCommand
	{
		private readonly string _input;

		public ThrowingConsoleCommand(string input)
			: base("throw")
		{
			_input = input;
		}

		public string? LastErrorMessage { get; private set; }

		public override void Execute(Player player, params string[] paramsArr) => ParseInt(_input);

		protected override string ToErrorMessage(Exception e)
		{
			LastErrorMessage = base.ToErrorMessage(e);
			return LastErrorMessage;
		}
	}
}
