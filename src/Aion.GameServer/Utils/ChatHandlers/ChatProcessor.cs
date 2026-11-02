using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
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
    private static readonly ILogger log = AionLog.For(nameof(ChatProcessor));
    private readonly Dictionary<string, ChatCommand> commandHandlers = new Dictionary<string, ChatCommand>();

    private ChatProcessor()
    {
    }

    public void Init()
    {
        ScriptManager scriptManager = new ScriptManager();
        scriptManager.SetGlobalClassListener(new ChatCommandsLoader(this));
        scriptManager.Load(CommandsConfig.HANDLER_DIRECTORIES);
        log.LogInformation("Loaded {CommandHandlersSize} commands", commandHandlers.Count);
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
        string cmdName = cmd.GetAliasWithPrefix();
        if (cmd.GetLevel() < 0)
            throw new System.ArgumentException("Failed to register " + cmd.GetType().Name + ": Invalid access level for " + cmdName + ".");
        if (!commandHandlers.TryAdd(cmdName.ToLower(), cmd))
            throw new System.ArgumentException("Failed to register " + cmd.GetType().Name + ": " + cmdName + " is already registered.");
    }

    public bool HandleChatCommand(Player player, string text)
    {
        if (text == null || text.Length == 0)
            return false;

        if (!text.StartsWith(AdminCommand.PREFIX) && !text.StartsWith(PlayerCommand.PREFIX))
            return false;
        int splitIndex = text.IndexOf(' ');
        string cmdName = splitIndex == -1 ? text : text.Substring(0, splitIndex);
        ChatCommand cmd = GetCommand(cmdName);
        if (cmd == null)
            return false;
        string cmdParams = text.Substring(cmdName.Length);
        return cmd.Process(player, GetParamsFromString(cmdParams));
    }

    public void HandleConsoleCommand(Player player, string text)
    {
        if (text == null || text.Length == 0)
            return;

        int splitIndex = text.IndexOf(' ');
        string cmdName = splitIndex == -1 ? text : text.Substring(0, splitIndex);
        if (!(GetCommand(cmdName) is ConsoleCommand consoleCommand))
        {
            PacketSendUtility.SendMessage(player, "The command " + cmdName + " is not implemented.");
            return;
        }
        string cmdParams = text.Substring(cmdName.Length);
        consoleCommand.Process(player, GetParamsFromString(cmdParams));
    }

    internal static string[] GetParamsFromString(string paramsStr)
    {
        if (paramsStr == null || paramsStr.Trim().Length == 0)
            return new string[0];

        // advanced split to keep item links etc. in one piece (splitting on spaces, but only outside of square brackets)
        // Java String.split does not return capturing groups. Keep the alternation non-capturing so .NET does not
        // inject an empty argument between every ordinary command parameter.
        return Regex.Split(paramsStr.Trim(), " +(?=[^\\]]*(?:\\[|$))");
    }

    /// <summary>
    /// Command keys are registered with their prefix (<c>//</c> for admin, <c>.</c> for player commands, none for console), while
    /// configs and stored accesses use the bare alias, so an alias without prefix also resolves to its command.
    /// </summary>
    private ChatCommand GetCommand(string alias)
    {
        ChatCommand command = commandHandlers.GetValueOrDefault(alias.ToLower());
        if (command != null)
            return command;
        foreach (ChatCommand candidate in commandHandlers.Values)
            if (string.Equals(candidate.GetAlias(), alias, StringComparison.OrdinalIgnoreCase))
                return candidate;
        return null;
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

    /// <summary>True if a command with this alias exists, with or without its prefix.</summary>
    public bool IsCommandExists(string alias)
    {
        return GetCommand(alias) != null;
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
