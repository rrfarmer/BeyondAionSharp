namespace Aion.GameServer.Model.Guide;

/// <summary>Java parity: model/guide/Guide (xTz).</summary>
public class Guide
{
    private int guide_id;
    private int player_id;
    private string title;

    public Guide(int guide_id, int player_id, string title)
    {
        this.guide_id = guide_id;
        this.player_id = player_id;
        this.title = title;
    }

    public int GetGuideId()
    {
        return guide_id;
    }

    public int GetPlayerId()
    {
        return player_id;
    }

    public string GetTitle()
    {
        return title;
    }
}
