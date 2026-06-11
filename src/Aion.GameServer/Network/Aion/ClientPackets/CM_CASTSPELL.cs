using System;
using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CASTSPELL (alexa026, rhys2002). Player casts a skill at object/point/area target; validates death, pet-order, passive, and skill-cooldown timing. DataManager/SkillTemplate/AuditLogger red-tolerated.</summary>
public class CM_CASTSPELL : AionClientPacket
{
    private readonly long receiveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private int spellid;
    // 0 - obj id, 1 - point location, 2 - unk, 3 - object not in sight(skill 1606)? 4 - unk
    private int targetType;
    private float x, y, z;

    private int targetObjectId;
    private int hitTime;
    private int level;
    private int unk;

    public CM_CASTSPELL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        spellid = ReadUH();
        level = ReadUC();

        targetType = ReadUC();

        switch (targetType)
        {
            case 0:
            case 3:
            case 4:
                targetObjectId = ReadD();
                break;
            case 1:
                x = ReadF();
                y = ReadF();
                z = ReadF();
                break;
            case 2:
                x = ReadF();
                y = ReadF();
                z = ReadF();
                ReadF();// unk1
                ReadF();// unk2
                ReadF();// unk3
                ReadF();// unk4
                ReadF();// unk5
                ReadF();// unk6
                ReadF();// unk7
                ReadF();// unk8
                break;
        }

        hitTime = ReadUH();
        unk = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        if (player.IsDead())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST(ChatUtil.L10n(1400059)));
            return;
        }

        if (spellid == 0)
        {
            player.GetController().CancelCurrentSkill(null);
            return;
        }
        if (DataManager.PET_SKILL_DATA.IsPetOrderSkill(spellid) && (player.GetSummon() == null || !player.GetSummon().IsPet()))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_SKILL_NOT_NEED_PET());
            return;
        }

        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(spellid);
        if (template == null || template.IsPassive())
            return;

        if (player.IsProtectionActive())
            player.GetController().StopProtectionActiveTask();
        player.GetController().CancelUseItem();

        if (player.GetNextSkillUse() > receiveTime)
        {
            int lastSkillId = player.GetLastSkill().GetSkillId(); // lastSkill cannot be null, as nextSkillUse is zero on the first cast
            AuditLogger.Log(player, "tried to use skill " + spellid + " " + (player.GetNextSkillUse() - receiveTime) + " ms too early. Previous skill: " + lastSkillId);
            if (player.GetNextSkillUse() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                SendPacket(SM_SYSTEM_MESSAGE.STR_SKILL_NOT_READY());
                return;
            }
        }

        player.GetController().UseSkill(template, targetType, x, y, z, hitTime, level);
    }
}
