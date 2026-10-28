namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Read-only projection of the reviewed armor enchant table owners.
/// Inputs must already be selected from their respective 5.8 and 4.8 tables;
/// this does not select an item, equip it, or change a player's statistics.
/// </summary>
internal static class RetailNpcArmorEnchantMath
{
    internal static int Retail(int level,int start,int beforeStart,int increment,int direct)
    {
        if(level is <0 or >255 || start is <1 or >255)
            throw new ArgumentOutOfRangeException(nameof(level),"An ordinary native enchant level and positive threshold are required.");
        if(level==0)return 0;
        return Checked(level<start?direct:(long)beforeStart+(long)(level-start+1)*increment);
    }

    internal static int Host(int level,int maximum,int beforeMaximum,int increment,int direct)
    {
        if(level is <0 or >255 || maximum is <2 or >255)
            throw new ArgumentOutOfRangeException(nameof(level),"A supported host enchant level and table maximum are required.");
        if(level==0)return 0;
        if(level>maximum && maximum<21)
            throw new ArgumentOutOfRangeException(nameof(level),"The host rejects levels beyond a short enchant table.");
        return Checked(level<maximum?direct:(long)beforeMaximum+(long)(level-maximum+1)*increment);
    }

    private static int Checked(long value)
    {
        if(value is <int.MinValue or >int.MaxValue)
            throw new ArithmeticException("Unreviewed enchant arithmetic overflow.");
        return (int)value;
    }
}
