using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model.GameObjects.FindGroup;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FIND_GROUP (cura, MrPoke). Find-group / instance-group board (recruitments, applications, server-wide instance groups, prepare-for-entry windows). List&lt;? extends FindGroupEntry&gt; -> IReadOnlyList&lt;FindGroupEntry&gt; (covariant, accepts subtype lists); switch-arrow -> switch statement; instanceof->is; getName(true)->GetName(true); currentTimeMillis()/1000->DateTimeOffset. FindGroup model/NetworkConfig red-tolerated.</summary>
public class SM_FIND_GROUP : AionServerPacket
{
    private int action;
    private IReadOnlyList<FindGroupEntry> entries;
    private byte serverId, unk1, unk2, unk3;
    private int idToDelete;
    private Player instanceApplicant;
    private bool showEnterInstanceMessage;
    private List<int> instanceMaskIds;

    public SM_FIND_GROUP(int action, IReadOnlyList<FindGroupEntry> entries)
    {
        this.action = action;
        this.entries = entries;
    }

    public SM_FIND_GROUP(int recruitmentIdToDelete, byte serverId, byte unk1, byte unk2, byte unk3)
    {
        this.action = 1;
        this.idToDelete = recruitmentIdToDelete;
        this.serverId = serverId;
        this.unk1 = unk1;
        this.unk2 = unk2;
        this.unk3 = unk3;
    }

    public SM_FIND_GROUP(int applicationIdToDelete)
    {
        this.action = 5;
        this.idToDelete = applicationIdToDelete;
    }

    public SM_FIND_GROUP(Player instanceApplicant)
    {
        this.action = 11;
        this.instanceApplicant = instanceApplicant;
    }

    public SM_FIND_GROUP(bool showEnterInstanceMessage)
    {
        this.action = 23;
        this.showEnterInstanceMessage = showEnterInstanceMessage;
    }

    public SM_FIND_GROUP(List<int> instanceMaskIds)
    {
        this.action = 26;
        this.instanceMaskIds = instanceMaskIds;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);
        switch (action)
        {
            case 0:
                ShowRecruitments((List<GroupRecruitment>)entries, (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000));
                break;
            case 1:
                RemoveRecruitment(idToDelete, serverId, unk1, unk2, unk3);
                break;
            case 4:
                ShowApplications((List<GroupApplication>)entries, (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000));
                break;
            case 5:
                RemoveApplication(idToDelete);
                break;
            case 10:
                ShowInstanceGroups((List<ServerWideGroup>)entries, (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000));
                break;
            case 11:
                SendInstanceGroupApplicationAsWhisperChatMessage(instanceApplicant);
                break;
            case 14:
                RegisterInstanceGroup((List<ServerWideGroup>)entries);
                break;
            case 16:
                ShowInstanceGroupMemberInfo((ServerWideGroup)entries[0], (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000));
                break;
            case 18:
                ShowEnterButtonInPrepareForEntryWindow((ServerWideGroup)entries[0]); // window must be initialized
                break;
            case 22:
                ShowPrepareForEntryWindow((ServerWideGroup)entries[0]); // initialize window if necessary
                break;
            case 23:
                DestroyPrepareForEntryWindow((ServerWideGroup)entries[0], showEnterInstanceMessage);
                break;
            case 24:
                UpdatePrepareForEntryWindow((ServerWideGroup)entries[0]);
                break;
            case 26:
                EnableRegisterForInstances(instanceMaskIds);
                break;
        }
    }

    private void ShowRecruitments(List<GroupRecruitment> recruitments, int lastUpdate)
    {
        WriteH(recruitments.Count);
        WriteH(recruitments.Count);
        WriteD(lastUpdate);
        foreach (GroupRecruitment recruitment in recruitments)
        {
            WriteD(recruitment.GetObjectId()); // team ID or recruiter ID if still solo
            WriteC(NetworkConfig.GAMESERVER_ID);
            WriteC(0); // unk (always 0)
            WriteC(0); // unk (always 0)
            WriteC(recruitment.GetObject() is Player ? 16 : 0); // 16: solo, 0: group | alliance
            WriteC(recruitment.GetGroupType()); // 0: group, 1: alliance, 2: mentor
            WriteS(recruitment.GetMessage()); // text
            WriteS(recruitment.GetName()); // recruiter name
            WriteC(recruitment.GetSize()); // members count
            WriteC(recruitment.GetMinLevel()); // members lowest level
            WriteC(recruitment.GetMaxLevel()); // members highest level
            WriteD(recruitment.GetLastUpdate()); // client hides entries older than two hours
        }
    }

    private void RemoveRecruitment(int playerOrTeamId, byte serverId, byte unk1, byte unk2, byte unk3)
    {
        WriteD(playerOrTeamId);
        WriteC(serverId);
        WriteC(unk1); // unk (always 0)
        WriteC(unk2); // unk (always 0)
        WriteC(unk3); // 16: solo, 0: group | alliance
    }

    private void ShowApplications(List<GroupApplication> applications, int lastUpdate)
    {
        WriteH(applications.Count);
        WriteH(applications.Count);
        WriteD(lastUpdate);
        foreach (GroupApplication application in applications)
        {
            WriteD(application.GetPlayer().GetObjectId());
            WriteC(application.GetGroupType()); // 0:group, 1:alliance
            WriteS(application.GetMessage()); // text
            WriteS(application.GetPlayer().GetName(true));
            WriteC(application.GetClassId()); // applied player class id
            WriteC(application.GetLevel()); // applied player level
            WriteD(application.GetLastUpdate()); // client hides entries older than two hours
        }
    }

    private void RemoveApplication(int playerId)
    {
        WriteD(playerId);
    }

    private void ShowInstanceGroups(List<ServerWideGroup> instanceGroups, int lastUpdate)
    {
        WriteH(instanceGroups.Count);
        WriteH(instanceGroups.Count);
        WriteD(lastUpdate);
        foreach (ServerWideGroup instanceGroup in instanceGroups)
        {
            WriteD(instanceGroup.GetId());// GroupEntryId
            WriteD(instanceGroup.GetInstanceMaskId());
            WriteD(1);// unk
            WriteC(instanceGroup.GetMembers().Count);
            WriteC(instanceGroup.GetMinMembers());
            WriteH(0);// unk maybe spacer
            WriteD(instanceGroup.GetRecruiter().GetObjectId());// playerObjId
            WriteD(1);// unk
            WriteD(0);// unk
            WriteC(instanceGroup.GetMinLevel());// playerLevel
            WriteC(instanceGroup.GetMaxLevel());// playerLevel
            WriteH(0);// unk maybe spacer?
            WriteD(instanceGroup.GetLastUpdate());// lastUpdate
            WriteD(0);// unk
            WriteS(instanceGroup.GetRecruiter().GetName(true));
            WriteS(instanceGroup.GetMessage());// Message
        }
    }

    private void SendInstanceGroupApplicationAsWhisperChatMessage(Player instanceApplicant)
    {
        WriteD(instanceApplicant.GetObjectId());
        WriteD(0);
        WriteD(0);
        WriteH(0);
        WriteC(0);
        WriteC(instanceApplicant.GetPlayerClass().GetClassId());
        WriteD(instanceApplicant.GetLevel());
        WriteS(instanceApplicant.GetName(true));
    }

    private void RegisterInstanceGroup(List<ServerWideGroup> instanceGroups)
    {
        WriteC(1);// packetNumber 0 || 1 || 2
        foreach (ServerWideGroup instanceGroup in instanceGroups)
        {
            WriteD(instanceGroup.GetId());// GroupEntryId (counts forwards every entry)
            WriteD(instanceGroup.GetInstanceMaskId());
            WriteD(1);// position?
            WriteC(instanceGroup.GetMembers().Count);
            WriteC(instanceGroup.GetMinMembers());// min members to enter Instance(writer choose it)
            WriteH(0);// unk maybe spacer
            WriteD(instanceGroup.GetRecruiter().GetObjectId());// playerObjId leader ID?
            WriteC(1);// unk
            WriteC(0);// unkGroupType?
            WriteD(1);// unk
            WriteH(0);// unk
            WriteC(instanceGroup.GetMinLevel());
            WriteC(instanceGroup.GetMaxLevel());
            WriteH(0);// unk
            WriteD(instanceGroup.GetLastUpdate());// timestamp
            WriteD(0);// unk
            WriteS(instanceGroup.GetRecruiter().GetName(true));
            WriteS(instanceGroup.GetMessage());
        }
    }

    private void ShowInstanceGroupMemberInfo(ServerWideGroup instanceGroup, int lastUpdate)
    {
        List<Player> members = instanceGroup.GetMembers();
        WriteH(members.Count);
        WriteH(members.Count);
        WriteD(lastUpdate);
        foreach (Player member in members)
        {
            WriteD(0);// groupId?
            WriteD(member.GetWorldId());
            WriteD(member.GetObjectId());
            WriteD(member.GetLevel());
            WriteD(member.GetPlayerClass().GetClassId());
            WriteH(1);// unk
            WriteC(0);// groupType?
            WriteC(0);// unk
            WriteS(member.GetName(true));
        }
    }

    private void ShowEnterButtonInPrepareForEntryWindow(ServerWideGroup instanceGroup)
    {
        WriteD(instanceGroup.GetId()); // GroupEntryId
        WriteD(instanceGroup.GetInstanceMaskId());
    }

    private void ShowPrepareForEntryWindow(ServerWideGroup instanceGroup)
    {
        WriteD(instanceGroup.GetId()); // GroupEntryId
        WriteD(instanceGroup.GetInstanceMaskId());
    }

    private void DestroyPrepareForEntryWindow(ServerWideGroup instanceGroup, bool showEnterInstanceMessage)
    {
        WriteD(instanceGroup.GetId()); // GroupEntryId
        WriteD(instanceGroup.GetInstanceMaskId());
        WriteC(showEnterInstanceMessage ? 1 : 0);
    }

    private void UpdatePrepareForEntryWindow(ServerWideGroup instanceGroup)
    {
        List<Player> instanceGroupMembers = instanceGroup.GetMembers();
        WriteD(instanceGroup.GetId()); // GroupEntryId
        WriteD(instanceGroup.GetInstanceMaskId());
        WriteC(instanceGroupMembers.Count);
        foreach (Player member in instanceGroupMembers)
        {
            WriteD(0); // server ID?
            WriteD(0); // server ID?
            WriteD(member.GetObjectId());
            WriteD(member.GetLevel());
            WriteD(member.GetPlayerClass().GetClassId());
            WriteH(0); // ?
            WriteC(1); // 0: Preparing, 1: Ready
            WriteC(member.IsOnline() ? 1 : 0);
            WriteS(member.GetName(true));
        }
    }

    private void EnableRegisterForInstances(List<int> instanceMaskIds)
    {
        WriteH(instanceMaskIds.Count);
        foreach (int instanceMaskId in instanceMaskIds)
            WriteD(instanceMaskId);
    }
}
