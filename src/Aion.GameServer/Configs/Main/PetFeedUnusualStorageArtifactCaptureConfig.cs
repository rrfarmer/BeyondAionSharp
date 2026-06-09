namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/PetFeedUnusualStorageArtifactCaptureConfig.</summary>
public static class PetFeedUnusualStorageArtifactCaptureConfig
{
    /// <summary>Property key: gameserver.petfeed.unusual_storage_artifacts.enabled</summary>
    public static bool ENABLED = false;

    /// <summary>Property key: gameserver.petfeed.unusual_storage_artifacts.output_dir</summary>
    public static string OUTPUT_DIR = "./parity-artifacts/pet-feed-unusual-storage/java";

    /// <summary>Property key: gameserver.petfeed.unusual_storage_artifacts.max_pending_contexts_per_player</summary>
    public static int MAX_PENDING_CONTEXTS_PER_PLAYER = 4;

    /// <summary>Property key: gameserver.petfeed.unusual_storage_artifacts.max_queued_artifacts</summary>
    public static int MAX_QUEUED_ARTIFACTS = 32;

    /// <summary>Property key: gameserver.petfeed.unusual_storage_artifacts.allowed_scenario</summary>
    public static string ALLOWED_SCENARIO = "pet_feed_unusual_storage";
}
