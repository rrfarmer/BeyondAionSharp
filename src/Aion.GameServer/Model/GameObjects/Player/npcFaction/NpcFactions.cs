using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.GameObjects.Players.Npcfaction;

/// <summary>Java parity: model/gameobjects/player/npcFaction/NpcFactions.</summary>
public class NpcFactions
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly Player owner;

    private readonly Dictionary<int, NpcFaction> factions = new Dictionary<int, NpcFaction>();
    private readonly NpcFaction[] activeNpcFaction = new NpcFaction[2];
    private readonly int[] timeLimit = new int[] { 0, 0 };

    public NpcFactions(Player owner)
    {
        this.owner = owner;
    }

    public void AddNpcFaction(NpcFaction faction)
    {
        factions[faction.GetId()] = faction;
        int type = 0;
        if (faction.IsMentor())
            type = 1;

        if (faction.IsActive())
            activeNpcFaction[type] = faction;
        if (faction.GetTime() == -1)
        {
            // used to reset from quest daily command
            faction.SetTime((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000));
            timeLimit[type] = faction.GetTime();
        }
        else if (timeLimit[type] < faction.GetTime() && faction.GetState() == ENpcFactionQuestState.COMPLETE)
            timeLimit[type] = faction.GetTime();
    }

    public NpcFaction GetFactionById(int id)
    {
        return factions.TryGetValue(id, out NpcFaction faction) ? faction : null;
    }

    public ICollection<NpcFaction> GetNpcFactions()
    {
        return factions.Values;
    }

    public NpcFaction GetActiveNpcFaction(bool mentor)
    {
        return activeNpcFaction[mentor ? 1 : 0];
    }

    public NpcFaction SetActive(int npcFactionId)
    {
        if (!factions.TryGetValue(npcFactionId, out NpcFaction npcFaction))
        {
            npcFaction = new NpcFaction(npcFactionId, 0, false, ENpcFactionQuestState.NOTING, 0);
            factions[npcFactionId] = npcFaction;
        }
        npcFaction.SetActive(true);
        activeNpcFaction[npcFaction.IsMentor() ? 1 : 0] = npcFaction;
        return npcFaction;
    }

    public void LeaveNpcFaction(Npc npc)
    {
        int targetObjectId = npc.GetObjectId();
        Aion.GameServer.Model.Templates.Factions.NpcFactionTemplate npcFactionTemplate = Aion.GameServer.Dataholders.DataManager.NPC_FACTIONS_DATA.GetNpcFactionByNpcId(npc.GetNpcId());
        if (npcFactionTemplate == null)
            return;
        NpcFaction npcFaction = GetFactionById(npcFactionTemplate.GetId());
        if (npcFaction == null || !npcFaction.IsActive())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmDialogWindow(targetObjectId, 1438));
            return;
        }
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmDialogWindow(targetObjectId, 1353));
        LeaveNpcFaction(npcFaction);
    }

    private void LeaveNpcFaction(NpcFaction npcFaction)
    {
        Aion.GameServer.Model.Templates.Factions.NpcFactionTemplate npcFactionTemplate = Aion.GameServer.Dataholders.DataManager.NPC_FACTIONS_DATA.GetNpcFactionById(npcFaction.GetId());

        Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_FACTION_LEAVE(npcFactionTemplate.GetL10n()));
        npcFaction.SetActive(false);
        activeNpcFaction[npcFactionTemplate.IsMentor() ? 1 : 0] = null;
        if (npcFaction.GetState() == ENpcFactionQuestState.START)
        {
            Aion.GameServer.Services.QuestService.AbandonQuest(owner, npcFaction.GetQuestId());
            npcFaction.SetState(ENpcFactionQuestState.NOTING);
        }
    }

    public void EnterGuild(Npc npc)
    {
        int targetObjectId = npc.GetObjectId();
        Aion.GameServer.Model.Templates.Factions.NpcFactionTemplate npcFactionTemplate = Aion.GameServer.Dataholders.DataManager.NPC_FACTIONS_DATA.GetNpcFactionByNpcId(npc.GetNpcId());
        if (npcFactionTemplate == null)
        {
            log.LogWarning("Missing faction for faction registrar npc " + npc.GetNpcId());
            return;
        }
        NpcFaction npcFaction = GetFactionById(npcFactionTemplate.GetId());
        NpcFaction activeNpcFactionLocal = GetActiveNpcFaction(npcFactionTemplate.IsMentor());
        int npcFactionId = npcFactionTemplate.GetId();
        int skillPoints = npcFactionTemplate.GetSkillPoints();
        if (skillPoints != 0)
        {
            bool canEnter = false;
            if (npcFactionTemplate.GetCategory() == Aion.GameServer.Model.Templates.Factions.FactionCategory.COMBINESKILL)
            {
                foreach (Aion.GameServer.Model.Skill.PlayerSkillEntry skill in owner.GetSkillList().GetAllSkills())
                {
                    if (skill.IsCraftingSkill() && skill.GetSkillLevel() >= skillPoints)
                    {
                        canEnter = true;
                        break;
                    }
                }
            }
            if (!canEnter)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmDialogWindow(targetObjectId, 1098));
                return;
            }
        }
        if (owner.GetLevel() < npcFactionTemplate.GetMinLevel() || owner.GetLevel() > npcFactionTemplate.GetMaxLevel())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmDialogWindow(targetObjectId, 1182));
            return;
        }
        if (owner.GetRace() != npcFactionTemplate.GetRace() && npcFactionTemplate.GetRace() != Aion.GameServer.Model.Race.NPC)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmDialogWindow(targetObjectId, 1097));
            return;
        }
        if (npcFaction != null && npcFaction.IsActive())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_FACTION_CAN_NOT_JOIN());
            return;
        }
        if (activeNpcFactionLocal != null && activeNpcFactionLocal.GetId() != npcFactionId)
        {
            AskLeaveNpcFaction(npc);
            return;
        }
        if (npcFaction == null || !npcFaction.IsActive())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_FACTION_JOIN(npcFactionTemplate.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmDialogWindow(targetObjectId, 1012));
            SetActive(npcFactionId);
            SendDailyQuest();
        }
    }

    private void AskLeaveNpcFaction(Npc npc)
    {
        Aion.GameServer.Model.Templates.Factions.NpcFactionTemplate npcFactionTemplate = Aion.GameServer.Dataholders.DataManager.NPC_FACTIONS_DATA.GetNpcFactionByNpcId(npc.GetNpcId());
        NpcFaction activeNpcFactionLocal = GetActiveNpcFaction(npcFactionTemplate.IsMentor());
        Aion.GameServer.Model.Templates.Factions.NpcFactionTemplate activeNpcFactionTemplate = Aion.GameServer.Dataholders.DataManager.NPC_FACTIONS_DATA.GetNpcFactionById(activeNpcFactionLocal.GetId());
        Aion.GameServer.Model.GameObjects.Players.RequestResponseHandler<Player> responseHandler = new AskLeaveResponseHandler(owner, this, npc, activeNpcFactionLocal);
        bool requested = owner.GetResponseRequester().PutRequest(Aion.GameServer.Network.Aion.ServerPackets.SmQuestionWindow.STR_ASK_JOIN_NEW_FACTION, responseHandler);
        if (requested)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner,
                new Aion.GameServer.Network.Aion.ServerPackets.SmQuestionWindow(Aion.GameServer.Network.Aion.ServerPackets.SmQuestionWindow.STR_ASK_JOIN_NEW_FACTION, 0, 0, activeNpcFactionTemplate.GetL10n(), npcFactionTemplate.GetL10n()));
        }
    }

    // Java parity: anonymous RequestResponseHandler<Player> in askLeaveNpcFaction.
    private sealed class AskLeaveResponseHandler : Aion.GameServer.Model.GameObjects.Players.RequestResponseHandler<Player>
    {
        private readonly NpcFactions outer;
        private readonly Npc npc;
        private readonly NpcFaction activeNpcFaction;

        public AskLeaveResponseHandler(Player owner, NpcFactions outer, Npc npc, NpcFaction activeNpcFaction) : base(owner)
        {
            this.outer = outer;
            this.npc = npc;
            this.activeNpcFaction = activeNpcFaction;
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            outer.LeaveNpcFaction(activeNpcFaction);
            outer.EnterGuild(npc);
        }
    }

    public void StartQuest(Aion.GameServer.Model.Templates.QuestTemplate questTemplate)
    {
        NpcFaction npcFaction = activeNpcFaction[questTemplate.IsMentor() ? 1 : 0];
        if (npcFaction == null)
            return;
        if (npcFaction.GetState() != ENpcFactionQuestState.NOTING && npcFaction.GetQuestId() == 0)
            return;
        npcFaction.SetState(ENpcFactionQuestState.START);
    }

    public void AbortQuest(Aion.GameServer.Model.Templates.QuestTemplate questTemplate)
    {
        NpcFaction npcFaction = GetFactionById(questTemplate.GetNpcFactionId());
        if (npcFaction == null || !npcFaction.IsActive())
            return;
        npcFaction.SetState(ENpcFactionQuestState.NOTING);
        SendDailyQuest();
    }

    public void CompleteQuest(Aion.GameServer.Model.Templates.QuestTemplate questTemplate)
    {
        NpcFaction npcFaction = activeNpcFaction[questTemplate.IsMentor() ? 1 : 0];
        if (npcFaction == null)
            return;
        npcFaction.SetTime(GetNextTime());
        npcFaction.SetState(ENpcFactionQuestState.COMPLETE);
        timeLimit[npcFaction.IsMentor() ? 1 : 0] = npcFaction.GetTime();
        if (questTemplate.GetMentorType() == Aion.GameServer.Model.Templates.Quest.QuestMentorType.MENTOR)
        {
            owner.GetCommonData().SetMentorFlagTime((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + 60 * 60 * 24); // TODO 1 day
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmTitleInfo(owner, true), false);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmTitleInfo(true));
        }
    }

    public void SendDailyQuest()
    {
        for (int i = 0; i < 2; i++)
        {
            NpcFaction faction = activeNpcFaction[i];
            if (faction == null || !faction.IsActive())
                continue;
            if (timeLimit[i] > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000)
                continue;
            int questId = 0;
            switch (faction.GetState())
            {
                case ENpcFactionQuestState.COMPLETE:
                    if (faction.GetTime() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000)
                        continue;
                    break;
                case ENpcFactionQuestState.START:
                    continue;
                case ENpcFactionQuestState.NOTING:
                    if (faction.GetTime() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000)
                        questId = faction.GetQuestId();
                    break;
            }

            if (questId == 0)
            {
                List<Aion.GameServer.Model.Templates.QuestTemplate> quests = Aion.GameServer.Dataholders.DataManager.QUEST_DATA.GetQuestsByNpcFaction(faction.GetId(), owner);
                if (quests.Count == 0)
                    continue;
                questId = Aion.GameServer.Commons.Utils.Rnd.Get(quests).GetId();
                faction.SetQuestId(questId);
                faction.SetTime(GetNextTime());
                faction.SetState(ENpcFactionQuestState.NOTING);
            }
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmQuestAction(questId));
        }
    }

    public void OnLevelUp()
    {
        for (int i = 0; i < 2; i++)
        {
            NpcFaction faction = activeNpcFaction[i];
            if (faction == null || !faction.IsActive())
                continue;
            Aion.GameServer.Model.Templates.Factions.NpcFactionTemplate npcFactionTemplate = Aion.GameServer.Dataholders.DataManager.NPC_FACTIONS_DATA.GetNpcFactionById(faction.GetId());
            if (npcFactionTemplate.GetMaxLevel() < owner.GetLevel())
            {
                faction.SetActive(false);
                activeNpcFaction[i] = null;
                if (faction.GetState() == ENpcFactionQuestState.START)
                    Aion.GameServer.Services.QuestService.AbandonQuest(owner, faction.GetQuestId());
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_FACTION_LEAVE_BY_LEVEL_LIMIT(npcFactionTemplate.GetL10n()));
                faction.SetState(ENpcFactionQuestState.NOTING);
            }
        }
    }

    private int GetNextTime()
    {
        DateTimeOffset now = Aion.GameServer.Utils.Time.ServerTime.Now();
        long repeatDateEpochSeconds;
        DateTimeOffset nineAm = new DateTimeOffset(now.Year, now.Month, now.Day, 9, 0, 0, now.Offset);
        if (now.Hour >= 9)
            repeatDateEpochSeconds = nineAm.AddDays(1).ToUnixTimeSeconds(); // tomorrow morning at 9:00 AM
        else
            repeatDateEpochSeconds = nineAm.ToUnixTimeSeconds(); // today morning at 9:00 AM
        return (int)repeatDateEpochSeconds;
    }

    public bool CanStartQuest(Aion.GameServer.Model.Templates.QuestTemplate template)
    {
        int type = template.IsMentor() ? 1 : 0;
        return activeNpcFaction[type] != null && timeLimit[type] < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000;
    }
}
