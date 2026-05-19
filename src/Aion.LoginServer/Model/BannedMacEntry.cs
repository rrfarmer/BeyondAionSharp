namespace Aion.LoginServer.Model;

public sealed record BannedMacEntry(string Mac, DateTime Time, string Details);
