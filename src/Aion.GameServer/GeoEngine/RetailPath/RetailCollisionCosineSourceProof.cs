namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Mathematical assumptions, not observations of actual MXCSR or instruction availability.
/// Unknown is null; none is filled from the host environment.
/// </summary>
public sealed record RetailCollisionCosineAssumptions(bool? NearestEven, bool? MaskedFloatingExceptions,
    bool? ConsumedInstructionsAvailable, bool? NormalReturn);
public sealed record RetailCollisionCosineSourceEvidence(string ProofSha256, string IndependentReviewSha256,
    RetailCollisionCosineAssumptions Assumptions, string Evidence);
public sealed record RetailCollisionCosineAdmission(RetailCollisionStatus Status,
    RetailCollisionCosineReceipt? Receipt, string? Detail)
{
    public bool Success => Status == RetailCollisionStatus.Computed && Receipt is not null;
    public bool RuntimeVerified => false;
}

/// <summary>
/// Admission of one independently reviewed, PE-bound, exact-input source proof.
/// This never obtains a runtime return, invokes cosine, reads CPU state, supplies a
/// missing receipt or enables a scene. Both zero/nonzero native dispatch branches
/// produce the same low binary32 bits under the explicit mathematical conditions.
/// External phase4/cosine-constant-proof and cosine-proof-review retain full traces.
/// </summary>
public static class RetailCollisionCosineSourceProof
{
    public const string ProofSha256 = "81bccc68916c8f0a3f03597a752e7769af7dd37614aad6cac31bf11da60233ca";
    public const string IndependentReviewSha256 = "e575ca83990fb355c65ff552117b1f4a677c720c0c13c5bfa67c89c9fcf0399f";
    public const uint ProvenResultBits = 0x3f3504f3;

    public static RetailCollisionCosineAdmission Admit(RetailCollisionCosineSourceEvidence evidence, string revision)
    {
        if (!EvidenceMatches(evidence) || string.IsNullOrWhiteSpace(revision) || revision.Length > 512)
            return new(RetailCollisionStatus.MissingCosineEvidence, null,
                "Exact proof/review and every explicit conditional assumption are required.");
        return new(RetailCollisionStatus.Computed, new(true, RetailGroundPathSearch.SupportedNpcBinarySha256,
            RetailCollisionMath.NativeCosineFunction, RetailCollisionMath.NativeCosineArgumentBits,
            ProvenResultBits, revision, ProofSha256, false) { ConditionalSourceProof = evidence }, null);
    }

    internal static bool CanConsume(RetailCollisionCosineReceipt receipt) => receipt.ConditionalSourceProof is null
        || !receipt.SyntheticFixture && receipt.CaptureSha256 == ProofSha256
            && receipt.ArgumentBits == RetailCollisionMath.NativeCosineArgumentBits
            && receipt.ResultBits == ProvenResultBits && EvidenceMatches(receipt.ConditionalSourceProof);

    private static bool EvidenceMatches(RetailCollisionCosineSourceEvidence? evidence) => evidence is not null
        && evidence.ProofSha256 == ProofSha256 && evidence.IndependentReviewSha256 == IndependentReviewSha256
        && evidence.Assumptions is { NearestEven: true, MaskedFloatingExceptions: true,
            ConsumedInstructionsAvailable: true, NormalReturn: true }
        && !string.IsNullOrWhiteSpace(evidence.Evidence) && evidence.Evidence.Length <= 4096;
}
