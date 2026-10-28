using System.Buffers.Binary;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailFieldObjectGeometryTests
{
    [SkippableFact]
    public void InstalledPilotRangesAndBoundsAreComputedUnderExplicitConditionalCosineWithoutLiveState()
    {
        string folder = Environment.GetEnvironmentVariable("AION_RETAIL_COLLISION_FIXTURE_ROOT") ?? @"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying";
        string meshFile = Path.Combine(folder, "MeshMaterial.Dat"), collisionFile = Path.Combine(folder, "Collision.Dat");
        Skip.IfNot(File.Exists(meshFile) && File.Exists(collisionFile), "Pinned pilot pair is absent; no scene/current state is invented.");
        byte[] mesh = File.ReadAllBytes(meshFile), collision = File.ReadAllBytes(collisionFile);
        Assert.Equal("e964645652e44f064ebb84e0588677d36bbf4be472db88a5bd5af30057355977", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(mesh)).ToLowerInvariant());
        Assert.Equal("e5dd2cacc876c03f939fee77610db592e77d8b0322340434f7f961f2d49b7ffc", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(collision)).ToLowerInvariant());
        var raw = CollisionFixture.Read(mesh, collision, 512, 512);
        var admission = RetailCollisionCosineSourceProof.Admit(new(RetailCollisionCosineSourceProof.ProofSha256,
            RetailCollisionCosineSourceProof.IndependentReviewSha256, new(true, true, true, true),
            "Conditional mathematical fixture; no native return, MXCSR or live instance observed"), "field-range-source-fixture");
        Assert.True(admission.Success);
        var preparation = RetailCollisionMath.Prepare(raw, admission.Receipt, CollisionFixture.PrepareLimits);
        Assert.True(preparation.Success, preparation.Detail);
        uint[] starts = [80, 40, 0];
        double[] minX = [372.0821838378906, 457.4125061035156, 222.5128631591797];
        double[] maxZ = [249.03099060058594, 392.2826232910156, 571.0444946289062];
        for (int i = 0; i < 3; i++)
        {
            var observation = FieldGeometryFixture.Observation(null) with { EditorId = (uint)(15 + i),
                WorldIdentity = "320050000-source", EvidenceKind = RetailFieldObjectEvidenceKind.SourceInitializationDeclaration };
            var result = RetailFieldObjectGeometry.Prepare(preparation.Data!, observation, new(40, 552));
            Assert.True(result.Success, result.Detail); var field = result.Geometry!;
            Assert.Equal(starts[i], field.FirstTriangle); Assert.Equal(starts[i] + 39, field.LastTriangle); Assert.Equal(40, field.FaceCount);
            Assert.Equal(minX[i], (double)field.Minimum.X); Assert.Equal(maxZ[i], (double)field.Maximum.Z);
            Assert.Null(field.Observation.RawState); Assert.False(field.CurrentStateObservedByThisUnit);
            Assert.False(field.RuntimeVerified); Assert.Equal("ConditionalStaticSourceProof", field.Prepared.Cosine!.Origin);
        }
    }

    [Fact]
    public void ActualClassReceiptSelectsFirstCatalogRangeAndIgnoresNamesAndSecondPair()
    {
        var raw = FieldGeometryFixture.Raw([CollisionFixture.Face(2), CollisionFixture.Face(4)],
            [new(15, "misleading-label", "not-a-class", FieldGeometryFixture.Payload(1, 1, 0, uint.MaxValue)),
             new(15, "PlaceableObject", "duplicate", FieldGeometryFixture.Payload(0, 0))]);
        var field = FieldGeometryFixture.Prepare(CollisionFixture.Prepared(raw));
        Assert.Equal(1u, field.FirstTriangle); Assert.Equal(1u, field.LastTriangle); Assert.Equal(1, field.FaceCount);
        Assert.Equal(new(0, 0, 4), field.Minimum); Assert.Equal(new(2, 2, 4), field.Maximum);
        Assert.Equal((byte)1, field.EligibilityBytes.Span[0]);
        Assert.Equal("misleading-label", System.Text.Encoding.UTF8.GetString(field.Catalog.RawKind.Span));
        Assert.False(field.RuntimeVerified); Assert.False(field.CurrentStateObservedByThisUnit);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(-1, 1)] [InlineData(int.MinValue, 1)]
    public void NativeRawNonzeroStateEnablesComputedGeometry(int state, uint expected)
    {
        var scene = FieldGeometryFixture.Scene(state: state);
        var result = RetailCollisionQuery.FirstContact(scene, scene.Stamp, new(.5f, .5f, 2), new(.5f, .5f, -2), 0, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed, result.Status); Assert.Equal(expected, result.NativeResult);
        if (expected == 1) Assert.Equal(new RetailSearchPoint(.5f, .5f, 0), result.Point);
        else Assert.Null(result.Point);
        Assert.Single(result.ConsumedFieldObjects);
    }

    [Fact]
    public void WindingEligibilityAndPredictionFlagRemainDifferentComputedQueries()
    {
        var raw = FieldGeometryFixture.Raw([CollisionFixture.Face(5, reversed: true)]);
        var prepared = CollisionFixture.Prepared(raw); var field = FieldGeometryFixture.Prepare(prepared);
        Assert.Equal((byte)0, field.EligibilityBytes.Span[0]);
        var scene = FieldGeometryFixture.Scene(prepared, [field]);
        var input = new RetailSearchPoint(.5f, .5f, 5);
        var prediction = RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, input, CollisionFixture.QueryLimits);
        var ground = RetailCollisionQuery.ProjectGround(scene, scene.Stamp, input, CollisionFixture.QueryLimits);
        Assert.Equal(1u, prediction.NativeResult); Assert.Equal(5f, prediction.Point!.Value.Z);
        Assert.Equal(0u, ground.NativeResult); Assert.Null(ground.Point);
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(5, 0)]
    public void ActiveFieldSegmentsConsumeActualMaterialColumn(int view, uint expected)
    {
        var scene = FieldGeometryFixture.Scene();
        var result = RetailCollisionQuery.SegmentBlocked(scene, scene.Stamp, new(.5f, .5f, 2), new(.5f, .5f, -2), view, 0, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed, result.Status); Assert.Equal(expected, result.NativeResult);
    }

    [Fact]
    public void FieldBeforeUnknownReturnsEarlyOnlyForBooleanAndContactDiscardsProvisionalPoint()
    {
        var prepared = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face()]));
        var field = FieldGeometryFixture.Prepare(prepared);
        RetailCollisionSceneObject[] objects = [new(15, RetailFieldObjectGeometry.SupportedClass, "object1"), new(77, "unknown", "unknown1")];
        var scene = FieldGeometryFixture.Scene(prepared, [field], objects);
        var a = new RetailSearchPoint(.5f, .5f, 2); var b = new RetailSearchPoint(.5f, .5f, -2);
        var boolean = RetailCollisionQuery.SegmentBlocked(scene, scene.Stamp, a, b, 0, 0, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(1u, boolean.NativeResult); Assert.Equal(1, boolean.SceneObjects);
        var contact = RetailCollisionQuery.FirstContact(scene, scene.Stamp, a, b, 0, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.UnsupportedDynamicScene, contact.Status); Assert.Null(contact.Point); Assert.Null(contact.NativeResult);
        Assert.Equal(2, contact.SceneObjects);
        var ignored = RetailCollisionQuery.FirstContact(scene, scene.Stamp, a, b, 77, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(1u, ignored.NativeResult);
    }

    [Theory]
    [InlineData("class")] [InlineData("constructor")] [InlineData("vtable")] [InlineData("binary")] [InlineData("class-evidence")]
    public void CatalogLabelNeverFillsUnknownClassFactoryOrVtable(string fault)
    {
        var prepared = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face()]));
        var observation = FieldGeometryFixture.Observation(); var cls = observation.Class;
        cls = fault switch
        {
            "class" => cls with { ClassName = "PlaceableObject" }, "constructor" => cls with { Constructor = 0 },
            "vtable" => cls with { Vtable = 0x140493550 }, "binary" => cls with { NativeNpcSha256 = new('0', 64) },
            _ => cls with { EvidenceSha256 = "" }
        };
        var result = RetailFieldObjectGeometry.Prepare(prepared, observation with { Class = cls }, new(1000, 100000));
        Assert.Equal(RetailCollisionStatus.UnsupportedDynamicScene, result.Status); Assert.Null(result.Geometry);
    }

    [Theory]
    [InlineData(4, 0, 0)] [InlineData(16, 1, 0)] [InlineData(16, 0, 2)]
    public void TruncatedReversedAndOutOfMeshRangesCannotPublishGeometry(int bytes, uint first, uint last)
    {
        var payload = FieldGeometryFixture.Payload(first, last)[..bytes];
        var raw = FieldGeometryFixture.Raw([CollisionFixture.Face()], [new(15, "PlaceableObject", "field", payload)]);
        var result = RetailFieldObjectGeometry.Prepare(CollisionFixture.Prepared(raw), FieldGeometryFixture.Observation(), new(1000, 100000));
        Assert.Equal(RetailCollisionStatus.UnsupportedDynamicScene, result.Status); Assert.Null(result.Geometry);
    }

    [Fact]
    public void MissingCosineCannotPretendEligibilityStorageExistsEvenForInactiveObject()
    {
        var raw = FieldGeometryFixture.Raw([CollisionFixture.Face()]);
        var prepared = RetailCollisionMath.Prepare(raw, null, CollisionFixture.PrepareLimits).Data!;
        var result = RetailFieldObjectGeometry.Prepare(prepared, FieldGeometryFixture.Observation(0), new(1000, 100000));
        Assert.Equal(RetailCollisionStatus.MissingCosineEvidence, result.Status); Assert.Null(result.Geometry);
        Assert.True(result.CosineEvidenceConsumed);
        var classified = FieldGeometryFixture.Raw([CollisionFixture.Face(material: 1)]);
        var accepted = RetailFieldObjectGeometry.Prepare(RetailCollisionMath.Prepare(classified, null, CollisionFixture.PrepareLimits).Data!,
            FieldGeometryFixture.Observation(), new(1000, 100000));
        Assert.True(accepted.Success); Assert.False(accepted.CosineEvidenceConsumed); Assert.Equal((byte)0, accepted.Geometry!.EligibilityBytes.Span[0]);
    }

    [Theory]
    [InlineData("state")] [InlineData("object")] [InlineData("scene")] [InlineData("instance")] [InlineData("asset")]
    public void ConsumedUnknownOrStaleStateDoesNotBecomeNoHit(string fault)
    {
        var prepared = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face()]));
        var observation = FieldGeometryFixture.Observation();
        observation = fault switch
        {
            "state" => observation with { RawState = null }, "object" => observation with { ObjectRevision = "changed" },
            "scene" => observation with { SceneRevision = "changed" }, "instance" => observation with { InstanceIdentity = "changed" },
            _ => observation with { AssetRevision = "changed" }
        };
        var field = FieldGeometryFixture.Prepare(prepared, observation);
        var scene = FieldGeometryFixture.Scene(prepared, [field]);
        var result = RetailCollisionQuery.FirstContact(scene, scene.Stamp, new(.5f, .5f, 1), new(.5f, .5f, -1), 0, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(fault == "state" ? RetailCollisionStatus.UnsupportedDynamicScene : RetailCollisionStatus.StaleSource, result.Status);
        Assert.Null(result.Point); Assert.Null(result.NativeResult);
    }

    [Fact]
    public void ProjectionBoundPrunePrecedesUnknownStateButSegmentReadsStateFirst()
    {
        var prepared = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face(-2)]));
        var field = FieldGeometryFixture.Prepare(prepared, FieldGeometryFixture.Observation(null));
        var scene = FieldGeometryFixture.Scene(prepared, [field], selector: 1); // terrain z0 beats all object bounds
        var projection = RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, new(.5f, .5f, 0), CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed, projection.Status); Assert.Equal(0f, projection.Point!.Value.Z);
        var segment = RetailCollisionQuery.FirstContact(scene, scene.Stamp, new(.5f, .5f, 5), new(.5f, .5f, 6), 0, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.UnsupportedDynamicScene, segment.Status);
    }

    [Theory]
    [InlineData("faces")] [InlineData("bytes")] [InlineData("query-triangles")] [InlineData("query-objects")]
    public void FieldBudgetsNeverPublishProvisionalNativeSuccess(string fault)
    {
        var prepared = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face(), CollisionFixture.Face(2)]));
        if (fault is "faces" or "bytes")
        {
            var result = RetailFieldObjectGeometry.Prepare(prepared, FieldGeometryFixture.Observation(), new(fault == "faces" ? 1 : 1000, fault == "bytes" ? 512 : 100000));
            Assert.Equal(RetailCollisionStatus.DefensiveLimit, result.Status); Assert.Null(result.Geometry);
        }
        else
        {
            var scene = FieldGeometryFixture.Scene(prepared, [FieldGeometryFixture.Prepare(prepared)]);
            var limits = fault == "query-triangles" ? CollisionFixture.QueryLimits with { MaxTriangles = 0 } : CollisionFixture.QueryLimits with { MaxSceneObjects = 0 };
            var result = RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, new(.5f, .5f, 3), limits);
            Assert.Equal(RetailCollisionStatus.DefensiveLimit, result.Status); Assert.Null(result.NativeResult); Assert.Null(result.Point);
        }
    }

    [Fact]
    public void OwnedGeometryFromAnotherPreparedAssetAndDuplicateBindingAreRejected()
    {
        var prepared = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face()]));
        var other = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face(5)]));
        var field = FieldGeometryFixture.Prepare(other);
        Assert.Equal(RetailCollisionStatus.SourceIdentityMismatch, FieldGeometryFixture.CreateScene(prepared, [field]).Status);
        field = FieldGeometryFixture.Prepare(prepared);
        Assert.Equal(RetailCollisionStatus.InvalidInput, FieldGeometryFixture.CreateScene(prepared, [field, field]).Status);
    }

    [Theory]
    [InlineData(11, true)] [InlineData(13, true)] [InlineData(16, true)] [InlineData(135, true)] [InlineData(136, true)]
    [InlineData(1, false)] [InlineData(25, false)]
    public void NativeSpecialMaterialWorldFlagIsReportedWithoutWritingAWorld(byte material, bool expected)
    {
        var field = FieldGeometryFixture.Prepare(CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face(material: material)])));
        Assert.Equal(expected, field.SetsWorldSpecialMaterialFlag);
        if (expected) Assert.Equal((byte)0, field.EligibilityBytes.Span[0]);
    }
    [Fact]
    public void NegativeWorldVerticesInitializeBoundsFromTheFirstActualVertex()
    {
        CollisionFace face = new(new(-4, -3, -2), new(-1, -3, -2), new(-4, -1, -2), 25);
        var field = FieldGeometryFixture.Prepare(CollisionFixture.Prepared(FieldGeometryFixture.Raw([face])));
        Assert.Equal(new(-4, -3, -2), field.Minimum); Assert.Equal(new(-1, -1, -2), field.Maximum);
        Assert.Equal(-2f - .001f, field.MinimumPruneZ); Assert.Equal(-2f + .001f, field.MaximumPruneZ);
    }

    [Theory]
    [InlineData(false, -.05f)] [InlineData(true, .05f)]
    public void EqualScoresAcrossSupportedObjectsRetainFirstSurfaceInActualListOrder(bool reversed, float expectedZ)
    {
        // query0: both +/-binary32(.05) score exactlybinary32(.05), with no above-.1 penalty.
        var scene = Pair(-.05f, .05f, reversed);
        var result = RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, new(.5f, .5f, 0), CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed, result.Status); Assert.Equal(expectedZ, result.Point!.Value.Z);
        Assert.Equal(reversed ? new uint[] { 16, 15 } : [15, 16], result.ConsumedFieldObjects.Select(o => o.EditorId));
        Assert.Equal(2, result.Triangles);
    }
    [Theory]
    [InlineData(-.05f, .05f)] [InlineData(.05f, -.05f)]
    public void EqualStaticAndDynamicProjectionScoresRetainEarlierStaticSurface(float staticZ, float dynamicZ)
    {
        var scene = Pair(staticZ, dynamicZ, staticFirst: true);
        var result = RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, new(.5f, .5f, 0), CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed, result.Status); Assert.Equal(staticZ, result.Point!.Value.Z);
        Assert.Single(result.ConsumedFieldObjects); Assert.Equal(2, result.Triangles);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EqualContactPointsConsumeSupportedObjectsInDeclaredOrder(bool reversed)
    {
        var scene = Pair(0, 0, reversed);
        var result = RetailCollisionQuery.FirstContact(scene, scene.Stamp, new(.5f, .5f, 1), new(.5f, .5f, -1), 0, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed, result.Status); Assert.Equal(new RetailSearchPoint(.5f, .5f, 0), result.Point);
        Assert.Equal(reversed ? new uint[] { 16, 15 } : [15, 16], result.ConsumedFieldObjects.Select(o => o.EditorId));
        Assert.Equal(2, result.Triangles);
        // Equal exact segment distance implies the same point here. There is no fabricated winner-object ID.
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LaterUnknownObjectDiscardsComputedProjectionForBothNativeFlags(bool ground)
    {
        var prepared = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face()]));
        var field = FieldGeometryFixture.Prepare(prepared);
        var scene = FieldGeometryFixture.Scene(prepared, [field],
            [new(15, RetailFieldObjectGeometry.SupportedClass, "object1"), new(77, "unknown", "unknown1")]);
        var result = ground ? RetailCollisionQuery.ProjectGround(scene, scene.Stamp, new(.5f, .5f, 1), CollisionFixture.QueryLimits)
            : RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, new(.5f, .5f, 1), CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.UnsupportedDynamicScene, result.Status); Assert.Null(result.NativeResult); Assert.Null(result.Point);
        Assert.Contains(result.Trace, t => t.Kind == RetailCollisionTraceKind.MeshTriangle && t.TriangleId == 0);
        Assert.Equal(2, result.SceneObjects); Assert.Single(result.ConsumedFieldObjects);
    }
    [Theory]
    [InlineData(0x3dcccccdu, .1f)] [InlineData(0x3dccccceu, -1f)]
    public void DynamicAboveSurfacePenaltyStartsStrictlyAfterPointOne(uint surfaceBits, float expectedZ)
    {
        var scene = Pair(-1, BitConverter.UInt32BitsToSingle(surfaceBits));
        var result = RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, new(.5f, .5f, 0), CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed, result.Status); Assert.Equal(expectedZ, result.Point!.Value.Z);
    }
    [Theory]
    [InlineData(0x3a83126fu, 1, false)] [InlineData(0x3a831270u, 0, false)]
    [InlineData(0xba83126fu, 1, false)] [InlineData(0xba831270u, 0, false)]
    [InlineData(0x3a83126fu, 1, true)] [InlineData(0x3a831270u, 0, true)]
    [InlineData(0xba83126fu, 1, true)] [InlineData(0xba831270u, 0, true)]
    public void SegmentPruneIncludesExactPaddedZBoundAndSkipsItsNextOutsideWord(uint zBits, int triangles, bool contact)
    {
        var scene = FieldGeometryFixture.Scene(); float z = BitConverter.UInt32BitsToSingle(zBits);
        var a = new RetailSearchPoint(.5f, .5f, z); var b = new RetailSearchPoint(.75f, .5f, z);
        var result = contact ? RetailCollisionQuery.FirstContact(scene, scene.Stamp, a, b, 0, .001f, CollisionFixture.QueryLimits)
            : RetailCollisionQuery.SegmentBlocked(scene, scene.Stamp, a, b, 0, 0, .001f, CollisionFixture.QueryLimits);
        Assert.Equal(RetailCollisionStatus.Computed, result.Status); Assert.Equal(0u, result.NativeResult);
        Assert.Equal(triangles, result.Triangles); // horizontal ray is parallel; count distinguishes exact bound from prune
    }
    [Theory]
    [InlineData(0xba83126fu, 0x00000000u, RetailCollisionStatus.Computed)]
    [InlineData(0xba83126eu, 0x2f000000u, RetailCollisionStatus.UnsupportedDynamicScene)]
    public void ProjectionPruneEqualityCanSkipStateReadButNextRepresentableSurfaceCannot(uint surfaceBits, uint maxBits, RetailCollisionStatus expected)
    {
        var prepared = CollisionFixture.Prepared(FieldGeometryFixture.Raw([CollisionFixture.Face(BitConverter.UInt32BitsToSingle(surfaceBits))]));
        var field = FieldGeometryFixture.Prepare(prepared, FieldGeometryFixture.Observation(null));
        Assert.Equal(maxBits, BitConverter.SingleToUInt32Bits(field.MaximumPruneZ));
        var scene = FieldGeometryFixture.Scene(prepared, [field], selector: 1); // existing terrain score0
        var result = RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, new(.5f, .5f, 0), CollisionFixture.QueryLimits);
        Assert.Equal(expected, result.Status);
        if (expected == RetailCollisionStatus.Computed) Assert.Equal(0f, result.Point!.Value.Z);
        else { Assert.Null(result.NativeResult); Assert.Null(result.Point); }
    }
    private static RetailCollisionScene Pair(float firstZ, float secondZ, bool reversed = false, bool staticFirst = false)
    {
        CollisionFace[] faces = [CollisionFixture.Face(firstZ), CollisionFixture.Face(secondZ)];
        CollisionField[] fields = staticFirst ? [new(15, "PlaceableObject", "dynamic", FieldGeometryFixture.Payload(1, 1))]
            : [new(15, "PlaceableObject", "first", FieldGeometryFixture.Payload(0, 0)), new(16, "PlaceableObject", "second", FieldGeometryFixture.Payload(1, 1))];
        var raw = CollisionFixture.Read(CollisionFixture.Mesh(faces, fields), staticFirst ? CollisionFixture.Collision((_, _) => [new(0, 0)]) : CollisionFixture.Collision());
        var prepared = CollisionFixture.Prepared(raw); var first = FieldGeometryFixture.Prepare(prepared);
        if (staticFirst) return FieldGeometryFixture.Scene(prepared, [first], selector: 15);
        var second = FieldGeometryFixture.Prepare(prepared, FieldGeometryFixture.Observation() with { EditorId = 16, ObjectRevision = "object2" });
        RetailCollisionSceneObject a = new(15, RetailFieldObjectGeometry.SupportedClass, "object1"), b = new(16, RetailFieldObjectGeometry.SupportedClass, "object2");
        return FieldGeometryFixture.Scene(prepared, [first, second], reversed ? [b, a] : [a, b]);
    }
}

internal static class FieldGeometryFixture
{
    internal static byte[] Payload(uint first, uint last, uint discarded1 = uint.MaxValue, uint discarded2 = uint.MaxValue)
    {
        byte[] bytes = new byte[16]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, first); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), last);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), discarded1); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), discarded2); return bytes;
    }
    internal static RetailCollisionData Raw(IReadOnlyList<CollisionFace> faces, IReadOnlyList<CollisionField>? fields = null) =>
        CollisionFixture.Read(CollisionFixture.Mesh(faces, fields ?? [new(15, "PlaceableObject", "field", Payload(0, (uint)faces.Count - 1))]), CollisionFixture.Collision());
    internal static RetailFieldObjectObservation Observation(int? state = 1) => new(15, "object1", state,
        "synthetic-world", "instance1", "scene1", "asset1", "synthetic-mesh-owner", "owner-revision1", new('f', 64),
        RetailFieldObjectEvidenceKind.SyntheticFixture, new(RetailFieldObjectGeometry.SupportedClass,
            RetailFieldObjectGeometry.SupportedConstructor, RetailFieldObjectGeometry.SupportedVtable,
            RetailGroundPathSearch.SupportedNpcBinarySha256, new('a', 64)));
    internal static RetailFieldObjectGeometry Prepare(RetailCollisionPreparedData prepared, RetailFieldObjectObservation? observation = null)
    {
        var result = RetailFieldObjectGeometry.Prepare(prepared, observation ?? Observation(), new(10000, 1000000));
        Assert.True(result.Success, result.Detail); return result.Geometry!;
    }
    internal static RetailCollisionScene Scene(int? state = 1) => Scene(CollisionFixture.Prepared(Raw([CollisionFixture.Face()])), state: state);
    private static RetailCollisionScene Scene(RetailCollisionPreparedData prepared, int? state) => Scene(prepared, [Prepare(prepared, Observation(state))]);
    internal static RetailCollisionScene Scene(RetailCollisionPreparedData prepared, IReadOnlyList<RetailFieldObjectGeometry> fields,
        IReadOnlyList<RetailCollisionSceneObject>? objects = null, byte selector = 13)
    { var result = CreateScene(prepared, fields, objects, selector); Assert.True(result.Success, result.Detail); return result.Scene!; }
    internal static RetailCollisionSceneResult CreateScene(RetailCollisionPreparedData prepared, IReadOnlyList<RetailFieldObjectGeometry> fields,
        IReadOnlyList<RetailCollisionSceneObject>? objects = null, byte selector = 13)
    {
        var terrain = CollisionFixture.Terrain((_, _) => selector);
        objects ??= [new(15, RetailFieldObjectGeometry.SupportedClass, "object1")];
        var regions = CollisionFixture.Regions().Select(r => r with { OrderedObjects = objects }).ToArray();
        return RetailCollisionScene.Create(prepared, terrain, CollisionFixture.Sources(prepared, terrain), new(new(0, 0, -10240), new(64, 64, 10240)),
            new("synthetic-world", "instance1", "scene1", "asset1", new('c', 64), "synthetic-owned-field-scene", true),
            regions, RetailCollisionMaterialPolicy.FromPinnedFileImage("synthetic-explicit-material"), new(1000, 10000), fields);
    }
}
