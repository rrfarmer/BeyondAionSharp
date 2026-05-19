namespace Aion.LoginServer.Model;

public sealed record ReconnectingAccount(Account Account, int ReconnectionKey);
