using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO (Lyahim, ATracer). Per-GroupEvent group member update (hp/mp/fp, position, class/gender/level, abnormal effects). Field event->eventValue (keyword); SkillTargetSlot.ordinal()->Array.IndexOf(Enum.GetValues) (position, see [[enum-ordinal-vs-value-trap]]); SkillTargetSlot.values()->Enum.GetValues; FULLSLOTS/GetId on SkillTargetSlotExtensions. Effect/PlayerGroup/WorldPosition red-tolerated.</summary>
public class SM_GROUP_MEMBER_INFO : AionServerPacket
{
    private int groupId;
    private Player player;
    private GroupEvent eventValue;
    private int slot;
    private List<Effect> abnormalEffects;

    public SM_GROUP_MEMBER_INFO(PlayerGroup group, Player player, GroupEvent eventValue, int slot)
    {
        this.groupId = group.GetTeamId();
        this.player = player;
        this.eventValue = eventValue;
        this.slot = slot;
        switch (eventValue)
        {
            case GroupEvent.ENTER:
            case GroupEvent.UPDATE:
                abnormalEffects = player.GetEffectController().GetAbnormalEffectsToShow();
                break;
            case GroupEvent.UPDATE_EFFECTS:
                abnormalEffects = player.GetEffectController().GetAbnormalEffectsToTargetSlot(slot);
                break;
        }
    }

    public SM_GROUP_MEMBER_INFO(PlayerGroup group, Player player, GroupEvent eventValue)
        : this(group, player, eventValue, 0)
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        PlayerLifeStats pls = player.GetLifeStats();
        PlayerCommonData pcd = player.GetCommonData();
        WorldPosition wp = player.GetPosition();

        if (eventValue == GroupEvent.ENTER && !player.IsOnline())
        {
            eventValue = GroupEvent.ENTER_OFFLINE;
        }

        WriteD(groupId);
        WriteD(player.GetObjectId());
        if (player.IsOnline())
        {
            WriteD(pls.GetMaxHp());
            WriteD(pls.GetCurrentHp());
            WriteD(pls.GetMaxMp());
            WriteD(pls.GetCurrentMp());
            WriteD(pls.GetMaxFp()); // maxflighttime
            WriteD(pls.GetCurrentFp()); // currentflighttime
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
        WriteC(pcd.GetPlayerClass().GetClassId()); // class id
        WriteC(pcd.GetGender().GetGenderId()); // gender id
        WriteC(pcd.GetLevel()); // level

        WriteC(eventValue.GetId()); // something events
        WriteC(1); // unk, always 0x01 since removal of Sarpan & Tiamarana
        WriteC(player.GetFlyState()); // isFly
        WriteC(player.IsMentor() ? 0x01 : 0x00);

        switch (eventValue)
        {
            case GroupEvent.MOVEMENT:
            case GroupEvent.DISCONNECTED:
            case GroupEvent.LEAVE:
                break;
            case GroupEvent.ENTER_OFFLINE:
            case GroupEvent.JOIN:
                WriteS(pcd.GetName()); // name
                break;
            case GroupEvent.UPDATE_EFFECTS:
                WriteD(0x00); // unk
                WriteD(0x00); // unk
                WriteC(slot);
                WriteH(abnormalEffects.Count); // Abnormal effects of slot type
                foreach (Effect effect in abnormalEffects)
                {
                    WriteD(effect.GetEffectorId()); // casterid
                    WriteH(effect.GetSkillId()); // spellid
                    WriteC(effect.GetSkillLevel()); // spell level
                    WriteC(Array.IndexOf(Enum.GetValues<SkillTargetSlot>(), effect.GetTargetSlot())); // unk ? (Java ordinal = position)
                    WriteD(effect.GetRemainingTimeToDisplay()); // estimatedtime
                }

                foreach (SkillTargetSlot targetSlot in Enum.GetValues<SkillTargetSlot>())
                {
                    if ((slot & targetSlot.GetId()) == 1)
                        WriteD(0x00); // TODO: remaining time ?
                    else
                        WriteD(0x00);
                }
                break;
            case GroupEvent.ENTER:
            case GroupEvent.UPDATE:
                WriteS(pcd.GetName()); // name
                WriteD(0x00); // unk
                WriteD(0x00); // unk
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
                foreach (SkillTargetSlot targetSlot in Enum.GetValues<SkillTargetSlot>())
                {
                    if ((SkillTargetSlotExtensions.FULLSLOTS & targetSlot.GetId()) == 1)
                        WriteD(0x00); // TODO: remaining time ?
                    else
                        WriteD(0x00);
                }
                break;
        }
    }
}
