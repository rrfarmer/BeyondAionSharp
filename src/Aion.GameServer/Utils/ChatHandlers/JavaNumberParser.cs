using System.Globalization;
using System.Numerics;

namespace Aion.GameServer.Utils.ChatHandlers;

/// <summary>
/// Parses command-line numbers with the lexical and overflow rules used by Java's
/// <c>Integer.parseInt</c>, <c>Long.parseLong</c>, <c>Float.parseFloat</c>,
/// <c>Double.parseDouble</c>, and <c>Byte.parseByte</c> methods.
/// </summary>
internal static class JavaNumberParser
{
    internal static int ParseInt(string value)
    {
        return ParseInt(value, 10);
    }

    internal static int ParseInt(string value, int radix)
    {
        long parsed = ParseSigned(value, int.MinValue, int.MaxValue, radix);
        return (int)parsed;
    }

    internal static long ParseLong(string value)
    {
        return ParseLong(value, 10);
    }

    internal static long ParseLong(string value, int radix)
    {
        return ParseSigned(value, long.MinValue, long.MaxValue, radix);
    }

    /// <summary>
    /// Returns the unsigned C# bit representation of the signed Java byte.
    /// </summary>
    internal static byte ParseByte(string value)
    {
        return unchecked((byte)ParseSByte(value));
    }

    internal static sbyte ParseSByte(string value)
    {
        int parsed = ParseInt(value);
        if (parsed < sbyte.MinValue || parsed > sbyte.MaxValue)
            throw new JavaNumberFormatException($"Value out of range. Value:\"{value}\" Radix:10");
        return (sbyte)parsed;
    }

    internal static bool TryParseInt(string value, out int result)
    {
        try
        {
            result = ParseInt(value);
            return true;
        }
        catch (JavaNumberFormatException)
        {
            result = 0;
            return false;
        }
    }

    internal static bool TryParseLong(string value, out long result)
    {
        try
        {
            result = ParseLong(value);
            return true;
        }
        catch (JavaNumberFormatException)
        {
            result = 0;
            return false;
        }
    }

    internal static bool TryParseFloat(string value, out float result)
    {
        try
        {
            result = ParseFloat(value);
            return true;
        }
        catch (JavaNumberFormatException)
        {
            result = 0;
            return false;
        }
    }

    internal static bool TryParseSByte(string value, out sbyte result)
    {
        try
        {
            result = ParseSByte(value);
            return true;
        }
        catch (JavaNumberFormatException)
        {
            result = 0;
            return false;
        }
    }

    /// <summary>Java parity for Apache Commons Lang 3.20 <c>NumberUtils.isCreatable</c> (the target of deprecated <c>isNumber</c>).</summary>
    internal static bool IsCreatable(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        char[] chars = value.ToCharArray();
        int size = chars.Length;
        bool hasExponent = false;
        bool hasDecimalPoint = false;
        bool allowSigns = false;
        bool foundDigit = false;
        int start = chars[0] is '-' or '+' ? 1 : 0;

        if (size > start + 1 && chars[start] == '0' && !value.Contains('.'))
        {
            if (chars[start + 1] is 'x' or 'X')
            {
                int index = start + 2;
                if (index == size)
                    return false;
                for (; index < chars.Length; index++)
                    if (!IsAsciiHexDigit(chars[index]))
                        return false;
                return true;
            }
            if (char.IsDigit(chars[start + 1]))
            {
                for (int index = start + 1; index < chars.Length; index++)
                    if (chars[index] is < '0' or > '7')
                        return false;
                return true;
            }
        }

        size--; // Check a possible type qualifier separately.
        int current = start;
        while (current < size || current < size + 1 && allowSigns && !foundDigit)
        {
            if (chars[current] is >= '0' and <= '9')
            {
                foundDigit = true;
                allowSigns = false;
            }
            else if (chars[current] == '.')
            {
                if (hasDecimalPoint || hasExponent)
                    return false;
                hasDecimalPoint = true;
            }
            else if (chars[current] is 'e' or 'E')
            {
                if (hasExponent || !foundDigit)
                    return false;
                hasExponent = true;
                allowSigns = true;
            }
            else if (chars[current] is '+' or '-')
            {
                if (!allowSigns)
                    return false;
                allowSigns = false;
                foundDigit = false;
            }
            else
            {
                return false;
            }
            current++;
        }

        if (current < chars.Length)
        {
            if (chars[current] is >= '0' and <= '9')
                return true;
            if (chars[current] is 'e' or 'E')
                return false;
            if (chars[current] == '.')
                return !hasDecimalPoint && !hasExponent && foundDigit;
            if (!allowSigns && chars[current] is 'd' or 'D' or 'f' or 'F')
                return foundDigit;
            if (chars[current] is 'l' or 'L')
                return foundDigit && !hasExponent && !hasDecimalPoint;
            return false;
        }
        return !allowSigns && foundDigit;
    }

    internal static int DecodeInt(string value)
    {
        if (value is null)
            throw new NullReferenceException(); // Integer.decode(null) dereferences the input before parsing.
        if (value.Length == 0)
            throw new JavaNumberFormatException("Zero length string");

        int radix = 10;
        int index = 0;
        bool negative = false;
        if (value[0] == '-')
        {
            negative = true;
            index++;
        }
        else if (value[0] == '+')
        {
            index++;
        }

        if (value.AsSpan(index).StartsWith("0x") || value.AsSpan(index).StartsWith("0X"))
        {
            index += 2;
            radix = 16;
        }
        else if (value.AsSpan(index).StartsWith("#"))
        {
            index++;
            radix = 16;
        }
        else if (value.AsSpan(index).StartsWith("0") && value.Length > index + 1)
        {
            index++;
            radix = 8;
        }

        if (value.AsSpan(index).StartsWith("-") || value.AsSpan(index).StartsWith("+"))
            throw new JavaNumberFormatException("Sign character in wrong position");

        string magnitude = value[index..];
        try
        {
            int result = ParseInt(magnitude, radix);
            return negative ? -result : result;
        }
        catch (JavaNumberFormatException)
        {
            // Java retries with the sign attached so Integer.MIN_VALUE remains decodable.
            return ParseInt(negative ? "-" + magnitude : magnitude, radix);
        }
    }

    internal static long DecodeLong(string value)
    {
        if (value is null)
            throw new NullReferenceException(); // Long.decode(null) dereferences the input before parsing.
        if (value.Length == 0)
            throw new JavaNumberFormatException("Zero length string");

        int radix = 10;
        int index = 0;
        bool negative = false;
        if (value[0] == '-')
        {
            negative = true;
            index++;
        }
        else if (value[0] == '+')
        {
            index++;
        }

        if (value.AsSpan(index).StartsWith("0x") || value.AsSpan(index).StartsWith("0X"))
        {
            index += 2;
            radix = 16;
        }
        else if (value.AsSpan(index).StartsWith("#"))
        {
            index++;
            radix = 16;
        }
        else if (value.AsSpan(index).StartsWith("0") && value.Length > index + 1)
        {
            index++;
            radix = 8;
        }

        if (value.AsSpan(index).StartsWith("-") || value.AsSpan(index).StartsWith("+"))
            throw new JavaNumberFormatException("Sign character in wrong position");

        string magnitude = value[index..];
        try
        {
            long result = ParseLong(magnitude, radix);
            return negative ? -result : result;
        }
        catch (JavaNumberFormatException)
        {
            // Java retries with the sign attached so Long.MIN_VALUE remains decodable.
            return ParseLong(negative ? "-" + magnitude : magnitude, radix);
        }
    }

    internal static float ParseFloat(string value)
    {
        if (value is null)
            throw new NullReferenceException();

        ReadOnlySpan<char> token = TrimJavaWhitespace(value.AsSpan());
        if (token.IsEmpty)
            throw new JavaNumberFormatException("empty String");

        bool negative = false;
        int index = 0;
        if (token[index] is '+' or '-')
        {
            negative = token[index] == '-';
            if (++index == token.Length)
                throw Invalid(value);
        }

        ReadOnlySpan<char> unsignedToken = token[index..];
        if (unsignedToken.SequenceEqual("NaN"))
            // Java Float.parseFloat canonicalizes both signed and unsigned NaN to 0x7FC00000.
            // .NET's float.NaN constant uses the negative NaN bit pattern on current runtimes.
            return BitConverter.Int32BitsToSingle(0x7FC00000);
        if (unsignedToken.SequenceEqual("Infinity"))
            return negative ? float.NegativeInfinity : float.PositiveInfinity;

        int numericEnd = token.Length;
        if (token[^1] is 'f' or 'F' or 'd' or 'D')
            numericEnd--;
        if (numericEnd <= index)
            throw Invalid(value);

        ReadOnlySpan<char> numericToken = token[..numericEnd];
        if (numericEnd - index >= 2 && token[index] == '0' && token[index + 1] is 'x' or 'X')
            return ParseHexFloat(numericToken, negative, index + 2, value);

        if (!IsDecimalFloat(numericToken, index))
            throw Invalid(value);

        if (!float.TryParse(numericToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out float result))
            throw Invalid(value);
        return result;
    }

    internal static double ParseDouble(string value)
    {
        if (value is null)
            throw new NullReferenceException();

        ReadOnlySpan<char> token = TrimJavaWhitespace(value.AsSpan());
        if (token.IsEmpty)
            throw new JavaNumberFormatException("empty String");

        bool negative = false;
        int index = 0;
        if (token[index] is '+' or '-')
        {
            negative = token[index] == '-';
            if (++index == token.Length)
                throw Invalid(value);
        }

        ReadOnlySpan<char> unsignedToken = token[index..];
        if (unsignedToken.SequenceEqual("NaN"))
            return BitConverter.Int64BitsToDouble(0x7FF8000000000000);
        if (unsignedToken.SequenceEqual("Infinity"))
            return negative ? double.NegativeInfinity : double.PositiveInfinity;

        int numericEnd = token.Length;
        if (token[^1] is 'f' or 'F' or 'd' or 'D')
            numericEnd--;
        if (numericEnd <= index)
            throw Invalid(value);

        ReadOnlySpan<char> numericToken = token[..numericEnd];
        if (numericEnd - index >= 2 && token[index] == '0' && token[index + 1] is 'x' or 'X')
            return ParseHexDouble(numericToken, negative, index + 2, value);

        if (!IsDecimalFloat(numericToken, index))
            throw Invalid(value);

        if (!double.TryParse(numericToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out double result))
            throw Invalid(value);
        return result;
    }

    private static long ParseSigned(string value, long minimum, long maximum, int radix)
    {
        if (value is null)
            throw Invalid(value, radix);
        if (radix < 2)
            throw new JavaNumberFormatException($"radix {radix} less than Character.MIN_RADIX");
        if (radix > 36)
            throw new JavaNumberFormatException($"radix {radix} greater than Character.MAX_RADIX");
        if (value.Length == 0)
            throw Invalid(value, radix);

        bool negative = false;
        int index = 0;
        if (value[0] is '+' or '-')
        {
            negative = value[0] == '-';
            if (++index == value.Length)
                throw Invalid(value, radix);
        }

        // Accumulating negatively mirrors java.lang.Integer/Long and permits MIN_VALUE.
        long limit = negative ? minimum : -maximum;
        long multiplyLimit = limit / radix;
        long result = 0;
        for (; index < value.Length; index++)
        {
            int digit = Digit(value[index], radix);
            if (digit < 0 || result < multiplyLimit)
                throw Invalid(value, radix);
            result *= radix;
            if (result < limit + digit)
                throw Invalid(value, radix);
            result -= digit;
        }
        return negative ? result : -result;
    }

    private static int Digit(char value, int radix)
    {
        int digit;
        if (value is >= '0' and <= '9')
            digit = value - '0';
        else if (value is >= 'A' and <= 'Z')
            digit = value - 'A' + 10;
        else if (value is >= 'a' and <= 'z')
            digit = value - 'a' + 10;
        else if (value is >= '\uFF21' and <= '\uFF3A')
            digit = value - '\uFF21' + 10;
        else if (value is >= '\uFF41' and <= '\uFF5A')
            digit = value - '\uFF41' + 10;
        else if (CharUnicodeInfo.GetUnicodeCategory(value) == UnicodeCategory.DecimalDigitNumber)
        {
            double numericValue = char.GetNumericValue(value);
            digit = numericValue is >= 0 and <= 9 && numericValue == Math.Truncate(numericValue) ? (int)numericValue : -1;
        }
        else
        {
            digit = -1;
        }
        return digit < radix ? digit : -1;
    }

    private static bool IsDecimalFloat(ReadOnlySpan<char> value, int index)
    {
        bool hasDigits = false;
        while (index < value.Length && IsAsciiDigit(value[index]))
        {
            hasDigits = true;
            index++;
        }

        if (index < value.Length && value[index] == '.')
        {
            index++;
            while (index < value.Length && IsAsciiDigit(value[index]))
            {
                hasDigits = true;
                index++;
            }
        }

        if (!hasDigits)
            return false;

        if (index < value.Length && value[index] is 'e' or 'E')
        {
            index++;
            if (index < value.Length && value[index] is '+' or '-')
                index++;
            int exponentStart = index;
            while (index < value.Length && IsAsciiDigit(value[index]))
                index++;
            if (index == exponentStart)
                return false;
        }
        return index == value.Length;
    }

    private static float ParseHexFloat(ReadOnlySpan<char> value, bool negative, int index, string original)
    {
        BigInteger significand = BigInteger.Zero;
        bool hasDigits = false;
        bool afterPoint = false;
        long fractionalDigits = 0;

        for (; index < value.Length && value[index] is not ('p' or 'P'); index++)
        {
            char current = value[index];
            if (current == '.')
            {
                if (afterPoint)
                    throw Invalid(original);
                afterPoint = true;
                continue;
            }

            int digit = HexDigit(current);
            if (digit < 0)
                throw Invalid(original);
            hasDigits = true;
            significand = significand * 16 + digit;
            if (afterPoint)
                fractionalDigits++;
        }

        if (!hasDigits || index == value.Length)
            throw Invalid(original);
        index++; // p/P

        bool negativeExponent = false;
        if (index < value.Length && value[index] is '+' or '-')
        {
            negativeExponent = value[index] == '-';
            index++;
        }
        int exponentStart = index;
        long exponent = 0;
        while (index < value.Length && IsAsciiDigit(value[index]))
        {
            exponent = Math.Min(1_000_000_000L, exponent * 10 + value[index] - '0');
            index++;
        }
        if (index != value.Length || index == exponentStart)
            throw Invalid(original);
        if (negativeExponent)
            exponent = -exponent;

        long binaryExponent = exponent - 4 * fractionalDigits;
        return ToSingle(significand, binaryExponent, negative);
    }

    private static double ParseHexDouble(ReadOnlySpan<char> value, bool negative, int index, string original)
    {
        BigInteger significand = BigInteger.Zero;
        bool hasDigits = false;
        bool afterPoint = false;
        long fractionalDigits = 0;

        for (; index < value.Length && value[index] is not ('p' or 'P'); index++)
        {
            char current = value[index];
            if (current == '.')
            {
                if (afterPoint)
                    throw Invalid(original);
                afterPoint = true;
                continue;
            }

            int digit = HexDigit(current);
            if (digit < 0)
                throw Invalid(original);
            hasDigits = true;
            significand = significand * 16 + digit;
            if (afterPoint)
                fractionalDigits++;
        }

        if (!hasDigits || index == value.Length)
            throw Invalid(original);
        index++; // p/P

        bool negativeExponent = false;
        if (index < value.Length && value[index] is '+' or '-')
        {
            negativeExponent = value[index] == '-';
            index++;
        }
        int exponentStart = index;
        long exponent = 0;
        while (index < value.Length && IsAsciiDigit(value[index]))
        {
            exponent = Math.Min(1_000_000_000L, exponent * 10 + value[index] - '0');
            index++;
        }
        if (index != value.Length || index == exponentStart)
            throw Invalid(original);
        if (negativeExponent)
            exponent = -exponent;

        long binaryExponent = exponent - 4 * fractionalDigits;
        return ToDouble(significand, binaryExponent, negative);
    }

    private static float ToSingle(BigInteger significand, long binaryExponent, bool negative)
    {
        int sign = negative ? unchecked((int)0x80000000) : 0;
        if (significand.IsZero)
            return BitConverter.Int32BitsToSingle(sign);

        int bitLength = checked((int)significand.GetBitLength());
        long exponent = bitLength - 1L + binaryExponent;
        if (exponent > 127)
            return BitConverter.Int32BitsToSingle(sign | 0x7F800000);

        if (exponent >= -126)
        {
            BigInteger rounded = RoundToEven(significand, bitLength - 24);
            if (rounded == (BigInteger.One << 24))
            {
                rounded >>= 1;
                if (++exponent > 127)
                    return BitConverter.Int32BitsToSingle(sign | 0x7F800000);
            }

            int exponentBits = (int)(exponent + 127) << 23;
            int fractionBits = (int)(rounded - (BigInteger.One << 23));
            return BitConverter.Int32BitsToSingle(sign | exponentBits | fractionBits);
        }

        // Subnormal floats are integral multiples of 2^-149.
        long subnormalScale = binaryExponent + 149;
        BigInteger units;
        if (subnormalScale >= 0)
            units = significand << checked((int)subnormalScale);
        else if (subnormalScale < int.MinValue)
            units = BigInteger.Zero;
        else
            units = RoundToEven(significand, checked((int)-subnormalScale));

        if (units >= (BigInteger.One << 23))
            return BitConverter.Int32BitsToSingle(sign | 0x00800000); // rounded to the smallest normal value
        return BitConverter.Int32BitsToSingle(sign | (int)units);
    }

    private static double ToDouble(BigInteger significand, long binaryExponent, bool negative)
    {
        long sign = negative ? unchecked((long)0x8000000000000000) : 0;
        if (significand.IsZero)
            return BitConverter.Int64BitsToDouble(sign);

        int bitLength = checked((int)significand.GetBitLength());
        long exponent = bitLength - 1L + binaryExponent;
        if (exponent > 1023)
            return BitConverter.Int64BitsToDouble(sign | 0x7FF0000000000000);

        if (exponent >= -1022)
        {
            BigInteger rounded = RoundToEven(significand, bitLength - 53);
            if (rounded == (BigInteger.One << 53))
            {
                rounded >>= 1;
                if (++exponent > 1023)
                    return BitConverter.Int64BitsToDouble(sign | 0x7FF0000000000000);
            }

            long exponentBits = (exponent + 1023) << 52;
            long fractionBits = (long)(rounded - (BigInteger.One << 52));
            return BitConverter.Int64BitsToDouble(sign | exponentBits | fractionBits);
        }

        // Subnormal doubles are integral multiples of 2^-1074.
        long subnormalScale = binaryExponent + 1074;
        BigInteger units;
        if (subnormalScale >= 0)
            units = significand << checked((int)subnormalScale);
        else if (subnormalScale < int.MinValue)
            units = BigInteger.Zero;
        else
            units = RoundToEven(significand, checked((int)-subnormalScale));

        if (units >= (BigInteger.One << 52))
            return BitConverter.Int64BitsToDouble(sign | 0x0010000000000000); // rounded to the smallest normal value
        return BitConverter.Int64BitsToDouble(sign | (long)units);
    }

    private static BigInteger RoundToEven(BigInteger value, int rightShift)
    {
        if (rightShift <= 0)
            return value << -rightShift;

        // Avoid constructing an enormous halfway value for inputs such as 0x1p-1000000000.
        // If every significant bit is shifted away by more than the value's bit length, the
        // correctly rounded result is necessarily zero.
        if (rightShift > value.GetBitLength())
            return BigInteger.Zero;

        BigInteger rounded = value >> rightShift;
        BigInteger remainder = value - (rounded << rightShift);
        BigInteger halfway = BigInteger.One << (rightShift - 1);
        if (remainder > halfway || remainder == halfway && !rounded.IsEven)
            rounded++;
        return rounded;
    }

    private static int HexDigit(char value)
    {
        if (value is >= '0' and <= '9')
            return value - '0';
        if (value is >= 'a' and <= 'f')
            return value - 'a' + 10;
        if (value is >= 'A' and <= 'F')
            return value - 'A' + 10;
        return -1;
    }

    private static bool IsAsciiHexDigit(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static ReadOnlySpan<char> TrimJavaWhitespace(ReadOnlySpan<char> value)
    {
        int start = 0;
        while (start < value.Length && value[start] <= ' ')
            start++;
        int end = value.Length;
        while (end > start && value[end - 1] <= ' ')
            end--;
        return value[start..end];
    }

    private static JavaNumberFormatException Invalid(string? value, int radix = 10)
    {
        return new JavaNumberFormatException(value is null
            ? "Cannot parse null string"
            : $"For input string: \"{value}\"" + (radix == 10 ? "" : $" under radix {radix}"));
    }
}

internal sealed class JavaNumberFormatException : FormatException
{
    internal JavaNumberFormatException(string message)
        : base(message)
    {
    }
}
