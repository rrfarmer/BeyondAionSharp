using System.Collections.Generic;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ABYSS_ARTIFACT_INFO3. Sends abyss artifact status (locationId*10+1 + status). ArtifactLocation/SiegeService red-tolerated.</summary>
public class SM_ABYSS_ARTIFACT_INFO3 : AionServerPacket
{
    private ICollection<ArtifactLocation> locations;

    public SM_ABYSS_ARTIFACT_INFO3(ICollection<ArtifactLocation> collection)
    {
        this.locations = collection;
    }

    public SM_ABYSS_ARTIFACT_INFO3(int loc)
    {
        locations = new List<ArtifactLocation>();
        locations.Add(SiegeService.GetInstance().GetArtifact(loc));
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(locations.Count);
        foreach (ArtifactLocation artifact in locations)
        {
            WriteD(artifact.GetLocationId() * 10 + 1);
            WriteC(artifact.GetStatus().GetValue());
            WriteD(0);
        }
    }
}
