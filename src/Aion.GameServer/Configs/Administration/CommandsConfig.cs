using System.Collections.Generic;
using System.IO;

namespace Aion.GameServer.Configs.Administration;

/// <summary>Java parity: configs/administration/CommandsConfig (Neon). @Properties keyPattern→Dictionary; Map<String,Byte>→Dictionary<string,sbyte> (signed byte); File[]→FileInfo[]. Populated by config loader.</summary>
public static class CommandsConfig
{
    /// <summary>@Properties keyPattern ^[a-zA-Z0-9_]+$</summary>
    public static Dictionary<string, sbyte> ACCESS_LEVELS;

    /// <summary>Location of chat command *.java handlers. Key: gameserver.commands.handler_directories (default ./data/handlers/{admin,player,console}commands)</summary>
    public static FileInfo[] HANDLER_DIRECTORIES;
}
