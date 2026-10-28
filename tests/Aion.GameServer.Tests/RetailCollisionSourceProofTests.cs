using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailCollisionSourceProofTests
{
    private static RetailCollisionCosineSourceEvidence Evidence() => new(
        RetailCollisionCosineSourceProof.ProofSha256, RetailCollisionCosineSourceProof.IndependentReviewSha256,
        new(true, true, true, true), "conditional instruction interpretation; actual runtime environment unobserved");

    [Fact]
    public void ConditionalProofDoesNotMasqueradeAsObservedReturnOrBecomeAMissingReceiptDefault()
    {
        var admitted = RetailCollisionCosineSourceProof.Admit(Evidence(), "explicit-mathematical-context");
        Assert.True(admitted.Success); Assert.False(admitted.RuntimeVerified);
        var receipt = admitted.Receipt!;
        Assert.Equal("ConditionalStaticSourceProof", receipt.Origin); Assert.False(receipt.SyntheticFixture);
        Assert.False(receipt.RuntimeVerified); Assert.Equal(0x3f3504f3u, receipt.ResultBits);
        var face = new RetailCollisionTriangle(0, new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), 0, 0);
        Assert.True(RetailCollisionMath.GroundEligibility(face, receipt).Eligible);
        Assert.Equal(RetailCollisionStatus.MissingCosineEvidence, RetailCollisionMath.GroundEligibility(face, null).Status);
    }

    [Theory]
    [InlineData("proof")] [InlineData("review")] [InlineData("nearest-null")] [InlineData("nearest-false")]
    [InlineData("exceptions-null")] [InlineData("exceptions-false")] [InlineData("isa-null")] [InlineData("isa-false")]
    [InlineData("return-null")] [InlineData("return-false")] [InlineData("evidence")] [InlineData("revision")]
    public void EveryAssumptionAndProofIdentityIsExplicitRatherThanInferred(string failure)
    {
        var e = Evidence();
        e = failure switch
        {
            "proof" => e with { ProofSha256 = new('0', 64) }, "review" => e with { IndependentReviewSha256 = new('0', 64) },
            "nearest-null" => e with { Assumptions = e.Assumptions with { NearestEven = null } },
            "nearest-false" => e with { Assumptions = e.Assumptions with { NearestEven = false } },
            "exceptions-null" => e with { Assumptions = e.Assumptions with { MaskedFloatingExceptions = null } },
            "exceptions-false" => e with { Assumptions = e.Assumptions with { MaskedFloatingExceptions = false } },
            "isa-null" => e with { Assumptions = e.Assumptions with { ConsumedInstructionsAvailable = null } },
            "isa-false" => e with { Assumptions = e.Assumptions with { ConsumedInstructionsAvailable = false } },
            "return-null" => e with { Assumptions = e.Assumptions with { NormalReturn = null } },
            "return-false" => e with { Assumptions = e.Assumptions with { NormalReturn = false } },
            "evidence" => e with { Evidence = "" }, _ => e
        };
        var result = RetailCollisionCosineSourceProof.Admit(e, failure == "revision" ? "" : "fixture");
        Assert.False(result.Success); Assert.Equal(RetailCollisionStatus.MissingCosineEvidence, result.Status); Assert.Null(result.Receipt);
    }

    [Theory]
    [InlineData("result")] [InlineData("synthetic")] [InlineData("capture")]
    public void ConditionalReceiptCannotCarryConflictingBitsOrEvidence(string failure)
    {
        var receipt = RetailCollisionCosineSourceProof.Admit(Evidence(), "fixture").Receipt!;
        receipt = failure switch { "result" => receipt with { ResultBits = 0x3f3504f4 },
            "synthetic" => receipt with { SyntheticFixture = true }, _ => receipt with { CaptureSha256 = new('0', 64) } };
        var face = new RetailCollisionTriangle(0, new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), 0, 0);
        var result = RetailCollisionMath.GroundEligibility(face, receipt);
        Assert.Equal(RetailCollisionStatus.MissingCosineEvidence, result.Status); Assert.Null(result.Eligible);
    }

    [SkippableFact]
    public void OptInActualPilotAssetsComputeEveryCellWithExplicitConditionalContextAndUnknownDynamicScene()
    {
        string? output = Environment.GetEnvironmentVariable("AION_RETAIL_COLLISION_QUERY_AUDIT_OUTPUT");
        Skip.If(string.IsNullOrWhiteSpace(output), "Set an explicit fresh output path to run the conditional actual-asset grid audit.");
        Assert.False(File.Exists(output), "Never overwrite prior audit evidence.");
        string root = Environment.GetEnvironmentVariable("AION_RETAIL_COLLISION_FIXTURE_ROOT")
            ?? @"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
        byte[] mesh = File.ReadAllBytes(Path.Combine(root, "MeshMaterial.Dat"));
        byte[] collision = File.ReadAllBytes(Path.Combine(root, "Collision.Dat"));
        byte[] height = File.ReadAllBytes(Path.Combine(root, "heightmap40.dat"));
        static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        Assert.Equal("e964645652e44f064ebb84e0588677d36bbf4be472db88a5bd5af30057355977", Hash(mesh));
        Assert.Equal("e5dd2cacc876c03f939fee77610db592e77d8b0322340434f7f961f2d49b7ffc", Hash(collision));
        Assert.Equal("a6bd0f7a3f0d73b869b4e98c66146b1caf523e709bcb158dcc66ab6a9904d674", Hash(height));
        var timer = Stopwatch.StartNew();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var raw = CollisionFixture.Read(mesh, collision, 512, 512);
        var admitted = RetailCollisionCosineSourceProof.Admit(Evidence(), "pilot-source-grid-RNE-masked-ISA-normal-return");
        Assert.True(admitted.Success);
        var prepare = RetailCollisionMath.Prepare(raw, admitted.Receipt, new(1_000_000, 1_000_000, 128L * 1024 * 1024));
        Assert.True(prepare.Success, prepare.Detail); Assert.Equal(462168, prepare.ProcessedReferences);
        var heightRead = RetailHeightMap40Reader.Read(height); Assert.True(heightRead.Success, heightRead.Error?.ToString());
        var terrain = heightRead.Data!;
        var sources = new RetailCollisionSources(
            new(new(Path.Combine(root, "MeshMaterial.Dat"), raw.MeshSha256, mesh.Length), raw.MeshGuid, 512, 512),
            new(new(Path.Combine(root, "Collision.Dat"), raw.CollisionSha256, collision.Length), raw.CollisionGuid, 512, 512),
            new(new(Path.Combine(root, "heightmap40.dat"), Hash(height), height.Length), terrain.BuildGuid, 512, 512),
            new("pinned NPC image declaration", RetailGroundPathSearch.SupportedNpcBinarySha256, RetailGroundPathSearch.SupportedNpcBinaryBytes));
        var sceneResult = RetailCollisionScene.Create(prepare.Data!, terrain, sources,
            new(new(0, 0, -10240), new(512, 512, 10240)),
            new("actual-pilot-assets;conditional-mathematical-world-bounds", "conditional-instance", "no-live-scene-observation", "pinned-assets",
                admitted.Receipt!.CaptureSha256, "No actual loaded world bounds, actor, CPU mode or field membership is asserted.", true),
            [], RetailCollisionMaterialPolicy.FromPinnedFileImage("explicit-pinned-image-declaration"), new(1024, 0));
        Assert.True(sceneResult.Success, sceneResult.Detail); var scene = sceneResult.Scene!;
        var limits = new RetailCollisionQueryLimits(20, 1000, 20000, 1000, 32768);
        var counts = new Dictionary<string, int>(); var failures = new List<object>();
        int oddCells = 0, unexpected = 0;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
        {
            byte selector = terrain.BytePlane2.Span[y * terrain.Stride + x];
            if ((selector & 1) != 0) oddCells++;
            var origin = new RetailSearchPoint(x * 2 + 1, y * 2 + 1, 512);
            var destination = origin with { Z = 0 };
            foreach (var kind in Enum.GetValues<RetailCollisionQueryKind>())
            {
                var result = kind switch
                {
                    RetailCollisionQueryKind.PredictionProjection => RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, origin, limits),
                    RetailCollisionQueryKind.GroundProjection => RetailCollisionQuery.ProjectGround(scene, scene.Stamp, origin, limits),
                    RetailCollisionQueryKind.BooleanSegment => RetailCollisionQuery.SegmentBlocked(scene, scene.Stamp, origin, destination, 0, 0, .0316f, limits),
                    _ => RetailCollisionQuery.FirstContact(scene, scene.Stamp, origin, destination, 0, .0316f, limits)
                };
                string key = $"{kind}/{result.Status}/{result.NativeResult?.ToString() ?? "unavailable"}";
                counts[key] = counts.GetValueOrDefault(key) + 1;
                bool allowed = result.Status == RetailCollisionStatus.Computed
                    || result.Status == RetailCollisionStatus.UnsupportedDynamicScene && (selector & 1) != 0;
                if (!allowed) { unexpected++; if (failures.Count < 32) failures.Add(new { x, y, selector, kind, result.Status, result.Detail }); }
                if (result.Status != RetailCollisionStatus.Computed) { Assert.Null(result.NativeResult); Assert.Null(result.Point); }
                Assert.False(result.RuntimeVerified); Assert.False(result.AppliesActorClearance); Assert.False(result.NativeTargetExecuted);
                byte[] row = new byte[32];
                BinaryPrimitives.WriteInt32LittleEndian(row, x); BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(4), y);
                BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(8), (int)kind); BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(12), (int)result.Status);
                BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(16), result.NativeResult ?? uint.MaxValue);
                if (result.Point is { } point)
                {
                    BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(20), point.X);
                    BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(24), point.Y);
                    BinaryPrimitives.WriteSingleLittleEndian(row.AsSpan(28), point.Z);
                }
                digest.AppendData(row);
            }
        }
        var report = new
        {
            Classification = "ACTUAL_ASSETS_WITH_CONDITIONAL_STATIC_CONTEXT_NOT_NATIVE_ORACLE",
            Cells = 65536, Queries = 262144, OddSelectorCells = oddCells, UnexpectedSupportFailures = unexpected,
            Counts = counts, FailureExamples = failures, ResultDigestSha256 = Convert.ToHexStringLower(digest.GetHashAndReset()),
            ElapsedMilliseconds = timer.ElapsedMilliseconds, AllocatedManagedBytesOnAuditThread = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
            IsPeakOrProcessMemoryMeasurement = false,
            Cosine = admitted.Receipt, Sources = sources, BoundsAreMathematicalAssertions = true,
            ActualFieldSceneUnknown = true, NativeExecution = false, RuntimeVerified = false, ActorClearanceVerified = false, ActivationReady = false
        };
        File.WriteAllText(output!, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(74, oddCells); Assert.Equal(0, unexpected);
        Assert.Equal(74, counts.GetValueOrDefault("GroundProjection/UnsupportedDynamicScene/unavailable"));
        Assert.Equal(74, counts.GetValueOrDefault("PredictionProjection/UnsupportedDynamicScene/unavailable"));
    }
}
