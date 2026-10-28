using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.GameServer.Retail;

// Publication accompanies the ordinary normal consumer and its attack hooks.
// Optional full-pilot source checks extend this validation without navigation.
internal static partial class RetailNpcStatCatalog
{
    static partial void ValidateAdditionalHostIdentity(Member member);

    static partial void ValidateRuntimePublication(Package package, ref bool ready)
    {
        if (package.CombatPve is null) throw new InvalidDataException("Statistics combat requires the selected map/NPC PvE source inputs");
        foreach (int map in package.Maps)
            _ = package.CombatPve.PlayerBase(map,Race.ASMODIANS,physical:true,attack:true);
        foreach (var member in package.Profiles.Values)
        {
            var host = DataManager.NPC_DATA.GetNpcTemplate(member.Profile.NpcId)
                ?? throw new InvalidDataException("Missing supported NPC template: " + member.Profile.NpcId);
            if (RetailNpcHostIdentity.Template(host) != member.HostTemplateSha256
                || host.GetTribe() is TribeClass.PET or TribeClass.PET_DARK
                && !(member.MapId == 220050000 && RetailNpcStatSourceExceptions.IsUnownedRiftGenerator(host,member.Profile.SourceRecordSha256)))
                throw new InvalidDataException("Retail statistics host identity/owner-family drift: " + member.Profile.NpcId);
            ValidateAdditionalHostIdentity(member);
        }
        ready = true;
    }
}
