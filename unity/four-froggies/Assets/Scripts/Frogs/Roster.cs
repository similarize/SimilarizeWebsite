using UnityEngine;

// ffu14: the fixed playable roster (same ten as Froggy Hop Racing and the Balloon Blast critters). USER RULE: only these
// names, each always the same animal - never invent others. Index 0..3 = the four froggies (same order as Froggies).
public static class Roster
{
    public enum Kind { Frog, Cat, Dog }
    public enum Look { Plain, Spots, Stripes, Socks, Shepherd, Dachshund }

    public struct Def
    {
        public string name; public Kind kind; public Look look; public string hex, markHex;
        public Def(string n, Kind k, Look l, string h, string m) { name = n; kind = k; look = l; hex = h; markHex = m; }
    }

    public static readonly Def[] All =
    {
        new Def("James", Kind.Frog, Look.Plain, "#3fbf3f", null),
        new Def("Jimmy", Kind.Frog, Look.Plain, "#a6d832", null),
        new Def("Bubbles", Kind.Frog, Look.Plain, "#2fb3c4", null),
        new Def("Rexy", Kind.Frog, Look.Plain, "#f2582a", null),
        new Def("Spotty", Kind.Cat, Look.Spots, "#f4f1ea", "#2a2626"),
        new Def("Tigy", Kind.Cat, Look.Stripes, "#f28c28", "#7a3a10"),
        new Def("Kitty", Kind.Cat, Look.Plain, "#b8a9c9", null),
        new Def("Little White Socks", Kind.Cat, Look.Socks, "#34343a", "#ffffff"),
        new Def("Germy", Kind.Dog, Look.Shepherd, "#b47a3c", "#1e1a18"),
        new Def("Daisy", Kind.Dog, Look.Dachshund, "#a0482a", null),
    };
    public const int Count = 10;

    public static bool Valid(int c) { return c >= 0 && c < Count; }
    public static string Name(int c) { return Valid(c) ? All[c].name : "?"; }
    public static Color Color(int c) { return Valid(c) ? Mats.Hex(All[c].hex) : UnityEngine.Color.white; }
    public static string Hex(int c) { return Valid(c) ? All[c].hex : "#ffffff"; }
    public static Color Mark(int c) { return Valid(c) && All[c].markHex != null ? Mats.Hex(All[c].markHex) : Color(c); }
    public static bool IsFrog(int c) { return !Valid(c) || All[c].kind == Kind.Frog; }
    public static Kind KindOf(int c) { return Valid(c) ? All[c].kind : Kind.Frog; }
    public static string Species(int c)
    {
        if (!Valid(c)) return "";
        var d = All[c];
        return d.kind == Kind.Frog ? "Frog" : d.kind == Kind.Cat ? "Cat" : d.look == Look.Shepherd ? "German shepherd" : "Dachshund";
    }
    // same one-liners as the hop game's roster card
    public static string Ability(int c)
    {
        if (!Valid(c)) return "";
        var d = All[c];
        return d.kind == Kind.Frog ? "Big springy hops" : d.kind == Kind.Cat ? "Quick, nimble turns" : d.look == Look.Shepherd ? "Strong, steady sprint" : "Low and zippy";
    }
    // a light, readable UI tint (Little White Socks is near-black)
    public static Color UiColor(int c)
    {
        Color k = Color(c);
        return k.grayscale < 0.3f ? UnityEngine.Color.Lerp(k, new Color(0.75f, 0.78f, 0.9f), 0.55f) : k;
    }
    public static string UiHex(int c) { return "#" + ColorUtility.ToHtmlStringRGB(UiColor(c)); }
    // short name for tight spots (name tags, mech plates)
    public static string Short(int c) { return c == 7 ? "Socks" : Name(c); }

    // movement per animal (frogs keep the original numbers)
    public static float RunSpeed(int c) { var d = Valid(c) ? All[c] : All[0]; return d.kind == Kind.Frog ? 6.5f : d.kind == Kind.Cat ? 7.0f : d.look == Look.Shepherd ? 7.8f : 6.9f; }
    public static float JumpV(int c) { var d = Valid(c) ? All[c] : All[0]; return d.kind == Kind.Frog ? 8.5f : d.kind == Kind.Cat ? 7.8f : 7.2f; }
    public static float TurnRate(int c) { var d = Valid(c) ? All[c] : All[0]; return d.kind == Kind.Cat ? 1080f : d.look == Look.Dachshund ? 900f : 720f; }
}
