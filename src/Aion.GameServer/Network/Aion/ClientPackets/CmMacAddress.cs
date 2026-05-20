using System.Text;
using System.Text.RegularExpressions;
using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed partial class CmMacAddress : GameClientPacket
{
	public CmMacAddress(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Unknown { get; private set; }

	public IReadOnlyList<int> RouteIps { get; private set; } = Array.Empty<int>();

	public string MacAddress { get; private set; } = string.Empty;

	public string HddSerial { get; private set; } = string.Empty;

	public int LocalIp { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_MAC_ADDRESS.readImpl.
		Unknown = buffer.ReadC();
		var routeSteps = buffer.ReadH();
		var routeIps = new List<int>(routeSteps);
		for (var i = 0; i < routeSteps; i++)
			routeIps.Add(buffer.ReadD());

		RouteIps = routeIps;
		MacAddress = buffer.ReadS();
		HddSerial = FixHddSerial(buffer.ReadS());
		LocalIp = buffer.ReadD();
	}

	private static string FixHddSerial(string hddSerial)
	{
		// Java parity: login-server receives the normalized HDD serial used by CM_MAC_ADDRESS handling.
		if (!string.IsNullOrEmpty(hddSerial) && (hddSerial.Length <= 2 || !ValidHddSerialPattern().IsMatch(hddSerial)))
			return "0x" + Convert.ToHexString(Encoding.Unicode.GetBytes(hddSerial));

		if (SwappedHddSerialPattern().IsMatch(hddSerial))
			return PairPattern().Replace(hddSerial, "$2$1").Trim();

		return hddSerial.Trim();
	}

	[GeneratedRegex("^[0-9a-zA-Z _-]+$")]
	private static partial Regex ValidHddSerialPattern();

	[GeneratedRegex("^[a-zA-Z0-9] [a-zA-Z0-9_-].*|.*[a-zA-Z0-9_-] [a-zA-Z0-9]$")]
	private static partial Regex SwappedHddSerialPattern();

	[GeneratedRegex("(.)(.)")]
	private static partial Regex PairPattern();
}
