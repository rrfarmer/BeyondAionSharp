using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>One immutable publication point for selected statistics and their
/// compiled consumers. Profile-only builds cannot publish a partial correction.</summary>
internal static partial class RetailNpcStatCatalog
{
    internal const string ManifestEnvironment = "AION_RETAIL_STATS_MANIFEST";
    internal const string HashEnvironment = "AION_RETAIL_STATS_MANIFEST_SHA256";
    private sealed partial record Package(FrozenDictionary<(int Map, int Npc), Member> Profiles, FrozenSet<int> Maps);
    private static Package? active;
    internal static bool Enabled => Volatile.Read(ref active) is not null;

    internal static IReadOnlyCollection<int> SupportedMaps => Volatile.Read(ref active) is { } package ? package.Maps : Array.Empty<int>();
    internal static IReadOnlyCollection<Member> Members => Volatile.Read(ref active) is { } catalog ? catalog.Profiles.Values : Array.Empty<Member>();

    internal static bool TryGet(int mapId, int npcId, out SkyTempleNpcProfile profile)
    {
        profile = null!;
        if (Volatile.Read(ref active) is not { } catalog || !catalog.Maps.Contains(mapId)) return false;
        if (!catalog.Profiles.TryGetValue((mapId,npcId),out var member) && !catalog.Profiles.TryGetValue((0,npcId),out member)) return false;
        profile = member.Profile;
        return true;
    }

    internal static async Task InitializeAsync(string? path, string? sha256, CancellationToken cancellationToken = default)
    {
        if (Enabled) throw new InvalidOperationException("Retail statistics cannot reload after publication");
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(sha256)) return;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sha256))
            throw new InvalidDataException("Retail statistics manifest and separate SHA256 are both required");
        string full = Path.GetFullPath(path);
        var file = new FileInfo(full);
        if (!file.Exists || file.Length is <= 0 or > 64 * 1024 * 1024)
            throw new InvalidDataException("Missing/oversized statistics manifest: " + full);
        var parsed = Parse(await File.ReadAllBytesAsync(full, cancellationToken), sha256, Path.GetDirectoryName(full)!);
        bool runtimeReady = false;
        ValidateRuntimePublication(parsed, ref runtimeReady);
        if (!runtimeReady)
            throw new InvalidDataException("Retail statistics runtime consumers are unavailable; profile data was not published");
        Volatile.Write(ref active, parsed);
    }

    private static Package Parse(byte[] bytes, string expected, string directory)
    {
        CheckHash(bytes, expected, "statistics manifest");
        using var doc = JsonDocument.Parse(bytes, new() { MaxDepth = 384 });
        var root = doc.RootElement; RetailNpcDatasetValidation.UniqueJson(root);
        ValidateManifestIdentity(root);
        RetailNpcReviewedIdentityBindings? identities = null;
        if (root.TryGetProperty("identity_review", out var review))
            identities = RetailNpcReviewedIdentityBindings.Admit(ReadAsset(review, directory), review.GetProperty("sha256").GetString()!);
        var (profiles, maps) = ReadProfiles(root, expected, directory, identities);
        var package = new Package(profiles.ToFrozenDictionary(), maps.ToFrozenSet());
        ReadNormalPackage(root, directory, ref package);
        ReadBuffPackage(root, directory, ref package);
        ReadAdditionalPackage(root, expected, directory, ref package);
        return package;
    }

    static partial void ReadNormalPackage(System.Text.Json.JsonElement root, string directory, ref Package package);
    static partial void ReadBuffPackage(System.Text.Json.JsonElement root, string directory, ref Package package);
    static partial void ReadAdditionalPackage(JsonElement root, string expected, string directory, ref Package package);
    static partial void ValidateRuntimePublication(Package package, ref bool ready);

    internal static IDisposable InstallForTests(byte[] bytes, string hash, string directory)
    {
        var previous = active; active = Parse(bytes, hash, directory); return new Restore(previous);
    }
    internal static IDisposable PreservePublicationForTests() => new Restore(Volatile.Read(ref active));
    private sealed class Restore(Package? previous) : IDisposable
    {
        public void Dispose() => Volatile.Write(ref active, previous);
    }
}
