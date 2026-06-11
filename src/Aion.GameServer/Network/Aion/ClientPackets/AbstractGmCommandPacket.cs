using System.Collections.Generic;
using System.Text.RegularExpressions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.ChatHandlers;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/AbstractGmCommandPacket. Base for GM console-command packets; reads the command string and dispatches to ChatProcessor. Java regex excludes the U+0000..U+013E range; built from char codes here to keep source clean. ChatProcessor red-tolerated.</summary>
public abstract class AbstractGmCommandPacket : AionClientPacket
{
    public const string UNSUPPORTED_COMMAND_CHAR_PLACEHOLDER = "?"; // client sends this for each unsupported char in the command
    private static readonly Regex unsupportedCommandChars = new Regex("[^" + (char)0x0000 + "-" + (char)0x013E + "]");
    protected string command;

    protected AbstractGmCommandPacket(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        command = ReadS();
    }

    protected override void RunImpl()
    {
        ChatProcessor.GetInstance().HandleConsoleCommand(GetConnection().GetActivePlayer(), command);
    }

    public static string ReplaceUnsupportedCommandChars(string input)
    {
        return unsupportedCommandChars.Replace(input, UNSUPPORTED_COMMAND_CHAR_PLACEHOLDER);
    }
}
