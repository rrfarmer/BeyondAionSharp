using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Ordinary NPC maximum-HP change in Npc::CalcSecondLevelAttr.</summary>
internal static class RetailNpcCurrentHpMath
{
    internal static int Adjust(int oldMaximum,int newMaximum,int current)
    {
        if(oldMaximum==0 || oldMaximum==newMaximum)return current;
        if(current<=0)return 0;
        float ratio=Divide(unchecked(newMaximum-oldMaximum),oldMaximum);
        float delta=Multiply(ratio,current);
        // x86 CVTTSS2SI returns INT_MIN for invalid/out-of-range inputs.
        int converted=float.IsNaN(delta)||delta < -2147483648f||delta >= 2147483648f
            ? int.MinValue : (int)delta;
        return Math.Max(1,unchecked(current+converted));
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float numerator,float denominator)=>numerator/denominator;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float left,float right)=>left*right;
}
