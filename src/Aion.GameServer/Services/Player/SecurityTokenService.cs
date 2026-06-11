using System;
using System.Security.Cryptography;
using Aion.GameServer.Model.Account;

namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/SecurityTokenService. SecureRandom→RandomNumberGenerator; Base64.getEncoder().encodeToString→Convert.ToBase64String.</summary>
public class SecurityTokenService
{
    private SecurityTokenService()
    {
    }

    public static void GenerateToken(Account account)
    {
        byte[] token = new byte[16];
        using (RandomNumberGenerator secureRandom = RandomNumberGenerator.Create())
        {
            secureRandom.GetBytes(token);
        }
        account.SetSecurityToken(Convert.ToBase64String(token));
    }
}
