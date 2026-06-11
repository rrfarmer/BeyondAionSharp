using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Scripting;
using Aion.GameServer.Configs;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Utils.ChatHandlers;

/// <summary>Java parity: utils/chathandlers/ChatProcessor (KID, Rolandas, Neon). Registers and dispatches chat/console commands. ScriptManager/GameEngine (scripting pillar) red-tolerated.</summary>
public class ChatProcessor : GameEngine
{

    // GameEngine async lifecycle (infra adapter over the faithful Init()).
    public string Name => GetType().Name;
    public System.Threading.Tasks.ValueTask InitAsync(System.Threading.CancellationToken cancellationToken) { Init(); return System.Threading.Tasks.ValueTask.CompletedTask; }
    public System.Threading.Tasks.ValueTask ShutdownAsync(System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.ValueTask.CompletedTask;
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ChatProcessor));
    private readonly Dictionary<string, ChatCommand> commandHandlers = new Dictionary<string, ChatCommand>();

    private ChatProcessor()
    {
    }

    public void Init()
    {
        ScriptManager scriptManager = new ScriptManager();
        scriptManager.SetGlobalClassListener(new ChatCommandsLoader(this));
        scriptManager.Load(CommandsConfig.HANDLER_DIRECTORIES);
        log.LogInformation("Loaded " + commandHandlers.Count + " commands.");
    }

    public void Reload()
    {
        Dictionary<string, ChatCommand> oldCommands = new Dictionary<string, ChatCommand>(commandHandlers);
        try
        {
            Config.Load(typeof(CommandsConfig));
            commandHandlers.Clear();
            Init();
        }
        catch
        {
            commandHandlers.Clear();
            foreach (KeyValuePair<string, ChatCommand> e in oldCommands)
                commandHandlers[e.Key] = e.Value;
            throw;
        }
    }

    public void RegisterCommand(ChatCommand cmd)
    {
        if (cmd.GetLevel() < 0)
            throw new System.NullReferenceException("Failed to register chat command: Invalid access level for " + cmd.GetAlias() + ".");
        if (!commandHandlers.TryAdd(cmd.GetAlias().ToLower(), cmd))
            throw new System.ArgumentException("Failed to register chat command: " + cmd.GetAlias() + " is already registered.");
    }

    public bool HandleChatCommand(Player player, string text)
    {
        if (text == null || text.Length == 0)
            return false;

        string prefix;
        if (text.StartsWith(AdminCommand.PREFIX))
            prefix = AdminCommand.PREFIX;
        else if (text.StartsWith(PlayerCommand.PREFIX))
            prefix = PlayerCommand.PREFIX;
        else
            return false;
        int splitIndex = text.IndexOf(' ');
        string cmdName = text.Substring(prefix.Length, (splitIndex == -1 ? text.Length : splitIndex) - prefix.Length);
        ChatCommand cmd = GetCommand(cmdName);
        if (cmd == null)
            return false;
        string cmdParams = splitIndex == -1 ? "" : text.Substring(splitIndex);
        return cmd.Process(player, GetParamsFromString(cmdParams));
    }

    public void HandleConsoleCommand(Player player, string text)
    {
        if (text == null || text.Length == 0)
            return;

        if (!text.StartsWith(ConsoleCommand.PREFIX))
            return;

        string cmdName = text.Split(' ')[0];
        string cmdParams = text.Substring(cmdName.Length);

        // TODO remove this temporary fix (AdminCommand is already called addskill)
        if (cmdName.EndsWith("addskill"))
            cmdName = cmdName.Replace("addskill", "addcskill");

        ChatCommand cmd = GetCommand(cmdName.Substring(ConsoleCommand.PREFIX.Length));

        if (cmd == null)
            PacketSendUtility.SendMessage(player, "The command " + cmdName + " is not implemented.");
        else if (cmd is ConsoleCommand)
            cmd.Process(player, GetParamsFromString(cmdParams));
    }

    private string[] GetParamsFromString(string paramsStr)
    {
        if (paramsStr == null || paramsStr.Trim().Length == 0)
            return new string[0];

        // advanced split to keep item links etc. in one piece (splitting on spaces, but only outside of square brackets)
        return Regex.Split(paramsStr.Trim(), " +(?=[^\\]]*(\\[|$))");
    }

    private ChatCommand GetCommand(string alias)
    {
        return commandHandlers.GetValueOrDefault(alias.ToLower());
    }

    /// <summary>
    /// Command of the given type. It will not find a command if called from a non-chatcommand class (different class loaders);
    /// you can still get a command from a core class via GetCommand(string) but won't be able to cast it to its runtime type (supertypes like ChatCommand work).
    /// </summary>
    public T GetCommand<T>() where T : ChatCommand
    {
        return (T)commandHandlers.Values.Where(c => typeof(T) == c.GetType()).FirstOrDefault();
    }

    public List<ChatCommand> GetCommandList()
    {
        return new List<ChatCommand>(commandHandlers.Values);
    }

    public bool IsCommandAllowed(Player executor, string alias)
    {
        return IsCommandAllowed(executor, GetCommand(alias));
    }

    public bool IsCommandAllowed(Player executor, ChatCommand command)
    {
        return command != null && command.ValidateAccess(executor);
    }

    public bool IsCommandExists(string alias)
    {
        return commandHandlers.ContainsKey(alias.ToLower());
    }

    public static ChatProcessor GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly ChatProcessor instance = new ChatProcessor();
    }
}
