using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Utils.ChatHandlers;

/// <summary>Java parity: utils/chathandlers/ChatCommand (KID, Neon). Base for player/console/admin chat commands. java.awt.Color preserved; Java reflection (Class.forName/getEnumConstants) -> Type.GetType/Enum.GetValues (best-effort). CommandsConfig/ChatUtil red-tolerated.</summary>
public abstract class ChatCommand
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ChatCommand));
    private readonly string prefix;
    private readonly string alias;
    private readonly string description;
    private string syntaxInfo;

    public ChatCommand(string prefix, string alias, string description)
    {
        this.prefix = prefix;
        this.alias = alias;
        this.description = description;
    }

    public bool Run(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 1 && "help".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            SendMessagePackets(player, "Command: " + ChatUtil.Color(GetAliasWithPrefix(), Color.White) + "\n\t"
                + (GetDescription().Length == 0 ? "No description available." : GetDescription()) + "\n" + GetSyntaxInfo());
            return true;
        }

        try
        {
            try
            {
                Execute(player, paramsArr);
            }
            catch (ArgumentException e)
            {
                SendInfo(player, ToErrorMessage(e));
            }
        }
        catch (Exception t)
        {
            log.LogError(t, "Exception executing chat command \"" + GetAliasWithPrefix() + " " + string.Join(" ", paramsArr) + "\" - Player: " + player.GetName()
                + ", Target: " + player.GetTarget());
            return false;
        }
        return true;
    }

    public string GetPrefix()
    {
        return prefix;
    }

    public string GetAlias()
    {
        return alias;
    }

    public string GetDescription()
    {
        return description;
    }

    public string GetAliasWithPrefix()
    {
        return prefix + alias;
    }

    protected void SetSyntaxInfo(params string[] lines)
    {
        this.syntaxInfo = ParseSyntaxInfo(lines);
    }

    public virtual string GetSyntaxInfo()
    {
        if (syntaxInfo == null) // init default info if handler did not set any syntax info
            SetSyntaxInfo();
        return syntaxInfo;
    }

    private string ParseSyntaxInfo(params string[] lines)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Syntax:");
        if (lines.Length > 0)
        {
            bool containsSquareBrackets = false;
            foreach (string info in lines)
            {
                string[] split = info.Split(new[] { " - " }, 2, StringSplitOptions.None);
                if (split.Length == 2)
                {
                    if (!containsSquareBrackets && split[0].Contains("["))
                        containsSquareBrackets = true;
                    sb.Append("\n\t").Append(ChatUtil.Color(GetAliasWithPrefix(), Color.White)).Append(' ');
                    sb.Append(Regex.Replace(split[0], "([^<>\\[\\]| ]+)", ChatUtil.Color("$1", Color.White)).Replace("[[color:f;", "[[color:f" + (char)0x200B + ";").Trim());
                    sb.Append(" - ");
                    sb.Append(split[1]);
                }
                else
                {
                    sb.Append("\n").Append(info);
                }
            }
            if (containsSquareBrackets)
                sb.Append("\nNote: Parameters enclosed in square brackets are optional.");
        }
        else
        {
            sb.Append("\n\tNo syntax info available.");
        }
        return sb.ToString();
    }

    public byte GetLevel()
    {
        if (!CommandsConfig.ACCESS_LEVELS.TryGetValue(alias, out sbyte level))
            throw new NullReferenceException("Missing access level for " + GetAliasWithPrefix());
        return (byte)level;
    }

    /// <summary>True if player is allowed to use this command.</summary>
    public abstract bool ValidateAccess(Player player);

    /// <summary>Handles processing of a chat command. True if command was executed.</summary>
    internal abstract bool Process(Player player, params string[] paramsArr);

    /// <summary>Code executed after successful access validation; ArgumentException is caught and the message printed to the player.</summary>
    /// <remarks>Java parity: declared <c>protected abstract</c> in ChatCommand, but GoTo widens its override to <c>public</c> so cross-package wrappers (Teleportto) can call <c>getCommand(GoTo.class).execute(...)</c>. C# cannot widen visibility on an override, so the base contract is public to permit the same cross-command dispatch.</remarks>
    public abstract void Execute(Player player, params string[] paramsArr);

    /// <summary>Override if the default message extraction is not sufficient. Null sends default syntax info.</summary>
    protected string ToErrorMessage(ArgumentException e)
    {
        string msg = e.Message;
        if (msg != null && msg.StartsWith("No enum constant ")) // "No enum constant com.aionemu.gameserver.model.siege.SiegeRace.invalidName"
        {
            string[] enumParts = msg.Substring(17).Split('.'); // -> ["com", ..., "SiegeRace", "invalidName"]
            string enumName = enumParts[enumParts.Length - 2]; // -> "SiegeRace"
            // split camelCase word (https://stackoverflow.com/a/7599674)
            string[] enumNameParts = Regex.Split(enumName, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])"); // -> ["Siege", "Race"]
            enumName = string.Join(" ", enumNameParts).ToLower(); // -> "siege race"
            msg = "Invalid " + enumName + ".";
            try
            {
                Type enumClass = Type.GetType(string.Join(".", enumParts.Take(enumParts.Length - 1)));
                string values = string.Join(", ", Enum.GetValues(enumClass).Cast<object>().Select(v => v.ToString()));
                msg += "\nPossible values:\n" + values;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not get enum values for " + enumName);
            }
        }
        else if (e is FormatException) // Integer.parseInt and Long.parseLong don't provide nice error messages
        {
            if (msg != null && msg.StartsWith("For input string: "))
                msg = "Invalid number: " + msg.Substring(18);
            else
                msg = "Invalid number.";
        }
        return msg;
    }

    /// <summary>Sends an info message to the player; with no message (or null) the default syntax info is sent.</summary>
    protected void SendInfo(Player player, params string[] message)
    {
        StringBuilder sb = new StringBuilder();
        if (message.Length > 1 || message.Length == 1 && message[0] != null)
        {
            for (int i = 0; i < message.Length; i++)
            {
                if (i > 0)
                    sb.Append('\n');
                sb.Append(message[i]);
            }
        }
        else
        {
            sb.Append(GetSyntaxInfo());
        }
        SendMessagePackets(player, sb.ToString());
    }

    /// <summary>Sends the formatted input message with as little packets as possible.</summary>
    private static void SendMessagePackets(Player player, string message)
    {
        int lineLimit = 15; // length limit check alone is not safe if you send chat links (they can exceeded the display limit on client side)
        string[] lines = message.Split('\n');
        if (message.Length <= SM_MESSAGE.MESSAGE_SIZE_LIMIT && lines.Length <= lineLimit)
        {
            PacketSendUtility.SendMessage(player, message);
        }
        else
        {
            StringBuilder sb = new StringBuilder(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                if (i % lineLimit == 0 || sb.Length + 1 + lines[i].Length > SM_MESSAGE.MESSAGE_SIZE_LIMIT) // current length + newLine char + next line length
                {
                    SendSafe(player, sb.ToString());
                    sb.Length = 0;
                }
                else
                {
                    sb.Append('\n');
                }
                sb.Append(lines[i]);
            }
            SendSafe(player, sb.ToString());
        }
    }

    /// <summary>Divides up the (single line) message if necessary and sends multiple packets to stay within the character limit per line.</summary>
    private static void SendSafe(Player player, string msg)
    {
        if (msg.Length > SM_MESSAGE.MESSAGE_SIZE_LIMIT)
        {
            int splitIndex = FindSplitIndex(msg, ',', ']', ' ') + 1;
            PacketSendUtility.SendMessage(player, msg.Substring(0, splitIndex));
            SendSafe(player, msg.Substring(splitIndex));
        }
        else
        {
            PacketSendUtility.SendMessage(player, msg);
        }
    }

    private static int FindSplitIndex(string msg, params char[] splitChars)
    {
        int searchStartIndex = Math.Min(msg.Length / 2, SM_MESSAGE.MESSAGE_SIZE_LIMIT / 2);
        foreach (char splitChar in splitChars)
        {
            int splitIndex = msg.IndexOf(splitChar, searchStartIndex);
            if (splitIndex > -1 && splitIndex <= SM_MESSAGE.MESSAGE_SIZE_LIMIT)
                return splitIndex;
        }
        return SM_MESSAGE.MESSAGE_SIZE_LIMIT;
    }
}
