using System.Collections.Generic;
using System.IO;
using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Administration;

/// <summary>Java parity: configs/administration/CommandsConfig (Neon). @Properties keyPattern→Dictionary; Map&lt;String,Byte&gt;→Dictionary&lt;string,sbyte&gt; (Java Byte is signed); File[]→FileInfo[]. Bound by the config loader (ConfigurableProcessor).</summary>
public static class CommandsConfig
{
    /// <summary>Java parity: @Properties(keyPattern = "^[a-zA-Z0-9_]+$"). No capturing group → whole key is the map key.</summary>
    [Properties(keyPattern: "^[a-zA-Z0-9_]+$")]
    public static Dictionary<string, sbyte> ACCESS_LEVELS;

    /// <summary>Location of chat command *.java handlers. Key: gameserver.commands.handler_directories.</summary>
    [Property(key: "gameserver.commands.handler_directories", defaultValue: "./data/handlers/admincommands, ./data/handlers/playercommands, ./data/handlers/consolecommands")]
    public static FileInfo[] HANDLER_DIRECTORIES;
}
