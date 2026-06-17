using System;
using System.Globalization;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/NumberTransformer (Neon). Parses byte/short/int/long via Java's *.decode semantics
/// (optional sign, 0x/#/0 radix prefixes) and float/double via valueOf (invariant culture).
/// </summary>
public sealed class NumberTransformer : PropertyTransformer
{
    public override bool Matches(Type targetType)
    {
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(int)
            || t == typeof(long) || t == typeof(float) || t == typeof(double);
    }

    protected override object? ParseObject(string value, Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(long))
            return checked((long)DecodeLong(value));
        if (t == typeof(int))
            return checked((int)DecodeLong(value));
        if (t == typeof(short))
            return checked((short)DecodeLong(value));
        if (t == typeof(byte))
            return checked((byte)DecodeLong(value));
        if (t == typeof(sbyte))
            return checked((sbyte)DecodeLong(value));
        if (t == typeof(double))
            return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (t == typeof(float))
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        throw new NotSupportedException("Number of type " + t.Name + " is not supported.");
    }

    /// <summary>
    /// Java parity: Integer/Long.decode — leading sign, then 0x/0X/# = hex, leading 0 = octal, else decimal.
    /// </summary>
    private static long DecodeLong(string nm)
    {
        int index = 0;
        bool negative = false;
        if (nm.Length == 0)
            throw new FormatException("Zero length string");

        char firstChar = nm[0];
        if (firstChar == '-')
        {
            negative = true;
            index++;
        }
        else if (firstChar == '+')
        {
            index++;
        }

        int radix;
        if (nm.Length > index + 1 && nm[index] == '0' && (nm[index + 1] == 'x' || nm[index + 1] == 'X'))
        {
            index += 2;
            radix = 16;
        }
        else if (nm.Length > index && nm[index] == '#')
        {
            index++;
            radix = 16;
        }
        else if (nm.Length > index + 1 && nm[index] == '0')
        {
            index++;
            radix = 8;
        }
        else
        {
            radix = 10;
        }

        if (index >= nm.Length)
            throw new FormatException("For input string: \"" + nm + "\"");

        var digits = nm.Substring(index);
        long result = Convert.ToInt64(digits, radix);
        return negative ? -result : result;
    }
}
