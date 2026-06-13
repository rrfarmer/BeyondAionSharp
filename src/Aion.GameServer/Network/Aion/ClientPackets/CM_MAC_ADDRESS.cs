using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.LoginServer;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MAC_ADDRESS (-Nemesiss-, KID, ViAl, Neon). Client sends connection/system info (IP traceroute, MAC, HDD serial); triggers loginserver auth. LoginServer red-tolerated.</summary>
public class CM_MAC_ADDRESS : AionClientPacket
{
    private string macAddress, hddSerial;

    public CM_MAC_ADDRESS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadC(); // unk
        int routeSteps = ReadUH();
        for (int i = 0; i < routeSteps; i++)
            ReadD(); // ip traceroute, see -> System.out.println(NetworkUtils.intToIpString(readD()))
        macAddress = ReadS();
        hddSerial = FixHddSerial(ReadS());
        ReadD(); // local IP, see -> System.out.println(NetworkUtils.intToIpString(readD()))
    }

    protected override void RunImpl()
    {
        AionConnection con = GetConnection();
        con.SetMacAddress(macAddress);
        con.SetHddSerial(hddSerial);
        global::Aion.GameServer.Network.LoginServer.LoginServer.GetInstance().AuthenticateClient(con);
    }

    private static string FixHddSerial(string hddSerial)
    {
        if (hddSerial.Length != 0 && (hddSerial.Length <= 2 || !Regex.IsMatch(hddSerial, "^[0-9a-zA-Z _-]+$")))
        {
            // not a serial number string (some clients send weird but deterministic data)
            return "0x" + Convert.ToHexString(Encoding.Unicode.GetBytes(hddSerial));
        }
        else if (Regex.IsMatch(hddSerial, "^[a-zA-Z0-9] [a-zA-Z0-9_-].*|.*[a-zA-Z0-9_-] [a-zA-Z0-9]$"))
        { // check if second or penultimate char is a space
            return Regex.Replace(hddSerial, "(.)(.)", "$2$1").Trim(); // for some reason some clients send switched chars
        }
        return hddSerial.Trim();
    }
}
