using System.Net;

namespace Aion.LoginServer.Utils;

public static class NetworkMask
{
	public static bool Matches(string? mask, string ip)
	{
		if (string.IsNullOrWhiteSpace(mask))
			return true;
		if (mask == "*")
			return true;
		if (string.Equals(mask, ip, StringComparison.OrdinalIgnoreCase))
			return true;

		var regex = "^" + System.Text.RegularExpressions.Regex.Escape(mask)
			.Replace("\\*", "[0-9]{1,3}")
			.Replace("\\?", "[0-9]") + "$";
		if (System.Text.RegularExpressions.Regex.IsMatch(ip, regex))
			return true;

		return TryCidrMatch(mask, ip);
	}

	private static bool TryCidrMatch(string mask, string ip)
	{
		var slash = mask.IndexOf('/');
		if (slash <= 0)
			return false;

		if (!IPAddress.TryParse(mask[..slash], out var network) || !IPAddress.TryParse(ip, out var address))
			return false;
		if (!int.TryParse(mask[(slash + 1)..], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
			return false;

		var networkBytes = network.GetAddressBytes();
		var addressBytes = address.GetAddressBytes();
		if (networkBytes.Length != 4 || addressBytes.Length != 4)
			return false;

		var networkValue = BitConverter.ToUInt32(networkBytes.Reverse().ToArray());
		var addressValue = BitConverter.ToUInt32(addressBytes.Reverse().ToArray());
		var prefixMask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
		return (networkValue & prefixMask) == (addressValue & prefixMask);
	}
}
