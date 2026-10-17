using UnityEngine;

// ffu53 CONSISTENCY (audit C9): ONE ownership table, read by every way of getting into a vehicle (a froggy on foot, a
// robot sent from the phone, the borrow toast). Rules are CANON.md "Vehicles and ownership", unchanged - only applied the
// same way everywhere now:
//   OwnerOnly  mechs (StoryMech.CanEnter: each froggy pilots its own), the VTOL jet (James only, Jet.CanEnter);
//              Figure 03's rocket is owner-locked too but is not a Vehicle (its own ramp check in Fig03Rocket50.cs)
//   AskFirst   any vehicle with an ownerName: James's / Dad's Cybertrucks, Spotty's Tourer + Carrier helm, the family
//              Roadster - anyone may use it after asking the owner, if nobody is in it
//   Free       everything else on the ranch (ranch toys / work vehicles: tank, buggies, bikes, boats, RC cars, mower...)
// Before ffu53 the ask step only existed for a human froggy on foot; a robot sent from the phone took Spotty's motorcycle
// (or Dad's truck) without asking. Now the robot asks the owner for James, the same words, on the phone.
public static class Ownership
{
    public enum Rule { Free, AskFirst, OwnerOnly }

    public static Rule RuleFor(Vehicle v)
    {
        if (v == null) return Rule.Free;
        if (v is StoryMech || v is Jet) return Rule.OwnerOnly;
        if (v.ownerName != null) return Rule.AskFirst;
        return Rule.Free;
    }

    // the A / E line for a froggy standing next to a free (nobody inside) vehicle it may enter
    public static string EnterPrompt(Vehicle v, Frog f)
    {
        return v.OwnedBy(f) ? v.EnterVerb : "ask " + v.ownerName + " to borrow the " + v.Title;
    }

    // what is said when someone who isn't the owner gets in (froggy) or is sent to drive it (robot, on James's behalf)
    public static string AskLine(string who, Vehicle v, Frog forWhom)
    {
        if (v == null || RuleFor(v) != Rule.AskFirst) return null;
        if (forWhom != null && v.OwnedBy(forWhom)) return null;
        return who + " asked " + v.ownerName + " to borrow " + (forWhom != null && forWhom.nick != who ? "the " + v.Title + " for " + forWhom.nick : "it") + " - \"Sure, it's free. Drive safe!\"";
    }

    // AI froggies and idle robots never take an ask-first or owner-only vehicle on their own (CANON: AI froggies don't
    // take Spotty's motorcycle out on their own); they may if they own it
    public static bool AiMayTake(Vehicle v, Frog f) { return RuleFor(v) == Rule.Free || (RuleFor(v) == Rule.AskFirst && v.OwnedBy(f) && v.ownerChar >= 0); }

    static bool logged;
    public static void LogTable()
    {
        if (logged) return;
        logged = true;
        int free = 0, ask = 0, own = 0;
        string asks = "";
        foreach (Vehicle v in Vehicle.All)
        {
            if (v == null) continue;
            switch (RuleFor(v))
            {
                case Rule.Free: free++; break;
                case Rule.OwnerOnly: own++; break;
                default: ask++; asks += (asks.Length > 0 ? ", " : "") + v.Title + " (" + v.ownerName + ")"; break;
            }
        }
        Debug.Log("Ownership53: " + free + " free, " + ask + " ask-first [" + asks + "], " + own + " owner-only (mechs / jet)");
    }
}
