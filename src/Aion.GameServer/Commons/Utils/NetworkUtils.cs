using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Aion.Commons.Nio;

namespace Aion.GameServer.Commons.Utils;

/// <summary>Java parity: commons/utils/NetworkUtils (KID, -Nemesiss-). java.net InetAddress→System.Net.IPAddress; String.format %0NX→ToString("XN").</summary>
public class NetworkUtils
{
    /// <summary>The first matching non-loopback IPv4 address on this machine (network reachable).</summary>
    public static IPAddress FindLocalIPv4()
    {
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                {
                    IPAddress addr = ua.Address;
                    if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr) && !IsMulticast(addr))
                        return addr;
                }
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsMulticast(IPAddress addr)
    {
        byte first = addr.GetAddressBytes()[0];
        return first >= 224 && first <= 239;
    }

    /// <summary>
    /// Check if IP address matches pattern (*.*.*.*, 192.168.1.0-255, *).
    /// Java parity preserved including Byte.parseByte (signed, range -128..127) → sbyte.Parse: octets &gt;127 throw, matching Java's behavior.
    /// </summary>
    public static bool CheckIPMatching(string pattern, string address)
    {
        if (pattern.Equals("*.*.*.*") || pattern.Equals("*"))
            return true;

        string[] mask = pattern.Split('.');
        string[] ip_address = address.Split('.');
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i].Equals("*") || mask[i].Equals(ip_address[i]))
                continue;
            else if (mask[i].Contains("-"))
            {
                sbyte min = sbyte.Parse(mask[i].Split('-')[0]);
                sbyte max = sbyte.Parse(mask[i].Split('-')[1]);
                sbyte ip = sbyte.Parse(ip_address[i]);
                if (ip < min || ip > max)
                    return false;
            }
            else
                return false;
        }
        return true;
    }

    /// <summary>The IP as a human-readable string (i.e. 127.0.0.1).</summary>
    public static string IntToIpString(int ip)
    {
        return (ip & 0xFF) + "." + ((ip >> 8) & 0xFF) + "." + ((ip >> 16) & 0xFF) + "." + ((ip >> 24) & 0xFF);
    }

    /// <summary>Formatted hex string of the buffer's data.</summary>
    public static string ToHex(ByteBuffer buffer)
    {
        return ToHex(buffer, 0, Math.Min(buffer.Limit(), buffer.Capacity()));
    }

    /// <summary>Formatted hex string of the buffer's data from start (inclusive) to end (exclusive).</summary>
    public static string ToHex(ByteBuffer buffer, int start, int end)
    {
        StringBuilder result = new StringBuilder();
        for (int i = start, bytes = 0; i < end; bytes++)
        {
            if (bytes % 16 == 0)
            {
                if (result.Length > 0)
                    result.Append("\n");
                result.Append(bytes.ToString("X4") + ": ");
            }

            int b = buffer.Get(i) & 0xff;
            result.Append(b.ToString("X2") + " ");

            int bytesInRow = (bytes % 16) + 1;
            if (++i == buffer.Capacity() || bytesInRow == 16)
            {
                for (int j = bytesInRow; j <= 16; j++)
                    result.Append("   ");
                ToText(buffer, result, i - bytesInRow, i);
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Writes bytes from startIndex (inclusive) to endIndex (exclusive) as printable chars (0x1F&lt;c&lt;0x80) or '.' otherwise.
    /// </summary>
    private static void ToText(ByteBuffer buffer, StringBuilder result, int startIndex, int endIndex)
    {
        for (int charPos = startIndex; charPos < endIndex; charPos++)
        {
            int c = buffer.Get(charPos) & 0xFF; // unsigned byte
            if (c > 0x1f && c < 0x80)
                result.Append((char)c);
            else
                result.Append('.');
        }
    }
}
