using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

// ffu6q VOICE TALK (Bill 2026-10-19: "We wanna be able to talk to the robots when they're on missions. And have them talk
// back. So by voice."). Push-to-talk through each player's ROBOT PHONE, walkie-talkie style:
//  * TALK button on the phone (tap / click: listens until you stop talking, tap again to stop), pad RT held while the phone
//    is open (RT is free there - the phone eats driving input), keyboard T held. One mic at a time in split-screen; the
//    reply goes to the player who spoke (their phone caption + their toast).
//  * Speech in = the browser's own recogniser (Plugins/WebGL/FFVoice.jslib), interim words shown live. No network AI, no
//    API keys. Browsers without one (Firefox, the Tesla browser): "Voice isn't available here - tap a command" and the
//    quick-reply chips (always shown) run the same words through the same parser.
//  * VoiceBrain: local kid-friendly intent parser (robot names + misheard forms, the PLAY verbs, chores, missions, queries,
//    jokes / greetings). An order runs RobotPhone.Do = exactly what the phone button does (C1-C10 consistency).
//  * Talk back: speechSynthesis with a pitch / rate / voice per robot, replies built from live state (StatusLine, mission
//    phase / got / want, world, distance + direction to the caller, nearest froggy, battery). Robots radio in on their own at
//    mission milestones (blast off, landed, found one, flying home, done), on arrival and when stuck - rate-limited.
//  * Phone settings strip: VOICE on/off (spoken replies), CHATTER on/off, VOL 100/60/30 (PlayerPrefs ff6v_*).
public static class FFVoice
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void FFVoice_Init();
    [DllImport("__Internal")] static extern int FFVoice_Supported();
    [DllImport("__Internal")] static extern int FFVoice_TtsSupported();
    [DllImport("__Internal")] static extern void FFVoice_SetFake(string p);
    [DllImport("__Internal")] static extern int FFVoice_Start(string lang);
    [DllImport("__Internal")] static extern void FFVoice_Stop();
    [DllImport("__Internal")] static extern string FFVoice_Poll();
    [DllImport("__Internal")] static extern int FFVoice_Speak(string text, float pitch, float rate, int vidx, float vol);
    [DllImport("__Internal")] static extern void FFVoice_Cancel();
#else
    static void FFVoice_Init() { }
    static int FFVoice_Supported() { return 0; }
    static int FFVoice_TtsSupported() { return 0; }
    static void FFVoice_SetFake(string p) { }
    static int FFVoice_Start(string lang) { return 0; }
    static void FFVoice_Stop() { }
    static string FFVoice_Poll() { return ""; }
    static int FFVoice_Speak(string text, float pitch, float rate, int vidx, float vol) { return 0; }
    static void FFVoice_Cancel() { }
#endif
    static bool inited;
    static void Init() { if (inited) return; inited = true; try { FFVoice_Init(); Debug.Log("VOICE66 init: recognition " + Supported + " tts " + Tts); } catch (System.Exception e) { Debug.LogWarning("VOICE66 init failed " + e.Message); } }
    public static bool Supported { get { Init(); try { return FFVoice_Supported() != 0; } catch { return false; } } }
    public static bool Tts { get { Init(); try { return FFVoice_TtsSupported() != 0; } catch { return false; } } }
    public static void SetFake(string p) { Init(); try { FFVoice_SetFake(p); } catch { } }
    public static bool Start() { Init(); try { return FFVoice_Start("en-US") != 0; } catch { return false; } }
    public static void Stop() { try { FFVoice_Stop(); } catch { } }
    public static string Poll() { if (!inited) return ""; try { return FFVoice_Poll() ?? ""; } catch { return ""; } }
    public static int Speak(string t, float pitch, float rate, int v, float vol) { Init(); try { return FFVoice_Speak(t, pitch, rate, v, vol); } catch { return 0; } }
    public static void Cancel() { try { FFVoice_Cancel(); } catch { } }

    // settings (phone strip)
    public static bool VoiceOn = PlayerPrefs.GetInt("ff6v_voice", 1) != 0, ChatterOn = PlayerPrefs.GetInt("ff6v_chat", 1) != 0;
    public static int VolStep = Mathf.Clamp(PlayerPrefs.GetInt("ff6v_vol", 0), 0, 2);
    public static readonly float[] Vols = { 1f, 0.6f, 0.3f };
    public static void Save() { PlayerPrefs.SetInt("ff6v_voice", VoiceOn ? 1 : 0); PlayerPrefs.SetInt("ff6v_chat", ChatterOn ? 1 : 0); PlayerPrefs.SetInt("ff6v_vol", VolStep); PlayerPrefs.Save(); }

    static AudioClip radio;
    // procedural walkie-talkie static (no asset): 0.16 s band-limited crackle with a click on top
    public static AudioClip Radio
    {
        get
        {
            if (radio != null) return radio;
            int sr = 22050, n = sr * 16 / 100; var d = new float[n]; var rng = new System.Random(66); float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (x - lp) * 0.45f; hp = lp - prev; prev = lp;
                float env = Mathf.Min(1f, i / (sr * 0.004f)) * Mathf.Exp(-i / (sr * 0.07f));
                float click = i < sr * 0.006f ? Mathf.Sin(i * 0.9f) * (1f - i / (sr * 0.006f)) : 0f;
                d[i] = Mathf.Clamp((hp * 1.6f + (rng.NextDouble() < 0.01 ? 0.6f : 0f)) * env * 0.55f + click * 0.5f, -1f, 1f);
            }
            radio = AudioClip.Create("RadioStatic", n, 1, sr, false); radio.SetData(d, 0);
            return radio;
        }
    }
}

// ---------------- the brain: words -> what to do / say (local, no network) ----------------
public static class VoiceBrain
{
    public struct Intent { public string kind, act, key; public int robot; public bool all; public Frog who; public int n; }
    struct K { public string[] keys; public string kind, act; public K(string kind, string act, params string[] keys) { this.kind = kind; this.act = act; this.keys = keys; } }

    static readonly string[] Ids = { "optimus", "unitree", "figure03", "figure02", "figure02big", "atlas_hd", "atlas_el" };
    static readonly string[][] NameKeys = {
        new[] { "optimus", "optimist", "optimum", "optimous", "opti", "octopus", "optimus prime" },
        new[] { "unitree", "unit tree", "uni tree", "you need tree", "you knee tree", "unitary", "unity", "you and tree", "eunice tree" },
        new[] { "figure 3", "figure three", "figure 03", "figure free", "figure tree", "fig 3", "figure o 3", "figure oh three" },
        new[] { "figure 2", "figure two", "figure to", "figure too", "figure 02", "fig 2", "figure oh two" },
        new[] { "big figure two", "big figure 2", "big figure", "big guy", "biggie", "big robot", "big one" },
        new[] { "atlas hd", "atlas h d", "atlas", "atlas hdd", "at last" },
        new[] { "atlas electric", "electric atlas", "electric", "atlas e" },
    };
    static readonly string[] AllKeys = { "all robots", "all of you", "everyone", "everybody", "all the robots", "robots", "you guys", "team" };

    static readonly K[] Table = {
        // queries (answered from live state)
        new K("where", "", "where are you", "where r you", "where you at", "where are ya", "wheres", "where is", "your location", "where"),
        new K("doing", "", "what are you doing", "what you doing", "whatcha doing", "watcha doing", "what are you up to", "whats up", "wassup", "are you busy", "doing"),
        new K("mission", "", "hows the mission", "how is the mission", "mission status", "how is it going", "hows it going", "how is your mission", "are you done", "eta", "how long", "when will you be back", "when are you coming back", "how much longer", "progress", "mission", "status"),
        new K("see", "", "what do you see", "what can you see", "whats around", "look around", "what does it look like", "see"),
        new K("battery", "", "battery", "how much power", "your power", "how much juice", "are you tired", "energy"),
        new K("howare", "", "how are you", "are you ok", "are you okay", "how do you feel", "you ok"),
        new K("hello", "", "hello", "hi", "hey", "howdy", "good morning", "good afternoon", "yo", "hiya", "greetings"),
        new K("yes", "", "yes", "yeah", "yep", "okay", "ok", "sure", "yes please", "lets do it", "lets go", "uh huh"),
        new K("love", "", "i love you", "love you", "best friend", "youre my best friend", "i like you", "love"),
        new K("praise", "", "good job", "great job", "well done", "good robot", "thank you", "thanks", "awesome", "youre awesome", "nice work", "good boy", "good girl", "amazing", "cool"),
        new K("joke", "", "tell me a joke", "joke", "funny", "make me laugh", "something funny"),
        new K("name", "", "who are you", "whats your name", "what is your name", "your name"),
        new K("sing", "", "sing", "song", "sing a song"),
        new K("quiet", "", "be quiet", "shh", "shhh", "shush", "stop talking", "quiet", "hush"),
        new K("help", "", "help", "what can you do", "what can i say", "commands", "what do i say"),
        new K("goodnight", "", "good night", "goodnight", "bedtime", "night night"),
        // worlds you can only call a robot to (they arrive by pod / door when you are there)
        new K("goworld", "Moon", "go to the moon", "the moon", "moon"),
        new K("goworld", "House", "go to the house", "go inside", "go in the house", "house"),
        new K("goworld", "Space", "go to space", "outer space", "space"),
        new K("goworld", "RealRoom", "real room"),
        // missions
        new K("act", "mis:0", "go to mars", "fly to mars", "mars rocks", "get mars rocks", "rock run", "mars", "red planet", "rocks", "space rocks"),
        new K("act", "mis:1:4", "go to callisto", "callisto", "ice run", "get ice", "jupiter", "ice"),
        new K("act", "mmis:0:4", "mech to mars", "mech mission", "mech rocks", "fly the mech"),
        new K("act", "mmis:1:3", "mech to callisto"),
        new K("act", "mmis:2:0", "space patrol", "patrol", "mech patrol"),
        new K("act", "recall", "recall mech", "recall the mech", "bring the mech home"),
        new K("comehome", "", "come home", "come back", "abort", "abort mission", "cancel the mission", "cancel mission", "stop the mission", "return to base", "come back home", "head home"),
        new K("act", "rocket", "fly your rocket", "your rocket", "rocket"),
        new K("act", "stash", "stash", "how much stuff", "materials"),
        new K("act", "feed", "show me", "your camera", "video", "watch you", "live feed", "camera"),
        // orders
        new K("act", "come", "come here", "come to me", "get over here", "over here", "here boy", "come"),
        new K("act", "stop", "stop", "freeze", "halt", "stop it", "hold on", "wait"),
        new K("act", "charge", "go charge", "charge", "charger", "recharge", "plug in", "charging"),
        new K("act", "mow:ride", "mow the lawn", "mow", "mower", "lawn", "cut the grass"),
        new K("act", "chore:0", "sweep", "sweep the porch", "porch"),
        new K("act", "chore:1", "vacuum", "vacuum the garage", "garage"),
        new K("act", "chore:2", "crates", "haul crates", "boxes"),
        new K("act", "chore:3", "hay", "haul hay", "barn"),
        new K("act", "chore:4", "litter", "trash", "garbage", "pick up litter", "clean up"),
        new K("act", "chore:6", "rake", "leaves", "rake the leaves"),
        new K("act", "chore:7", "water the flowers", "flowers", "garden"),
        new K("act", "auto", "auto chores", "do your chores", "do chores", "chores", "back to work", "go to work"),
        new K("act", "roofHeli", "roof", "helipad", "go to the roof"),
        new K("act", "roofDrone", "drone pad"),
        new K("act", "drive", "let me drive", "drive you", "i want to drive", "drive it", "control you"),
        // PLAY verbs (World/RobotVisit54.cs)
        new K("act", "play:follow", "follow me", "follow", "come with me", "tag along", "stay with me"),
        new K("act", "play:tag", "play tag", "tag", "youre it", "you are it", "chase me", "catch me"),
        new K("act", "play:hop", "hop", "bounce", "jump", "jumping", "hopping"),
        new K("act", "play:swim", "swim", "swimming", "go swimming", "go in the pond", "pond", "cannonball"),
        new K("act", "play:dive", "dive", "diving", "pearls", "pearl", "dive for pearls"),
        new K("act", "play:climb", "climb", "climb a tree", "climbing", "tree"),
        new K("act", "play:horse", "horse", "ride a horse", "horsey", "pony", "giddy up"),
        new K("act", "play:skate", "skate", "skateboard", "skateboarding", "skating"),
        new K("act", "play:bike", "bike", "bicycle", "ride a bike", "biking", "motorbike"),
        new K("act", "play:ridewith", "ride with me", "get in", "hop in", "ride along", "come for a ride", "car ride", "ride in my car"),
        new K("act", "play:catch", "play catch", "catch", "throw", "ball"),
        new K("act", "play:hunt", "hunt", "hunting", "bow", "arrow", "arrows", "bow and arrow", "archery"),
        new K("act", "play:cook", "cook", "cooking", "eat", "hungry", "food", "dinner", "lunch", "snack", "campfire"),
        new K("act", "play:swing", "push my swing", "push me", "swing", "swings"),
        new K("act", "play:swingsit", "sit with me on the swing", "sit on the swing with me", "sit by me on the swing", "sit next to me on the swing", "come sit by me", "come sit with me", "sit with me", "sit by me", "swing with me", "swing together", "come to the swing", "come to the swings"),   // ffu6r
        new K("act", "play:hamwith", "swing on the hammock", "swing in the hammock", "come to the hammock", "come to the hammocks", "hammock with me", "hammocks with me", "rock in the hammock", "lie in the hammock with me", "hammocks", "rock with me"),
        new K("act", "play:hammock", "hammock", "nap", "take a nap", "sleep", "rest"),
        new K("act", "play:collect", "collect", "collect stuff", "gather", "samples", "crystals", "treasure"),
        new K("act", "play:dance", "dance", "dancing", "boogie", "disco", "party", "dance party"),
        new K("act", "play:piggy", "piggyback", "piggy back", "piggy", "carry me", "pick me up"),
        new K("act", "play:build", "build", "build a castle", "castle", "sandcastle", "sand castle", "block tower", "tower", "build something"),
        new K("act", "play:race", "race", "race me", "race you", "lets race", "run fast", "whos faster"),
        new K("act", "play:wave", "wave", "wave hi", "say hi", "wave at me"),
        new K("act", "play:lights", "lights", "light", "turn on the lights", "turn off the lights", "lamp"),
        new K("act", "play:fort", "tree fort", "fort", "treehouse", "tree house", "build the fort"),
        new K("act", "play:bugs", "bug hunt", "bugs", "bug", "insects", "butterflies", "beetles", "catch bugs"),
        new K("act", "play:pets", "pet the animals", "feed the animals", "animals", "feed", "pet", "pets", "kitty", "puppy"),
        new K("act", "play:ridepet", "ride a cow", "cow", "moo", "ride the cow"),
        new K("act", "play:fetch", "fetch", "dogs", "doggy", "doggies", "throw the stick", "stick", "play with the dogs"),
        new K("act", "play:stay", "stay here", "stay", "sit down", "dont move", "wait here"),
        new K("act", "play:home", "go home", "go back home", "home"),
    };

    public static string Norm(string s)
    {
        s = (s ?? "").ToLowerInvariant().Replace("'", "").Replace("\u2019", "");
        s = Regex.Replace(s, "[^a-z0-9 ]", " ");
        s = Regex.Replace(s, "([a-z])([0-9])", "$1 $2");
        s = Regex.Replace(s, "\\b0([0-9])\\b", "$1");
        return " " + Regex.Replace(s, "\\s+", " ").Trim() + " ";
    }

    static int Lev(string a, string b)
    {
        if (Mathf.Abs(a.Length - b.Length) > 2) return 9;
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++) for (int j = 1; j <= b.Length; j++)
            d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    // exact phrase match, or a misheard single word (edit distance 1, 2 for long words)
    static bool fuzzy;
    static bool Has(string t, string key, out string hit)
    {
        fuzzy = false;
        hit = key;
        if (t.Contains(" " + key + " ")) return true;
        if (key.Length < 5 || key.Contains(" ")) return false;
        foreach (var w in t.Trim().Split(' '))
            if (w.Length >= 4 && Lev(w, key) <= (key.Length >= 8 ? 2 : 1)) { hit = w; fuzzy = true; return true; }
        return false;
    }

    static readonly string[] NumW = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
    static int Number(string t)
    {
        var m = Regex.Match(t, " ([0-9]{1,2}) ");
        if (m.Success) return int.Parse(m.Groups[1].Value);
        for (int i = 1; i < NumW.Length; i++) if (t.Contains(" " + NumW[i] + " ")) return i;
        return -1;
    }

    public static Intent Parse(string said, int selRobot, Frog speaker)
    {
        var it = new Intent { kind = "unknown", act = "", robot = selRobot, n = -1 };
        string t = Norm(said);
        if (t.Trim().Length == 0) { it.kind = "empty"; return it; }
        // which robot (longest name wins; the words are removed so "unit tree" is not "climb a tree")
        int best = -1; string bestKey = null;
        for (int r = 0; r < NameKeys.Length; r++) foreach (var k in NameKeys[r]) { string h; if (Has(t, k, out h) && (bestKey == null || h.Length > bestKey.Length)) { best = r; bestKey = h; } }
        if (best >= 0 && RanchLife.I != null)
        {
            int idx = RanchLife.I.robots.FindIndex(x => x.id == Ids[best]);
            if (idx >= 0) it.robot = idx;
            t = t.Replace(" " + bestKey + " ", " ");
        }
        foreach (var k in AllKeys) if (t.Contains(" " + k + " ")) { it.all = true; t = t.Replace(" " + k + " ", " "); break; }
        // "help <froggy>"
        if (Game.I != null)
            foreach (var fr in Game.I.frogs)
                if (fr != null && !string.IsNullOrEmpty(fr.nick) && (t.Contains(" help " + fr.nick.ToLowerInvariant() + " ") || t.Contains(" go to " + fr.nick.ToLowerInvariant() + " ")))
                { it.kind = "helpfrog"; it.who = fr; it.key = "help " + fr.nick; return it; }
        if (t.Contains(" help me ")) { it.kind = "act"; it.act = "come"; it.key = "help me"; return it; }
        int score = 0;
        foreach (var e in Table)
            foreach (var k in e.keys)
            {
                string h;
                if (!Has(t, k, out h)) continue;
                int s = k.Length * 10 + (e.kind == "act" ? 0 : 1) - (fuzzy ? 8 : 0);   // an exact word beats a misheard one ("horse" is not "house")
                if (s > score) { score = s; it.kind = e.kind; it.act = e.act; it.key = k; }
            }
        it.n = Number(t);
        if (it.act == "mis:0") it.act = "mis:0:" + (it.n == 5 ? 5 : 3);   // the phone has x3 and x5
        if (it.kind == "unknown" && best >= 0) it.kind = "hello";          // just the robot's name
        return it;
    }

    // ---------- personalities ----------
    public static int Ix(Robot r) { return r == null ? 0 : Mathf.Max(0, System.Array.IndexOf(Ids, r.id)); }
    static readonly string[] Catch = { "Affirmative!", "Beep boop, okay!", "Ribbit! Let's go!", "Roger that!", "BIG okay!", "On it!", "Zip zap, on it!" };
    // ffu65 (Bill 2026-10-20): every robot voice is MALE + MONOTONE (flat 0.1..0.4 pitch, even rate) so a robot never sounds like a
    // human or a froggy; only small per-robot differences. The jslib picks a male voice and plays a short beep before each line.
    static readonly float[] Pitch = { 0.25f, 0.35f, 0.3f, 0.2f, 0.1f, 0.28f, 0.4f };
    static readonly float[] Rate = { 0.9f, 0.95f, 0.92f, 0.9f, 0.86f, 0.92f, 0.95f };
    public static float PitchOf(Robot r) { return Pitch[Ix(r)]; }
    public static float RateOf(Robot r) { return Rate[Ix(r)]; }
    public static string CatchOf(Robot r) { return Catch[Ix(r)]; }
    static readonly string[] Jokes = {
        "Why did the robot go to the pond? To get a little hop-grade!",
        "What do you call a frog with no hat? A hat-less ribbit!",
        "Why was the robot so tired? It had a hard drive!",
        "What does a robot frog say? Rib-bot! Rib-bot!",
        "Why don't robots get scared? They have nerves of steel!",
        "What did the frog order at the cafe? French flies and a diet croak!",
        "How do robots eat salsa? With micro-chips!",
        "Why did the froggy take the bus? His car got toad!",
    };
    static int jokeI;

    // ---------- replies from live state ----------
    public static string Dir(Frog f, Vector3 p)
    {
        Vector3 d = p - f.transform.position; d.y = 0f;
        float m = d.magnitude;
        if (m < 3f) return "right next to you";
        Vector3 fw = f.transform.forward; fw.y = 0f; if (fw.sqrMagnitude < 0.01f) fw = Vector3.forward;
        float a = Vector3.SignedAngle(fw.normalized, d / m, Vector3.up);
        string side = Mathf.Abs(a) < 45f ? "in front of you" : Mathf.Abs(a) > 135f ? "behind you" : a > 0f ? "to your right" : "to your left";
        return "about " + Mathf.RoundToInt(m) + " meters " + side;
    }

    static string WorldSay(WorldId w) { return w == WorldId.Ranch || w == WorldId.Underwater ? "at the ranch" : w == WorldId.House ? "in the house" : w == WorldId.RealRoom ? "in the Real Room" : w == WorldId.Space ? "up in space" : "on " + w; }

    static readonly float[] PhaseSec = { 10f, 3f, 3f, 12f, 22f, 9f, 3f, 0f, 8f, 3f, 3f, 8f, 22f, 10f, 4f, 4f };
    public static void MissionEta(RobotMission m, out int pct, out int eta)
    {
        float total = 0f, done = 0f;
        for (int i = 0; i < PhaseSec.Length; i++)
        {
            float s = i == 7 ? 8f * Mathf.Max(1, m.want) : PhaseSec[i];
            total += s;
            if (i < m.phase) done += s; else if (i == m.phase) done += i == 7 ? 8f * m.got : s * 0.5f;
        }
        pct = Mathf.Clamp(Mathf.RoundToInt(done / total * 100f), 0, 99);
        eta = Mathf.Max(5, Mathf.RoundToInt((total - done) / 5f) * 5);
    }
    static string Secs(int s) { return s >= 90 ? "about " + Mathf.RoundToInt(s / 60f) + " minutes" : "about " + s + " seconds"; }

    public static string MissionLine(Robot r)
    {
        if (r.mission != null)
        {
            int pct, eta; MissionEta(r.mission, out pct, out eta);
            string s = "My " + r.mission.Dest + " mission is " + pct + " percent done - I'm " + r.mission.PhaseLine + ". ";
            s += r.mission.got > 0 ? "I've got " + r.mission.got + " of " + r.mission.want + " " + r.mission.Unit + ". " : "";
            s += r.mission.phase >= 15 ? "Almost home!" : "Back in " + Secs(eta) + "!";
            return s;
        }
        var mm = RobotMechMission.For(r);
        if (mm != null) return "Mech mission going great! I'm " + mm.PhaseLine + ".";
        if (r.hunt != null) return "I'm out hunting with my bow - " + r.hunt.Line + ".";
        if (RanchLife.I != null)
            foreach (var o in RanchLife.I.robots)
                if (o != r && (o.mission != null || RobotMechMission.For(o) != null))
                    return "I'm not on a mission, but " + o.robotName + " is - " + (o.mission != null ? o.mission.PhaseLine : RobotMechMission.For(o).PhaseLine) + ".";
        return "No mission right now. Want me to go get Mars rocks? Just say go to Mars!";
    }

    public static string SeeLine(Robot r, Frog f)
    {
        var m = r.mission;
        if (m != null)
        {
            if (m.phase >= 5 && m.phase <= 8) return m.kind == 0 ? "Red rocks and dusty hills everywhere! Mars is so red!" : "Ice everywhere, and giant Jupiter in the sky!";
            if (m.phase == 4 || m.phase == 12) return "Stars and planets zooming by my window!";
            return "I see the mission ship. It's so big!";
        }
        string near = "";
        float bd = 30f * 30f;
        if (Game.I != null)
            foreach (var o in Game.I.frogs)
            {
                if (o == null || !o.gameObject.activeInHierarchy || o.world != r.world && !(o.world == WorldId.Underwater && r.world == WorldId.Ranch)) continue;
                float d = (o.transform.position - r.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; near = o == f ? "you, " + o.nick + "! Hi!" : o.nick + " the froggy"; }
            }
        string place;
        switch (r.world)
        {
            case WorldId.Mars: place = "red dust and big rocks"; break;
            case WorldId.Callisto: place = "ice and Jupiter way up high"; break;
            case WorldId.Moon: place = "grey craters, and the Earth up in the black sky"; break;
            case WorldId.Space: place = "stars all around"; break;
            case WorldId.House: place = "the cozy living room"; break;
            case WorldId.RealRoom: place = "a super real room"; break;
            default: place = "the grass, the pond and the big barn"; break;
        }
        return near.Length > 0 ? "I see " + near + " And " + place + "." : "I see " + place + ".";
    }

    public static string Answer(Intent it, Robot r, Frog f)
    {
        string nick = f != null ? f.nick : "friend";
        WorldId uw = Robot.TargetWorld(f), rw = r.world == WorldId.Underwater ? WorldId.Ranch : r.world;
        switch (it.kind)
        {
            case "where":
                if (r.mission != null) return "I'm on my mission, " + r.mission.PhaseLine + "!";
                if (rw != uw) return "I'm " + WorldSay(rw) + ", " + r.StatusLine + ". Call me and I'll come to you!";
                return "I'm " + Dir(f, r.transform.position) + ". " + (r.Visiting ? "Playing with you!" : "I'm " + r.StatusLine + ".");
            case "doing":
                return r.manual != null ? "You're driving me! Wheee!" : r.Visiting && r.visit.ph == 1 ? "Hanging out with you, " + nick + "! " + "I'm " + r.StatusLine + "." : "Right now I'm " + r.StatusLine + ".";
            case "mission": return MissionLine(r);
            case "see": return SeeLine(r, f);
            case "battery": return "My battery is " + r.Pct + (r.Charging ? " and I'm charging. Yum, electrons!" : r.battery < r.lowAt ? ". Getting low - say go charge!" : ". Plenty of zip!");
            case "howare": return r.battery < r.lowAt ? "A little sleepy - battery " + r.Pct + ". I should charge soon!" : "I feel super! Battery " + r.Pct + ", and I'm " + r.StatusLine + ".";
            case "hello": return Fig03Free.Is(r) ? "Hiii " + nick + "! Ribbit! It's me, Figure 03! Wanna play?" : "Hi " + nick + "! It's me, " + r.robotName + ". What should we do?";
            case "love": return "Aww! I love you too, " + nick + "! My circuits are all warm and fuzzy!";
            case "yes": return Fig03Free.Is(r) ? "Ribbit! Yay! What do you wanna play?" : "Okay! What should I do?";
            case "praise": return "Thank you, " + nick + "! You're the best froggy ever!";
            case "joke": return Jokes[(jokeI++) % Jokes.Length];
            case "name": return "I'm " + r.robotName + (r.owner.Length > 0 ? ", " + r.owner + "'s robot" : ", a ranch robot") + ". I'm " + r.height.ToString("0.0") + " meters tall!";
            case "sing": return "La la la, beep beep boop! Froggies hop and robots bop! La la la!";
            case "quiet": return "Okay, shhh! Whisper mode.";
            case "goodnight": return "Good night, " + nick + "! Sleep tight, don't let the space bugs bite!";
            case "help": return "You can say: come here, follow me, play tag, go to Mars, how's the mission, where are you, or tell me a joke!";
            case "empty": return "I didn't hear anything! Hold the talk button and speak.";
            case "goworld":
                return "Hop over to " + Robot.WorldName(WorldFrom(it.act)) + " first, then call me - I'll come " + (it.act == "Moon" || it.act == "Space" ? "in a drop pod!" : "through the door!");
        }
        return "Beep? I didn't get that. Try: come here, follow me, play tag, or how's the mission?";
    }
    public static WorldId WorldFrom(string s) { try { return (WorldId)System.Enum.Parse(typeof(WorldId), s); } catch { return WorldId.Ranch; } }

    // strip "Unitree: " / "Unitree " from a phone line so the robot doesn't say its own name first
    public static string Clean(Robot r, string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Trim();
        if (s.StartsWith(r.robotName + ":")) s = s.Substring(r.robotName.Length + 1).Trim();
        else if (s.StartsWith(r.robotName + " ")) s = "I" + s.Substring(r.robotName.Length).Replace(" is ", " am ").Replace(" isn't ", " am not ");
        return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
    }
}

// ---------------- the phone side: TALK button, captions, quick replies, settings ----------------
public partial class RobotPhone
{
    public static RobotPhone Talking;               // one mic at a time (split-screen)
    Image micBtn; Text micT, micKey, youT, botT;
    readonly List<Image> chips = new List<Image>(), setChips = new List<Image>();
    readonly List<Text> setLbls = new List<Text>();
    static readonly string[] ChipSay = { "Come here", "Where are you?", "How's the mission?", "Follow me", "Tell me a joke" };
    bool listening, holdMode, pttWas; float listenT; string heardI = "", heardF = "";
    public string lastHeard = "", lastKind = "", lastAct = "", lastReply = ""; public int heardCount;
    static readonly Dictionary<Robot, Frog> caller = new Dictionary<Robot, Frog>();
    public static Frog CallerOf(Robot r) { Frog f; return r != null && caller.TryGetValue(r, out f) ? f : null; }

    void BuildVoice(Transform p)
    {
        float cx = 96f + (PW - 110f) * 0.5f;
        micBtn = UIK.Img(p, UIK.Circle, new Color(0.12f, 0.55f, 0.5f, 0.95f), new Vector2(0f, 0f), new Vector2(50f, 134f), new Vector2(64f, 64f));
        micT = UIK.Label(micBtn.transform, "TALK", 15, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(64f, 22f), Color.white);
        UIK.Modernize(micT, true, 0.5f);
        micKey = UIK.Label(micBtn.transform, "", 10, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(64f, 16f), new Color(1f, 0.93f, 0.6f));
        youT = UIK.Label(p, "Talk to your robot - even on a mission!", 13, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(cx, 154f), new Vector2(PW - 110f, 22f), new Color(0.62f, 0.86f, 1f));
        youT.horizontalOverflow = HorizontalWrapMode.Wrap; youT.resizeTextForBestFit = true; youT.resizeTextMinSize = 9; youT.resizeTextMaxSize = 13;
        botT = UIK.Label(p, "", 14, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(cx, 120f), new Vector2(PW - 110f, 40f), new Color(0.6f, 1f, 0.62f));
        botT.horizontalOverflow = HorizontalWrapMode.Wrap; botT.verticalOverflow = VerticalWrapMode.Truncate; botT.resizeTextForBestFit = true; botT.resizeTextMinSize = 9; botT.resizeTextMaxSize = 14;
        for (int i = 0; i < ChipSay.Length; i++)
        {
            var c = UIK.Img(p, UIK.Round, new Color(0.25f, 0.3f, 0.42f, 0.85f), new Vector2(0.5f, 0f), new Vector2((i - 2) * 84f, 86f), new Vector2(80f, 22f)); c.type = Image.Type.Sliced;
            var l = UIK.Label(c.transform, ChipSay[i], 11, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 20f), Color.white);
            l.resizeTextForBestFit = true; l.resizeTextMinSize = 8; l.resizeTextMaxSize = 11;
            chips.Add(c);
        }
        for (int i = 0; i < 3; i++)
        {
            var c = UIK.Img(p, UIK.Round, new Color(1f, 1f, 1f, 0.1f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 136f, 64f), new Vector2(130f, 18f)); c.type = Image.Type.Sliced;
            var l = UIK.Label(c.transform, "", 10, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128f, 17f), new Color(0.85f, 0.9f, 0.95f));
            setChips.Add(c); setLbls.Add(l);
        }
    }

    // a tap / click on the voice strip; true = eaten
    bool VoicePress(Vector2 p)
    {
        if (user == null) return false;
        if (In(micBtn, p)) { if (listening) VoiceEnd(); else VoiceBegin(user, false); return true; }
        for (int i = 0; i < chips.Count; i++) if (In(chips[i], p)) { Sfx.Play(Sfx.Click, 0.5f, 1.2f); Heard(user, ChipSay[i], "chip"); return true; }
        for (int i = 0; i < setChips.Count; i++)
            if (In(setChips[i], p))
            {
                if (i == 0) { FFVoice.VoiceOn = !FFVoice.VoiceOn; if (!FFVoice.VoiceOn) FFVoice.Cancel(); }
                else if (i == 1) FFVoice.ChatterOn = !FFVoice.ChatterOn;
                else FFVoice.VolStep = (FFVoice.VolStep + 1) % 3;
                FFVoice.Save(); Sfx.Play(Sfx.Click, 0.5f); Debug.Log("VOICE66 settings voice " + FFVoice.VoiceOn + " chatter " + FFVoice.ChatterOn + " vol " + FFVoice.Vols[FFVoice.VolStep]);
                return true;
            }
        return false;
    }

    // pad RT / keyboard T held = push-to-talk (called from Handle while the phone is open)
    void VoicePtt(Frog f, PIn i)
    {
        bool held = f.inputKind == InputKind.Gamepad ? i.gas > 0.55f : f.inputKind == InputKind.Keyboard && !Kb.typing && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tKey.isPressed;
        if (held && !pttWas) VoiceBegin(f, true);
        else if (!held && pttWas && listening && holdMode) VoiceEnd();
        pttWas = held;
    }

    public void VoiceBegin(Frog f, bool hold)
    {
        if (f == null) return;
        if (Talking != null && Talking != this && Talking.listening) { f.Toast("Someone else is talking - one at a time!", 2f); Sfx.Play(Sfx.Click, 0.4f, 0.6f); return; }
        if (!FFVoice.Supported)
        {
            botT.text = "Voice isn't available in this browser - tap a command below!";
            f.Toast("Voice not available here - tap a command", 2.2f); Sfx.Play(Sfx.Click, 0.4f, 0.7f);
            Debug.Log("VOICE66 fallback: no speech recognition in this browser (seat " + (seat + 1) + ")");
            return;
        }
        FFVoice.Cancel();
        if (!FFVoice.Start()) { botT.text = "The mic didn't start - tap a command below!"; return; }
        Talking = this; listening = true; holdMode = hold; listenT = 0f; heardI = ""; heardF = "";
        Robot r = Selected;
        youT.text = "You: (listening...)";
        botT.text = r != null ? "Radio open to " + r.robotName + " - " + (r.mission != null ? "on the " + r.mission.Dest + " mission" : r.StatusLine) : "";
        Sfx.Play(FFVoice.Radio, 0.32f * FFVoice.Vols[FFVoice.VolStep], 1.15f);
        Debug.Log("VOICE66 listen seat " + (seat + 1) + " (" + (hold ? "hold" : "tap") + ") to " + (r != null ? r.robotName : "?"));
    }

    public void VoiceEnd() { if (listening) FFVoice.Stop(); }

    // events from the browser recogniser (polled by RobotChatter for whoever holds the mic)
    public void VoiceEvent(string ev)
    {
        if (ev.Length < 2) return;
        char k = ev[0]; string s = ev.Substring(2);
        if (k == 'I') { heardI = s; youT.text = "You: " + s + "..."; }
        else if (k == 'F') { heardF = (heardF + " " + s).Trim(); youT.text = "You: " + heardF; }
        else if (k == 'E') { Debug.Log("VOICE66 recogniser error " + s); if (s == "not-allowed" || s == "service-not-allowed") botT.text = "The browser said no to the mic - tap a command below!"; }
        else if (k == 'X')
        {
            listening = false; if (Talking == this) Talking = null;
            string said = heardF.Length > 0 ? heardF : heardI;
            if (user != null) Heard(user, said, "voice");
        }
    }

    void TickVoice()
    {
        if (listening) { listenT += Mathf.Min(Time.unscaledDeltaTime, 0.1f);   // a long frame stall must not time the mic out before its words are polled
                          if (listenT > 12f) { FFVoice.Stop(); if (listenT > 15f) VoiceEvent("X:"); } }
        micBtn.color = listening ? new Color(0.9f, 0.2f + 0.15f * Mathf.Sin(Time.unscaledTime * 10f), 0.2f, 0.98f) : FFVoice.Supported ? new Color(0.12f, 0.55f, 0.5f, 0.95f) : new Color(0.3f, 0.32f, 0.36f, 0.9f);
        micT.text = listening ? "LIVE" : "TALK";
        InputKind ik = user != null ? user.inputKind : InputKind.Keyboard;
        string mk = !FFVoice.Supported ? "no mic" : ik == InputKind.Gamepad ? "hold RT" : ik == InputKind.Touch ? "tap" : "hold T";
        if (micKey.text != mk) micKey.text = mk;
        string[] sl = { "VOICE: " + (FFVoice.VoiceOn ? "ON" : "OFF"), "CHATTER: " + (FFVoice.ChatterOn ? "ON" : "OFF"), "VOL " + Mathf.RoundToInt(FFVoice.Vols[FFVoice.VolStep] * 100f) + "%" };
        for (int i = 0; i < 3; i++) if (setLbls[i].text != sl[i]) setLbls[i].text = sl[i];
    }

    // what the player said (voice or a quick-reply chip) -> the same phone order / a spoken answer
    public void Heard(Frog f, string said, string how)
    {
        var robots = RanchLife.I != null ? RanchLife.I.robots : null;
        if (robots == null || robots.Count == 0) return;
        var it = VoiceBrain.Parse(said, sel, f);
        if (it.robot >= 0 && it.robot < robots.Count && it.robot != sel) sel = it.robot;
        Robot r = robots[sel];
        lastHeard = said; lastKind = it.kind; heardCount++;
        youT.text = "You: " + (said.Length > 0 ? said : "...");
        string act = it.act, reply;
        Fig03Free.AskedNicely = VoiceBrain.Norm(said).Contains(" please ");   // ffu6r: Figure 03 may skip chores, less so when asked nicely
        if (it.kind == "yes") { string off = Fig03Free.TakeOffer(r); if (off != null) { it.kind = "act"; act = off; lastKind = it.kind; } }
        if (it.kind == "comehome") act = r.mission != null || RobotMechMission.For(r) != null || r.hunt != null ? "abort" : r.world != Robot.TargetWorld(f) ? "rcall:sel" : "come";
        if (it.kind == "goworld" && VoiceBrain.WorldFrom(it.act) == Robot.TargetWorld(f)) { it.kind = "act"; act = "rcall:sel"; }
        if (it.kind == "act" || it.kind == "comehome")
        {
            if (it.all && (act == "come" || act == "rcall:sel")) { Do(f, "rcall:all"); reply = msg.text; r = robots[sel]; }
            else if (it.all)
            {
                int n = 0; string first = "";
                for (int k = 0; k < robots.Count; k++) { var o = robots[k]; if (o.mission != null || RobotMechMission.For(o) != null) continue; sel = k; Do(f, act); if (n == 0) first = msg.text; n++; }
                sel = it.robot >= 0 ? it.robot : 1 % robots.Count; r = robots[sel];
                reply = n > 0 ? "We're all on it - " + n + " robots! " + VoiceBrain.Clean(robots[sel], first) : "Everyone's busy on missions!";
            }
            else { Do(f, act); reply = Fig03Free.LastDeclined ? VoiceBrain.Clean(r, msg.text) : VoiceBrain.CatchOf(r) + " " + VoiceBrain.Clean(r, msg.text); Fig03Free.LastDeclined = false; }
        }
        else if (it.kind == "helpfrog")
        {
            act = "rcall:help";
            if (it.who == f) { Do(f, "come"); reply = VoiceBrain.CatchOf(r) + " " + VoiceBrain.Clean(r, msg.text); }
            else { string s = r.Call(it.who, null); caller[r] = f; reply = "On my way to help " + it.who.nick + "! " + VoiceBrain.Clean(r, s); }
        }
        else
        {
            reply = VoiceBrain.Answer(it, r, f);
            if (it.kind == "quiet") FFVoice.Cancel();
        }
        lastAct = act; lastReply = reply;
        Debug.Log("VOICE66 heard '" + said + "' (" + how + ", seat " + (seat + 1) + ") -> " + it.kind + (act.Length > 0 ? " " + act : "") + (it.key != null ? " [" + it.key + "]" : "") + " robot " + r.robotName + (it.all ? " ALL" : "") + " -> reply '" + reply + "'");
        Reply(r, reply, it.kind != "quiet");
    }

    public void Reply(Robot r, string text, bool speak)
    {
        botT.text = r.robotName + ": " + text;
        msg.text = r.robotName + ": " + text;
        if (!open && user != null) user.Toast(r.robotName + " on the radio: " + text, 3.5f);
        float vol = FFVoice.Vols[FFVoice.VolStep] * AudioListener.volume;
        Sfx.Play(FFVoice.Radio, 0.25f * FFVoice.Vols[FFVoice.VolStep], 0.95f);
        int ok = speak && FFVoice.VoiceOn ? FFVoice.Speak(text, VoiceBrain.PitchOf(r), VoiceBrain.RateOf(r), VoiceBrain.Ix(r), vol) : 0;
        Debug.Log("VOICE66 reply " + r.robotName + " tts " + ok + " (voice " + FFVoice.VoiceOn + ", tts " + FFVoice.Tts + "): " + text);
    }

    public static void Note(Frog f, Robot r) { if (f != null && r != null) caller[r] = f; }

    // probe: one fake utterance through the real browser path
    public void VoiceDemo(Frog f, string phrase) { FFVoice.SetFake(phrase); VoiceBegin(f, false); }
    public void ChipDemo(int i) { if (user != null) Heard(user, ChipSay[Mathf.Clamp(i, 0, ChipSay.Length - 1)], "chip"); }
}

// ---------------- robots radio in on their own + polls the recogniser ----------------
public class RobotChatter : MonoBehaviour
{
    public static RobotChatter I;
    class St { public string stage = ""; public int got; public bool had; public int haveGot; public string mech = ""; public int visitPh = -1; public Vector3 pos; public float stillT, stuckSaid = -999f, lastSaid = -999f; }
    readonly Dictionary<Robot, St> st = new Dictionary<Robot, St>();
    float lastAny = -999f, tick;
    public static int Said, MissionSaid;

    public static void Ensure() { if (I == null) { I = new GameObject("RobotChatter").AddComponent<RobotChatter>(); DontDestroyOnLoad(I.gameObject); } }

    void Update()
    {
        for (int n = 0; n < 6; n++) { string ev = FFVoice.Poll(); if (ev.Length == 0) break; if (RobotPhone.Talking != null) RobotPhone.Talking.VoiceEvent(ev); }
        tick -= Time.unscaledDeltaTime; if (tick > 0f) return; tick = 0.5f;
        if (RanchLife.I == null || Game.I == null || Game.I.state != Game.State.Play) return;
        float now = Time.unscaledTime;
        foreach (var r in RanchLife.I.robots)
        {
            if (r == null) continue;
            St s; if (!st.TryGetValue(r, out s)) { s = new St { pos = r.transform.position }; st[r] = s; }
            string line = null; bool must = false;
            var m = r.mission;
            if (m != null)
            {
                string stg = m.phase <= 2 ? "prep" : m.phase <= 4 ? "out" : m.phase <= 8 ? "there" : m.phase <= 11 ? "leave" : m.phase == 12 ? "back" : "home";
                if (stg != s.stage)
                {
                    if (stg == "out" && s.stage != "") line = "Blast off! Heading to " + m.Dest + "! Wish me luck!";
                    else if (stg == "there") line = "I landed on " + m.Dest + "! Looking for " + m.Unit + " now.";
                    else if (stg == "back") line = "Got " + m.got + " " + m.Unit + "! Flying home now.";
                    s.stage = stg;
                }
                else if (m.got > s.got && m.phase == 7) line = m.got >= m.want ? "That's all " + m.want + "! Heading back to the ship." : "Found one! That's " + m.got + " of " + m.want + ".";
                s.got = m.got; s.had = true; s.haveGot = m.got;
            }
            else if (s.had) { line = s.stage == "prep" ? "Okay, mission cancelled! I'm staying at the ranch." : s.haveGot > 0 ? "Mission done! I'm back at the ranch with " + s.haveGot + " goodies!" : "I'm back at the ranch! No goodies this time."; s.had = false; s.stage = ""; s.got = 0; must = true; }
            var mm = RobotMechMission.For(r);
            string mk = mm == null ? "" : mm.PhaseLine.StartsWith("mech landing on") ? "land" : mm.PhaseLine.StartsWith("mech collecting") ? "collect" : mm.PhaseLine.StartsWith("mech flying home") ? "home" : mm.PhaseLine.StartsWith("mech flying to") ? "out" : s.mech == "" ? "prep" : s.mech;
            if (mk != s.mech)
            {
                if (mk == "out") line = "Mech is in space! Flying out now!";
                else if (mk == "land") line = "Mech touchdown! " + mm.PhaseLine + ".";
                else if (mk == "home") line = "Mech heading home! Woo!";
                else if (mk == "" && s.mech != "") { line = "Mech mission done! Parking the mech."; must = true; }
                s.mech = mk;
            }
            int vp = r.visit != null ? r.visit.ph : -1;
            if (vp != s.visitPh) { if (vp == 1 && s.visitPh == 0 && !Fig03Free.FreeVisit(r)) { var c = r.visit.lead; line = "I'm here" + (c != null ? ", " + c.nick : "") + "! What should we play?"; } s.visitPh = vp; }
            // stuck: on the way somewhere but not moving for 20 s
            bool going = m == null && mm == null && r.manual == null && (vp == 0 || r.StatusLine.StartsWith("walking") || r.StatusLine.StartsWith("to "));
            if (going && (r.transform.position - s.pos).sqrMagnitude < 0.16f) s.stillT += 0.5f; else { s.stillT = 0f; s.pos = r.transform.position; }
            if (s.stillT > 20f && now - s.stuckSaid > 90f) { s.stuckSaid = now; s.stillT = 0f; line = "Uh oh, I think I'm stuck! Can you come help me?"; }
            if (line == null || !FFVoice.ChatterOn) continue;
            if (!must && (now - lastAny < 8f || now - s.lastSaid < 15f)) continue;
            if (must && now - lastAny < 3f) continue;
            lastAny = now; s.lastSaid = now; Said++; if (m != null || mm != null) MissionSaid++;
            Frog to = RobotPhone.CallerOf(r);
            if (to == null && Game.I.slots.Count > 0) to = Game.I.frogs[Game.I.slots[0].frog];
            var ph = RobotPhone.Of(to);
            Debug.Log("VOICE66 chatter " + r.robotName + " -> " + (to != null ? to.nick : "?") + ": " + line);
            if (ph != null) { if (ph.user == null) ph.user = to; ph.Reply(r, line, true); }
        }
    }
}

// ---------------- probe: ?ffdemo=1&ffshot=voice66[&voicetest=1|off] ----------------
// P1 opens the phone (Unitree, PLAY) and says each test phrase through the real recogniser path (the jslib fake recogniser
// emits interim + final results); logs "VOICE66 TEST PASS/FAIL", phase lines "FFDEMO voice66 ph N <tag>" for screenshots.
// voicetest=off (no recogniser, like Firefox / Tesla): the TALK press shows the fallback, then the quick-reply chips run.
[DefaultExecutionOrder(100)]
public class Ffu6q : MonoBehaviour
{
    static Ffu6q I;
    bool started, waiting; int step = -1, pass, fail; float t0, stepT, sentT, waitLog = -99f; Frog f;
    struct T { public string say, kind, act, robot, tag; public T(string s, string k, string a, string r, string tag) { say = s; kind = k; act = a; robot = r; this.tag = tag; } }
    readonly T[] tests = {
        new T("hey unitree come here", "act", "come", "Unitree", "come"),
        new T("unitree can you play tag with me", "act", "play:tag", "Unitree", "tag"),
        new T("what are you doing", "doing", "", "Unitree", "doing"),
        new T("optimus go get five mars rocks", "act", "mis:0:5", "Optimus", "mission"),
        new T("where are you", "where", "", "Optimus", "where"),
        new T("tell me a joke", "joke", "", "Optimus", "joke"),
        new T("I love you", "love", "", "Optimus", "love"),
        new T("figure three lets dance", "act", "play:dance", "Figure 03", "dance"),
        new T("flibber jabber zoo", "unknown", "", "Figure 03", "unknown"),
        new T("figure three mow the lawn", "act", "mow:ride", "Figure 03", "nah"),            // ffu6r: fig03roll=0.05 -> declines + offers fun
        new T("okay", "act", "", "Figure 03", "yesoffer"),                                   // takes his offer
        new T("figure three please mow the lawn", "act", "mow:ride", "Figure 03", "please"),  // fig03roll 0.95 -> accepts
        new T("unit tree follow me", "act", "play:follow", "Unitree", "follow"),
        new T("optimus how's the mission going", "mission", "", "Optimus", "status"),
        new T("optimus come home", "comehome", "abort", "Optimus", "abort"),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null || Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "voice66") return;
        if (Ffu33.Url("v66") == "sit") { Ffu6r.Boot(); return; }
        I = new GameObject("ffu6q").AddComponent<Ffu6q>(); DontDestroyOnLoad(I.gameObject);
    }

    void Ph(string tag) { Debug.Log("FFDEMO voice66 ph " + (step + 1) + " " + tag); }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || RanchLife.I == null || RanchLife.I.robots.Count < 3 || RobotPhone.I == null) return;
        var ph = RobotPhone.Of(f);
        if (!started)
        {
            started = true; t0 = Time.realtimeSinceStartup;
            f = Game.I.frogs[Game.I.slots[0].frog];
            Robot.DtCap = 0.25f; Robot.IdleChoreAfter = 9999f;
            foreach (var r in RanchLife.I.robots) r.battery = 1f;
            RobotPhone.I.DemoOpen(f, 1, "PLAY");
            Debug.Log("FFDEMO voice66 start recognition " + FFVoice.Supported + " tts " + FFVoice.Tts + " frog " + f.nick);
            stepT = Time.realtimeSinceStartup + 4f;
            return;
        }
        ph = RobotPhone.Of(f);
        if (!ph.open) RobotPhone.I.DemoOpen(f, ph.Selected != null ? RanchLife.I.robots.IndexOf(ph.Selected) : 1, "PLAY");
        float now = Time.realtimeSinceStartup;
        if (waiting)
        {
            if (ph.lastHeard != tests[step].say && now - sentT < 45f) return;   // SwiftShader stalls 20+ s on the first TTS / recogniser frame
            Check(tests[step], ph); Ph(tests[step].tag);
            waiting = false; stepT = now + 7f;   // screenshot time for the phase
            return;
        }
        if (now < stepT) return;
        if (!FFVoice.Supported)
        {
            if (step < 0) { step = 0; ph.VoiceBegin(f, false); Ph("fallback"); stepT = now + 6f; return; }
            if (step == 0) { step = 1; ph.ChipDemo(0); Check(new T("Come here", "act", "come", "", "chip"), ph); Ph("chip"); stepT = now + 6f; return; }
            if (step == 1) { step = 2; ph.ChipDemo(2); Check(new T("How's the mission?", "mission", "", "", "chip2"), ph); stepT = now + 4f; return; }
            Finish(); return;
        }
        int next = step + 1;
        if (next == tests.Length - 1 && RobotChatter.MissionSaid == 0 && now - t0 < 480f && RanchLife.I.robots[0].mission != null)
        {
            // before "come home": give the Mars mission time to radio in on its own (chatter)
            stepT = now + 3f;
            if (now - waitLog > 15f) { waitLog = now; Debug.Log("FFDEMO voice66 waiting for chatter, Optimus " + RanchLife.I.robots[0].StatusLine); }
            return;
        }
        if (next >= tests.Length) { Finish(); return; }
        step = next; waiting = true; sentT = now;
        ph.VoiceDemo(f, tests[step].say);
    }

    void Check(T t, RobotPhone ph)
    {
        var r = ph.Selected;
        bool ok = ph.lastKind == t.kind && (t.act.Length == 0 || ph.lastAct == t.act) && (t.robot.Length == 0 || (r != null && r.robotName == t.robot)) && ph.lastReply.Length > 0;
        if (ok) pass++; else fail++;
        Debug.Log("VOICE66 TEST " + (ok ? "PASS" : "FAIL") + " '" + t.say + "' heard '" + ph.lastHeard + "' -> " + ph.lastKind + " " + ph.lastAct + " robot " + (r != null ? r.robotName : "?") + " (want " + t.kind + " " + t.act + " " + t.robot + ") reply '" + ph.lastReply + "'");
    }

    void Finish()
    {
        if (step == 999) return;
        step = 999;
        Debug.Log("VOICE66 RESULT " + pass + "/" + (pass + fail) + " PASS, chatter lines " + RobotChatter.Said + " (mission " + RobotChatter.MissionSaid + "), FIG03 declined " + Fig03Free.Declined + " accepted " + Fig03Free.Accepted);
        Debug.Log("FFDEMO voice66 done");
    }
}

// ---------------- probe: ?ffdemo=1&ffshot=voice66&v66=sit&voicetest=1 (ffu6r swing / hammock with me) ----------------
// James sits on the porch swing nearest the campfire, says "unitree sit with me on the swing" -> Unitree comes, takes the
// next swing, swings in step ("SIT66 ... angle"); then James moves to a hammock and says "come to the hammock".
public class Ffu6r : MonoBehaviour
{
    static Ffu6r I;
    int ph = -1; float t0, phT; Frog f; Robot r; GardenSeat mySeat; int pass, fail;
    public static void Boot() { if (I == null) { I = new GameObject("ffu6r").AddComponent<Ffu6r>(); DontDestroyOnLoad(I.gameObject); } }
    void Log(string tag) { Debug.Log("FFDEMO voice66 ph " + ph + " " + tag); }
    GardenSeat Nearest(GardenSeat.Kind k, Vector3 p) { GardenSeat b = null; float bd = 1e9f; foreach (var s in GardenSeat.Seats) { if (s == null || s.kind != k || s.driver != null) continue; float d = (s.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; b = s; } } return b; }
    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || RanchLife.I == null || RanchLife.I.robots.Count < 2 || RobotPhone.I == null || GardenSeat.Seats.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        if (ph < 0)
        {
            ph = 0; t0 = now; f = Game.I.frogs[Game.I.slots[0].frog]; r = RanchLife.I.robots[1];
            Robot.DtCap = 0.25f; Robot.IdleChoreAfter = 9999f; r.battery = 1f;
            Debug.Log("FFDEMO voice66 sit start, seats " + GardenSeat.Seats.Count + " recognition " + FFVoice.Supported);
            phT = now + 3f; return;
        }
        if (now < phT) return;
        var ph1 = RobotPhone.Of(f);
        var rs = RobotSit66.SeatOf(r);
        switch (ph)
        {
            case 0:   // James onto a swing, phone open, say it
                mySeat = Nearest(GardenSeat.Kind.Swing, Campfire.I != null ? Campfire.I.transform.position : f.transform.position);
                if (mySeat == null) { Debug.Log("SIT66 FAIL no swing"); Done(); return; }
                f.transform.position = mySeat.transform.position + mySeat.transform.forward * 1.2f;
                f.EnterVehicle(mySeat);
                RobotPhone.I.DemoOpen(f, 1, "ORDERS");
                ph1 = RobotPhone.Of(f);
                ph1.VoiceDemo(f, "unitree sit with me on the swing");
                ph = 1; phT = now + 6f; sayT = now; return;
            case 1:   // ffu6r+: wait (<= 45 s) for the phrase to be heard - SwiftShader frame stalls delay recognition
                if (ph1.lastAct != "play:swingsit" && now - sayT < 45f) { phT = now + 1f; return; }
                Check("swing", ph1.lastAct == "play:swingsit");
                ph = 2; phT = now + 2f; return;
            case 2:   // wait for Unitree to sit on the next swing
                if (rs != null && rs.driver != null && rs != mySeat)
                {
                    Check("swing seated next to James on " + rs.name + " (" + (rs.transform.position - mySeat.transform.position).magnitude.ToString("0.0") + " m away)", true);
                    ph = 3; Log("sitswing"); phT = now + 14f; return;
                }
                if (now - t0 > 300f) { Check("swing seated (timeout: " + r.StatusLine + ")", false); ph = 3; }
                phT = now + 2f; return;
            case 3:   // in step?
                if (rs != null) Check("swing in step: James " + mySeat.SwingAngle.ToString("0.0") + " Unitree " + rs.SwingAngle.ToString("0.0"), Mathf.Abs(mySeat.SwingAngle - rs.SwingAngle) < 6f);
                // hammock: James hops out, into the hammock nearest him, says it
                f.ExitVehicle();
                mySeat = Nearest(GardenSeat.Kind.Hammock, f.transform.position);
                if (mySeat == null) { Debug.Log("SIT66 FAIL no hammock"); Done(); return; }
                f.transform.position = mySeat.transform.position + mySeat.transform.forward * 1.2f;
                f.EnterVehicle(mySeat);
                ph1.VoiceDemo(f, "unitree come to the hammock");
                t0 = now; ph = 4; phT = now + 6f; sayT = now; return;
            case 4:
                if (ph1.lastAct != "play:hamwith" && now - sayT < 45f) { phT = now + 1f; return; }
                Check("hammock", ph1.lastAct == "play:hamwith");
                ph = 5; phT = now + 2f; return;
            case 5:
                if (rs != null && rs.driver != null && rs != mySeat && rs.kind == GardenSeat.Kind.Hammock)
                {
                    Check("hammock seated next to James on " + rs.name + " (" + (rs.transform.position - mySeat.transform.position).magnitude.ToString("0.0") + " m away)", true);
                    ph = 6; Log("sitham"); phT = now + 14f; return;
                }
                if (now - t0 > 240f) { Check("hammock seated (timeout: " + r.StatusLine + ")", false); ph = 6; }
                phT = now + 2f; return;
            case 6:
                if (rs != null) Check("hammock in step: James " + mySeat.SwingAngle.ToString("0.0") + " Unitree " + rs.SwingAngle.ToString("0.0"), Mathf.Abs(mySeat.SwingAngle - rs.SwingAngle) < 6f);
                Done(); return;
        }
    }
    float sayT;
    void Check(string what, bool ok) { if (ok) pass++; else fail++; Debug.Log("SIT66 TEST " + (ok ? "PASS" : "FAIL") + " " + what); }
    void Done() { if (ph == 99) return; ph = 99; Debug.Log("SIT66 RESULT " + pass + "/" + (pass + fail) + " PASS"); Debug.Log("FFDEMO voice66 done"); }
}
