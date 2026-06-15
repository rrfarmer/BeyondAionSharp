using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Npcshout;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/SpeakerAI (Rolandas).</summary>
[AIName("speaker")]
public class SpeakerAI : GeneralNpcAI
{
    public SpeakerAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnPatternShout(ShoutEventType @event, string pattern, int skillNumber)
    {
        if (GetOwner().GetWorldId() != 210050000 && GetOwner().GetWorldId() != 220070000)
            return false;
        if (@event != ShoutEventType.IDLE || pattern == null)
            return false;

        SiegeService srv = SiegeService.GetInstance();

        Race npcRace = GetOwner().GetRace();
        if (npcRace == Race.ASMODIANS)
        {
            if ("1".Equals(pattern))
            {
                // TODO: find if Dredgion Ship is spawned
            }
            else if ("2".Equals(pattern))
            {
                return srv.IsSiegeInProgress(1142) && srv.GetSiegeLocation(1142).GetRace() == SiegeRace.ASMODIANS
                    || srv.IsSiegeInProgress(1143) && srv.GetSiegeLocation(1143).GetRace() == SiegeRace.ASMODIANS
                    || srv.IsSiegeInProgress(1144) && srv.GetSiegeLocation(1144).GetRace() == SiegeRace.ASMODIANS
                    || srv.IsSiegeInProgress(1145) && srv.GetSiegeLocation(1145).GetRace() == SiegeRace.ASMODIANS;
            }
            else if ("3".Equals(pattern))
            {
                return srv.IsSiegeInProgress(1132) && srv.GetSiegeLocation(1132).GetRace() == SiegeRace.ASMODIANS
                    || srv.IsSiegeInProgress(1251) && srv.GetSiegeLocation(1251).GetRace() == SiegeRace.ASMODIANS;
            }
            else if ("4".Equals(pattern))
            {
                return srv.IsSiegeInProgress(1221) && srv.GetSiegeLocation(1221).GetRace() == SiegeRace.BALAUR;
            }
            else if ("5".Equals(pattern))
            {
                return srv.IsSiegeInProgress(1131);
            }
            else if ("6".Equals(pattern))
            {
                return srv.GetSiegeLocation(3011) != null && srv.GetSiegeLocation(3011).GetRace() == SiegeRace.ELYOS && srv.GetSiegeLocation(3021) != null
                    && srv.GetSiegeLocation(3021).GetRace() == SiegeRace.ELYOS;
            }
        }
        else if (npcRace == Race.ELYOS)
        {
            if ("1".Equals(pattern))
            {
                // TODO: find if Dredgion Ship is spawned
            }
            else if ("2".Equals(pattern))
            {
                return srv.IsSiegeInProgress(1142) && srv.GetSiegeLocation(1142).GetRace() == SiegeRace.ELYOS
                    || srv.IsSiegeInProgress(1143) && srv.GetSiegeLocation(1143).GetRace() == SiegeRace.ELYOS
                    || srv.IsSiegeInProgress(1144) && srv.GetSiegeLocation(1144).GetRace() == SiegeRace.ELYOS
                    || srv.IsSiegeInProgress(1145) && srv.GetSiegeLocation(1145).GetRace() == SiegeRace.ELYOS;
            }
            else if ("3".Equals(pattern))
            {
                return srv.IsSiegeInProgress(1141) && srv.GetSiegeLocation(1141).GetRace() == SiegeRace.ELYOS
                    || srv.IsSiegeInProgress(1211) && srv.GetSiegeLocation(1211).GetRace() == SiegeRace.ELYOS;
            }
            else if ("4".Equals(pattern))
            {
                return srv.IsSiegeInProgress(1241) && srv.GetSiegeLocation(1241).GetRace() == SiegeRace.BALAUR;
            }
            else if ("5".Equals(pattern))
            {
                return srv.IsSiegeInProgress(1141);
            }
            else if ("6".Equals(pattern))
            {
                return srv.GetSiegeLocation(2011) != null && srv.GetSiegeLocation(2011).GetRace() == SiegeRace.ASMODIANS && srv.GetSiegeLocation(2021) != null
                    && srv.GetSiegeLocation(2021).GetRace() == SiegeRace.ASMODIANS;
            }
        }

        return false;
    }
}
