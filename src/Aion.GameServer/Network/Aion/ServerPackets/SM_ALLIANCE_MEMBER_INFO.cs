using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ALLIANCE_MEMBER_INFO (Sarynth, Rhys2002). Per-event alliance member update (hp/mp/fp, position, class/gender/level, abnormal effects). PlayerAllianceEvent switch labels qualified; getTargetSlot().ordinal() = declaration position (Array.IndexOf, see [[enum-ordinal-vs-value-trap]]); FULLSLOTS on SkillTargetSlotExtensions. Effect/team/WorldPosition red-tolerated.</summary>
public class SM_ALLIANCE_MEMBER_INFO : AionServerPacket
{
    private Player player;
    private PlayerAllianceEvent eventValue;
    private readonly int allianceId;
    private readonly int objectId;
    private readonly int slot;
    private List<Effect> abnormalEffects;

    public SM_ALLIANCE_MEMBER_INFO(PlayerAllianceMember member, PlayerAllianceEvent eventValue, int slot)
    {
        this.player = member.GetObject();
        this.eventValue = eventValue;
        this.allianceId = member.GetAllianceId();
        this.objectId = member.GetObjectId();
        this.slot = slot;
        switch (eventValue)
        {
            case PlayerAllianceEvent.JOIN:
            case PlayerAllianceEvent.ENTER:
            case PlayerAllianceEvent.ENTER_OFFLINE:
            case PlayerAllianceEvent.UPDATE:
            case PlayerAllianceEvent.RECONNECT:
            case PlayerAllianceEvent.APPOINT_VICE_CAPTAIN: // Unused maybe...
            case PlayerAllianceEvent.DEMOTE_VICE_CAPTAIN:
            case PlayerAllianceEvent.APPOINT_CAPTAIN:
                abnormalEffects = player.GetEffectController().GetAbnormalEffectsToShow();
                break;
            case PlayerAllianceEvent.UPDATE_EFFECTS:
                abnormalEffects = player.GetEffectController().GetAbnormalEffectsToTargetSlot(slot);
                break;
        }
    }

    public SM_ALLIANCE_MEMBER_INFO(PlayerAllianceMember member, PlayerAllianceEvent eventValue)
        : this(member, eventValue, 0)
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        PlayerCommonData pcd = player.GetCommonData();
        WorldPosition wp = player.GetPosition();

        // Required so that when member is disconnected, and his playerAllianceGroup slot is changed, he will continue to
        // appear as disconnected to the alliance.
        if (eventValue == PlayerAllianceEvent.ENTER && !player.IsOnline())
            eventValue = PlayerAllianceEvent.ENTER_OFFLINE;

        WriteD(allianceId);
        WriteD(objectId);
        if (player.IsOnline())
        {
            PlayerLifeStats pls = player.GetLifeStats();
            WriteD(pls.GetMaxHp());
            WriteD(pls.GetCurrentHp());
            WriteD(pls.GetMaxMp());
            WriteD(pls.GetCurrentMp());
            WriteD(pls.GetMaxFp());
            WriteD(pls.GetCurrentFp());
        }
        else
        {
            WriteD(0);
            WriteD(0);
            WriteD(0);
            WriteD(0);
            WriteD(0);
            WriteD(0);
        }

        WriteD(0);// unk 3.5
        WriteD(wp.GetMapId());
        WriteD(wp.GetMapId() + wp.GetInstanceId() - 1);
        WriteF(wp.GetX());
        WriteF(wp.GetY());
        WriteF(wp.GetZ());
        WriteC(pcd.GetPlayerClass().GetClassId());
        WriteC(pcd.GetGender().GetGenderId());
        WriteC(pcd.GetLevel());
        WriteC(eventValue.GetId());
        WriteC(1); // unk, always 0x01 since removal of Sarpan & Tiamaranta
        WriteC(player.GetFlyState()); // isFly
        WriteC(0x0);
        switch (eventValue)
        {
            case PlayerAllianceEvent.LEAVE:
            case PlayerAllianceEvent.BANNED:
            case PlayerAllianceEvent.MOVEMENT:
            case PlayerAllianceEvent.DISCONNECTED:
                break;
            case PlayerAllianceEvent.UPDATE_EFFECTS:
                WriteD(0x00); // unk
                WriteD(0x00); // unk
                WriteC(slot);
                WriteH(abnormalEffects.Count); // Abnormal effects
                foreach (Effect effect in abnormalEffects)
                {
                    WriteD(effect.GetEffectorId()); // casterid
                    WriteH(effect.GetSkillId()); // spellid
                    WriteC(effect.GetSkillLevel()); // spell level
                    WriteC(Array.IndexOf(Enum.GetValues<SkillTargetSlot>(), effect.GetTargetSlot())); // unk ? (Java ordinal = position)
                    WriteD(effect.GetRemainingTimeToDisplay()); // estimatedtime
                }

                WriteD(0x00);
                WriteD(0x00);
                WriteD(0x00);
                WriteD(0x00);
                WriteD(0x00);
                WriteD(0x00);
                WriteD(0x00);
                WriteD(0x00);
                break;
            case PlayerAllianceEvent.JOIN:
            case PlayerAllianceEvent.ENTER:
            case PlayerAllianceEvent.ENTER_OFFLINE:
            case PlayerAllianceEvent.UPDATE:
            case PlayerAllianceEvent.RECONNECT:
            case PlayerAllianceEvent.APPOINT_VICE_CAPTAIN: // Unused maybe...
            case PlayerAllianceEvent.DEMOTE_VICE_CAPTAIN:
            case PlayerAllianceEvent.APPOINT_CAPTAIN:
                WriteS(pcd.GetName());
                WriteD(0x00); // unk
                WriteD(0x00); // unk
                if (player.IsOnline())
                {
                    WriteC(SkillTargetSlotExtensions.FULLSLOTS);
                    WriteH(abnormalEffects.Count); // Abnormal effects
                    foreach (Effect effect in abnormalEffects)
                    {
                        WriteD(effect.GetEffectorId()); // casterid
                        WriteH(effect.GetSkillId()); // spellid
                        WriteC(effect.GetSkillLevel()); // spell level
                        WriteC(Array.IndexOf(Enum.GetValues<SkillTargetSlot>(), effect.GetTargetSlot())); // unk ? (Java ordinal = position)
                        WriteD(effect.GetRemainingTimeToDisplay()); // estimatedtime
                    }
                    WriteD(0x00);
                    WriteD(0x00);
                    WriteD(0x00);
                    WriteD(0x00);
                    WriteD(0x00);
                    WriteD(0x00);
                    WriteD(0x00);
                    WriteD(0x00);
                }
                else
                {
                    WriteH(0);
                }
                break;
            case PlayerAllianceEvent.MEMBER_GROUP_CHANGE:
                WriteS(pcd.GetName());
                break;
        }
    }
}
