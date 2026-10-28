using System.Security.Cryptography;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcTribeMatrixTests
{
    [SkippableFact]
    public void ReconstructedStartupMatrixMatchesEveryRetailDumpCell()
    {
        string? source = Environment.GetEnvironmentVariable("AION_RETAIL_TRIBE_SOURCE");
        string? dump = Environment.GetEnvironmentVariable("AION_RETAIL_TRIBE_DUMP");
        Skip.If(string.IsNullOrEmpty(source) || string.IsNullOrEmpty(dump), "Requires pinned retail tribe files");
        byte[] raw = File.ReadAllBytes(source!);
        var matrix = RetailNpcTribeMatrix.ReadReviewedSource(raw);
        byte[] changed = (byte[])raw.Clone(); changed[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => RetailNpcTribeMatrix.ReadReviewedSource(changed));
        byte[] dumpBytes = File.ReadAllBytes(dump!);
        Assert.Equal("cd895f3a0411082cb1d6b4474e4f3c0906727198ba3c13e36fc6584b6314ff2e",
            Convert.ToHexStringLower(SHA256.HashData(dumpBytes)));
        // The dump header is truncated; its 955 complete named data rows and
        // 955 values per row provide the independent comparison order.
        var rows = File.ReadLines(dump!).Skip(1).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Split(',').Select(y => y.Trim()).ToArray()).ToArray();
        Assert.Equal(955, rows.Length);
        Assert.All(rows, x => Assert.Equal(956, x.Length));
        string[] names = rows.Select(x => x[0]).ToArray();
        int mismatch = 0;
        for (int i = 0; i < rows.Length; i++)
            for (int j = 0; j < names.Length; j++)
            {
                int expected = rows[i][j + 1] switch
                {
                    "A" => 0, "H" => 1, "N" => 2, "F" => 3, "S" => 4, "O" => 5,
                    _ => throw new InvalidDataException("Unknown retail dump relation")
                };
                if (matrix.Find(names[i], names[j]) != expected) mismatch++;
            }
        Assert.Equal(0, mismatch);
        Assert.Equal(0, matrix.Find("AggressiveSingleMonster", "PC_Dark"));
        Assert.Equal(matrix.Find("Monster", "PC_Dragon"), matrix.Find("aggressivesinglemonster", "pc_dragon"));
        Assert.Null(matrix.Find("unreviewed tribe", "PC_Dark"));
    }
}
