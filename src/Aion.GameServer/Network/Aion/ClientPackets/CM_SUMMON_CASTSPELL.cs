using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SUMMON_CASTSPELL (ATracer, KID). Summon/mercenary skill cast w/ pet vs mercenary handling + skill-order validation. SkillOrder/DataManager.PET_SKILL_DATA red-tolerated.</summary>
public class CM_SUMMON_CASTSPELL : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_SUMMON_CASTSPELL));
    private int summonObjId;
    private int targetObjId;
    private int skillId;
    private int skillLvl;
    private int unk; // probably related to release

    public CM_SUMMON_CASTSPELL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        summonObjId = ReadD();
        skillId = ReadUH();
        skillLvl = ReadUC();
        targetObjId = ReadD();
        unk = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        Creature summonOrMercenary = player.GetSummonOrMercenary(summonObjId);
        if (summonOrMercenary == null || summonOrMercenary is Summon summon0 && !summon0.IsPet())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_SKILL_NOT_NEED_PET());
            return;
        }

        Creature target;
        if (targetObjId != summonOrMercenary.GetObjectId())
        {
            VisibleObject obj = summonOrMercenary.GetKnownList().GetObject(targetObjId);
            if (obj is Creature)
            {
                target = (Creature)obj;
            }
            else
            { // null or not a creature (attack should be client restricted)
                if (obj != null) // may be null due to lags while the target runs out of sight
                    AuditLogger.Log(player, "tried to cast a summon spell on a wrong target: " + obj);
                return;
            }
        }
        else
        {
            target = summonOrMercenary;
        }

        if (summonOrMercenary is Summon summon)
        {
            SkillOrder order = summon.RetrieveNextSkillOrder();
            if (order != null && order.GetTarget().Equals(target))
            {
                if (order.GetSkillId() != skillId || order.GetSkillLevel() != skillLvl)
                    log.LogWarning(player + " used summon order with a different skill: skillId {SkillId}->{OrderSkillId}; skillLvl {SkillLvl}->{OrderSkillLvl}.", skillId, order.GetSkillId(), skillLvl,
                        order.GetSkillLevel());
                summon.GetController().UseSkill(order);
            }
        }
        else
        {
            summonOrMercenary.SetTarget(target);
            if (DataManager.PET_SKILL_DATA.PetHasSkill(summonOrMercenary.GetObjectTemplate().GetTemplateId(), skillId))
                summonOrMercenary.GetController().UseSkill(skillId, skillLvl);
            else
                AuditLogger.Log(player, "tried to use invalid mercenary skill " + skillId);
        }
    }
}
