using W=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalWorldInputs;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Computed native base-record protector, not an original-result transition tape.
/// Active exhausted End/redirect/convert/positive opposite-direction HP remain typed children.
/// All geometry uses the pinned endpoint/same-World helper, never a caller answer.</summary>
internal static class RetailNpcUserNormalProtection
{
    internal readonly record struct Result(int Hp,int Mp,byte Flags,int Reflected,uint ReflectionSkill);
    internal static Result Calculate(W.Work w,int incoming,int classification)
    {
        Need(classification is 1 or 2&&incoming>=0,"protection","entry",0x1400b40f0,"Finite nonnegative ordinary physical/elemental classification required.");
        var c=w.Cursor;int hp=incoming,mp=0;byte output=c.U8(w.Record+0x32,0,"protector.output_flags");
        ulong manager=c.U64(w.User+0x158,0x1400b4230,"User.manager158");
        if(manager==0||incoming==0)return new(hp,0,output,0,0);
        c.External(manager,0x4404,"manager");
        uint flags=c.U32(manager+0x2b18,0x14034cb77,"protector.flags");
        if(flags==0)return new(hp,0,output,0,0);
        if((flags&0x40)!=0&&hp>0)
        {
            ulong row=manager+(ulong)(classification+0x3b5)*12;
            int draw=w.Draw("Shield",0x14034cbdf,1,1000);
            if(draw<=c.I32(row,0x14034cbe4,"shield.chance"))
            {
                int amount=c.I32(row+4,0x14034cbf3,"shield.amount");
                int mode=c.I32(row+8,0x14034cbfa,"shield.mode");
                amount=mode==0?System.Math.Min(hp,amount):System.Math.Max(0,Cvtt(Div(FromInt(unchecked(amount*hp)),100f)));
                int budget=c.I32(manager+0x2cac,0x14034cc25,"shield.budget");
                Need(amount>=0&&budget>=0,"protection","shield scalar",0,"Negative declared shield budget/amount is outside finite admission.");
                int used=System.Math.Min(amount,budget);hp=System.Math.Max(0,unchecked(hp-used));budget=unchecked(budget-used);
                c.W32(manager+0x2cac,unchecked((uint)budget),0x14034cc39,"shield.remaining_budget");
                Need(budget>=1,"incomplete-child","shield.End",0x14034cc75,"Exhausted shield invokes whole skill removal; no poststate tape admitted.",true);
                output|=2;
            }
        }
        flags=c.U32(manager+0x2b18,0x14034cc8e,"protector.flags.after_shield");
        if((flags&0x800)!=0&&hp>0)
        {
            ulong row=manager+(ulong)(classification+0x23c)*20;
            int draw=w.Draw("MpShield",0x14034ccb1,1,1000);
            if(draw<=c.I32(row,0x14034ccb6,"mp_shield.chance"))
            {
                int mode=c.I32(row+8,0x14034ccc0,"mp_shield.mode");
                int amount=c.I32(row+4,mode==0?0x14034cce9ul:0x14034ccc8ul,"mp_shield.amount");
                amount=mode==0?System.Math.Min(hp,amount):System.Math.Max(0,Cvtt(Div(FromInt(unchecked(amount*hp)),100f)));
                int currentMp=c.I32(w.User+0x790,0x1400b2bf0,"User.actual_MP_slot62");
                int budget=c.I32(manager+0x2d00,0x14034ccff,"mp_shield.budget");
                amount=System.Math.Min(amount,currentMp);
                Need(amount>=0&&budget>=0,"protection","MP shield scalar",0,"Negative consumed MP-shield values outside finite admission.");
                if(amount<budget)
                {
                    budget=unchecked(budget-amount);
                    c.W32(manager+0x2d00,unchecked((uint)budget),0x14034cd12,"mp_shield.remaining_budget");
                    int reduction=c.I32(row+0x10,0x14034cd22,"User.mp_shield.hp_percent");
                    hp=Cvtt(Sub(FromInt(hp),Div(FromInt(unchecked(reduction*amount)),100f)));
                    mp=Cvtt(Div(FromInt(unchecked(c.I32(manager+0x2d04,0x14034cd4a,"mp_shield.mp_percent")*amount)),100f));
                }
                else
                {
                    int reduction=c.I32(row+0x10,0x14034cd72,"User.mp_shield.hp_percent");
                    hp=Cvtt(Sub(FromInt(hp),Div(FromInt(unchecked(reduction*budget)),100f)));
                    int percent=c.I32(manager+0x2d04,0x14034cd9a,"mp_shield.mp_percent");
                    int oldBudget=c.I32(manager+0x2d00,0x14034cda0,"mp_shield.depleted_budget");
                    mp=Cvtt(Div(FromInt(unchecked(percent*oldBudget)),100f));
                    budget=0;c.W32(manager+0x2d00,0,0x14034cdbc,"mp_shield.remaining_budget");
                }
                hp=System.Math.Max(hp,0);if(mp<0)mp=1;
                Need(c.I32(manager+0x2d00,0x14034cde6,"mp_shield.remaining_budget_for_End")>=1&&amount!=currentMp,
                    "incomplete-child","mp_shield.End",0x14034cdf6,"MP shield depletion/current-MP boundary invokes whole skill removal.",true);
                c.W32(w.Record+0x1c,unchecked((uint)mp),0x14034ce07,"record.mp_shield_metadata_amount");
                c.W32(w.Record+0x20,c.U32(row+0xc,0x14034ce0a,"mp_shield.skill"),0x14034ce0f,"record.mp_shield_metadata_skill");
                output|=0x10;
            }
        }
        flags=c.U32(manager+0x2b18,0x14034ce37,"protector.flags.reflection");
        uint reflectionMask=classification==1?0x10u:0x20u;
        if((flags&reflectionMask)!=0)
        {
            // Actual source NPC slot28 returns1; kind1 excludes the native kind0x40 skip.
            Need(c.I32(w.Source+0x1940,0x14034ce70,"reflection.source_kind")==1,"incomplete-child","reflection.source",0,"Unclosed alternate source kind.",true);
            ulong row=manager+(classification==1?0ul:24ul);
            float max=c.F32(row+0x2c44,0x14034cea3,"reflection.max_squared");
            if(Within(c,w.User,w.Source,max,0x14034ceab))
            {
                float min=c.F32(row+0x2c40,0x14034cebe,"reflection.min_squared");
                if(!Within(c,w.User,w.Source,min,0x14034cec7))
                {
                    int sample=w.Draw("Reflection",0x14034cedc,1,1000);
                    if(sample<=c.I32(row+0x2c48,0x14034cee2,"reflection.chance"))
                    {
                        int reflected=Cvtt(Add(Mul(Div(FromInt(c.I32(row+0x2c38,0x14034ceee,"reflection.percent")),100f),FromInt(incoming)),
                            FromInt(c.I32(row+0x2c3c,0x14034cf06,"reflection.flat"))));
                        // A positive result reaches source slot188 (template3f8), then actual
                        // User->NPC HP. Neither is supplied as a desired original return here.
                        Need(reflected<=0,"incomplete-child","reflection.actual_User_to_Npc",0x14034cf3c,"Positive reflection requires its source override and opposite-direction HP/attribution owner.",true);
                    }
                }
            }
        }
        if(hp>0)
        {
            uint protectMask=classification==1?1u:2u;
            flags=c.U32(manager+0x2b18,0x14034cfae,"protector.flags.redirect");
            ulong row=manager+(ulong)classification*0x1c;
            if((flags&protectMask)!=0&&c.U32(row+0x2b20,0x14034cfd5,"protected_recipient.id")!=0)
            {
                int draw=w.Draw("ProtectedRecipient",0x14034cfeb,1,1000);
                Need(draw>c.I32(row+0x2b24,0x14034cff4,"protected_recipient.chance"),"incomplete-child","protected_recipient.owner",0x14034d015,
                    "Active protected recipient requires actual lookup/geometry/type/HP owner.",true);
            }
            uint convertMask=classification==1?4u:8u;
            flags=c.U32(manager+0x2b18,0x14034d165,"protector.flags.convert");
            if((flags&convertMask)!=0)
            {
                int draw=w.Draw("Conversion",0x14034d18b,1,1000);
                Need(draw>c.I32(manager+0x2ba8+(ulong)classification*0x24,0x14034d193,"conversion.chance"),
                    "incomplete-child","conversion.heal_owner",0x14034d19b,"Active conversion needs exact signed heal/record/application ownership.",true);
            }
        }
        return new(hp,mp,output,0,0);
    }
    internal static bool Within(Cursor c,ulong first,ulong second,float squared,ulong site)
    {
        if(c.U64(second+0x118,site,"geometry.second.World")!=c.U64(first+0x118,site,"geometry.first.World"))return false;
        float y=Sub(c.F32(second+0x124,site,"geometry.second.Y"),c.F32(first+0x124,site,"geometry.first.Y"));
        float x=Sub(c.F32(second+0x120,site,"geometry.second.X"),c.F32(first+0x120,site,"geometry.first.X"));
        float z=Sub(c.F32(second+0x128,site,"geometry.second.Z"),c.F32(first+0x128,site,"geometry.first.Z"));
        return Add(Add(Mul(y,y),Mul(x,x)),Mul(z,z))<=squared;
    }
}
