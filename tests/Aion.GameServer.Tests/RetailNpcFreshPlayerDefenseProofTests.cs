using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcFreshPlayerDefenseProofTests
{
    [SkippableFact]
    public void RetailCreatureInitializationAndHostClassTemplatesBeginWithZeroDefense()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_17860_PLAYER_BASE_PROOF");
        Skip.If(string.IsNullOrWhiteSpace(path),"Requires byte-checked 5.8 player construction proof");
        byte[] bytes=File.ReadAllBytes(path!);
        Assert.Equal("8e401439ce45db99a1b3739006767ff45a61cf1446f535e21099dfdf6af5bc19",Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var proof=JsonDocument.Parse(bytes);
        Assert.Equal("PASS_STATIC_PLAYER_PDEF_INIT_ZERO",proof.RootElement.GetProperty("result").GetString());
        Assert.Equal(0,proof.RootElement.GetProperty("fresh_initial_value").GetInt32());
        Assert.Contains("does not establish any live player value",proof.RootElement.GetProperty("scope").GetString());
        var classes=Enum.GetValues<PlayerClass>();Assert.Equal(17,classes.Length);
        foreach(var playerClass in classes)
        foreach(int level in new[]{1,23,55,65})
            Assert.Equal(0,playerClass.CreateStatsTemplate(level).GetPdef());
    }
}
