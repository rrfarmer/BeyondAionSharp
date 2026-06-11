using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/PlayerSettings implements Persistable.</summary>
public class PlayerSettings : IPersistable
{
    private IPersistable.PersistentState persistentState;

    private byte[] uiSettings;
    private byte[] shortcuts;
    private byte[] houseBuddies;
    private int deny = 0;
    private int display = 0;

    public PlayerSettings()
    {
    }

    public PlayerSettings(byte[] uiSettings, byte[] shortcuts, byte[] houseBuddies, int deny, int display)
    {
        this.uiSettings = uiSettings;
        this.shortcuts = shortcuts;
        this.houseBuddies = houseBuddies;
        this.deny = deny;
        this.display = display;
    }

    /// <summary>the persistentState</summary>
    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    /// <param name="persistentState">the persistentState to set</param>
    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        this.persistentState = persistentState;
    }

    /// <summary>the uiSettings</summary>
    public byte[] GetUiSettings()
    {
        return uiSettings;
    }

    /// <param name="uiSettings">the uiSettings to set</param>
    public void SetUiSettings(byte[] uiSettings)
    {
        this.uiSettings = uiSettings;
        persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    /// <summary>the shortcuts</summary>
    public byte[] GetShortcuts()
    {
        return shortcuts;
    }

    /// <param name="shortcuts">the shortcuts to set</param>
    public void SetShortcuts(byte[] shortcuts)
    {
        this.shortcuts = shortcuts;
        persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    /// <summary>the houseBuddies</summary>
    public byte[] GetHouseBuddies()
    {
        return houseBuddies;
    }

    /// <param name="houseBuddies">the houseBuddies to set</param>
    public void SetHouseBuddies(byte[] houseBuddies)
    {
        this.houseBuddies = houseBuddies;
        persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    /// <summary>the display</summary>
    public int GetDisplay()
    {
        return display;
    }

    /// <param name="display">the display to set</param>
    public void SetDisplay(int display)
    {
        this.display = display;
        persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    /// <summary>the deny</summary>
    public int GetDeny()
    {
        return deny;
    }

    /// <param name="deny">the deny to set</param>
    public void SetDeny(int deny)
    {
        this.deny = deny;
        persistentState = IPersistable.PersistentState.UPDATE_REQUIRED;
    }

    public bool IsInDeniedStatus(DeniedStatus deny)
    {
        int isDeniedStatus = this.deny & deny.GetId();

        if (isDeniedStatus == deny.GetId())
            return true;

        return false;
    }
}
