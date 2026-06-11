using System;
using System.Collections.Generic;

namespace Aion.GameServer.Model.GameObjects.Players.Title;

/// <summary>Java parity: model/gameobjects/player/title/TitleList.</summary>
public class TitleList
{
    // Java parity: LinkedHashMap — insertion-ordered.
    private readonly Dictionary<int, Title> titles;
    private Aion.GameServer.Model.GameObjects.Players.Player owner;

    public TitleList()
    {
        this.titles = new Dictionary<int, Title>();
        this.owner = null;
    }

    public void SetOwner(Aion.GameServer.Model.GameObjects.Players.Player owner)
    {
        this.owner = owner;
    }

    public Aion.GameServer.Model.GameObjects.Players.Player GetOwner()
    {
        return owner;
    }

    public bool Contains(int titleId)
    {
        return titles.ContainsKey(titleId);
    }

    public void AddEntry(int titleId, int remaining)
    {
        Aion.GameServer.Model.Templates.TitleTemplate tt = Aion.GameServer.Dataholders.DataManager.TITLE_DATA.GetTitleTemplate(titleId);
        if (tt == null)
        {
            throw new ArgumentException("Invalid title id " + titleId);
        }
        titles[titleId] = new Title(tt, titleId, remaining);
    }

    public bool AddTitle(int titleId, bool questReward, int time)
    {
        Aion.GameServer.Model.Templates.TitleTemplate tt = Aion.GameServer.Dataholders.DataManager.TITLE_DATA.GetTitleTemplate(titleId);
        if (tt == null)
        {
            throw new ArgumentException("Invalid title id " + titleId);
        }
        if (owner != null)
        {
            if (owner.GetRace() != tt.GetRace() && tt.GetRace() != Aion.GameServer.Model.Race.PC_ALL)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendMessage(owner, "This title is not available for your race.");
                return false;
            }
            Title entry = new Title(tt, titleId, time);
            if (!titles.ContainsKey(titleId))
            {
                titles[titleId] = entry;
                Aion.GameServer.Taskmanager.Tasks.ExpireTimerTask.GetInstance().RegisterExpirable(entry, owner);
                Aion.GameServer.Dao.PlayerTitleListDAO.StoreTitles(owner, entry);
            }
            else
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_TOOLTIP_LEARNED_TITLE());
                return false;
            }
            if (questReward)
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_QUEST_GET_REWARD_TITLE(tt.GetL10n()));
            else
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_GET_CASH_TITLE(tt.GetL10n()));

            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmTitleInfo(owner));
            return true;
        }
        return false;
    }

    public void SetDisplayTitle(int titleId)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmTitleInfo(titleId));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmTitleInfo(owner, titleId));
        owner.GetCommonData().SetTitleId(titleId);
        owner.GetController().UpdateNearbyQuests();
    }

    public void SetBonusTitle(int bonusTitleId)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmTitleInfo(6, bonusTitleId));
        if (owner.GetCommonData().GetBonusTitleId() > 0)
        {
            if (owner.GetGameStats() != null)
            {
                Aion.GameServer.Model.Stats.Listeners.TitleChangeListener.OnBonusTitleChange(owner.GetGameStats(), owner.GetCommonData().GetBonusTitleId(), false);
            }
        }
        owner.GetCommonData().SetBonusTitleId(bonusTitleId);
        if (bonusTitleId > 0 && owner.GetGameStats() != null)
        {
            Aion.GameServer.Model.Stats.Listeners.TitleChangeListener.OnBonusTitleChange(owner.GetGameStats(), bonusTitleId, true);
        }
    }

    public void RemoveTitle(int titleId)
    {
        if (!titles.ContainsKey(titleId))
            return;
        if (owner.GetCommonData().GetTitleId() == titleId)
            SetDisplayTitle(-1);
        if (owner.GetCommonData().GetBonusTitleId() == titleId)
            SetBonusTitle(-1);
        titles.Remove(titleId);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmTitleInfo(owner));
        Aion.GameServer.Dao.PlayerTitleListDAO.RemoveTitle(owner.GetObjectId(), titleId);
    }

    public int Size()
    {
        return titles.Count;
    }

    public ICollection<Title> GetTitles()
    {
        return titles.Values;
    }
}
