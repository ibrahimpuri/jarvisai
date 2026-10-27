// The score and the mix. The music is rendered offline (tools/score/compose.py) as looping stems, and this lays
// them over each other by how things stand, the way a horror score should behave: never announcing itself, always
// a little ahead of the player.
//   bed        the region's own music: the hull's timbers, the dead machinery, the chanting brothers, the drowned
//              organ, the giant's heart, the swallowing throat, the music box running down, the storm on the shore
//   dread      something knows she is there: rises with the tension and with anything that has seen her
//   chase      it is coming: the hunt is close, or a boss is up
//   tide       the drowned choir under the water, swelling as the tide rises in the rooms the sea reaches
//   sanctuary  a Bell Room: the only warm music on the island, the lullaby on a cello
//   title      the title screen
// and under them, the region's ambience bed (the room itself, no music in it). Stingers mark moments: seen, a
// scare, a discovery, a boss, death, the tide turning toward High.
// The mix: music, ambience and stingers have their own buses. Loud things in the world (a scream, a shot) duck the
// music; under water everything goes dull; at the last pip of health the world narrows to the heart.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public partial class Score : Node
{
    static Score inst;
    class Layer { public string key; public AudioStreamPlayer p; public float vol, target, up, down, gain; }
    readonly Dictionary<string, Layer> layers = new();
    readonly List<AudioStreamPlayer> stingers = new();
    int nextSting;
    static int musicBus = -1, ambBus = -1, stingBus = -1;
    static AudioEffectLowPassFilter musicLp, ambLp, stingLp;
    public static float MusicVolume = 0.8f;
    float duck = 1, duckT, chaseHold, under, tense;
    // what the score is doing (tests, and the journal's debug line)
    public static string Bed { get; private set; }
    public static float Target(string key) => inst != null && inst.layers.TryGetValue(key, out var l) && l != null ? l.target : 0;
    public static float Level(string key) => inst != null && inst.layers.TryGetValue(key, out var l) && l != null ? l.vol : 0;
    public static readonly List<string> Stung = new();   // tests: stingers played
    public static bool Active { get; private set; }   // the score is installed (else the old synthesised drone plays)
    bool hadBoss;

    public override void _Ready()
    {
        inst = this;
        if (musicBus < 0)
        {
            musicBus = MakeBus("Music"); ambBus = MakeBus("Ambience"); stingBus = MakeBus("Sting");
            musicLp = new AudioEffectLowPassFilter { CutoffHz = 20000, Resonance = 0.5f };
            AudioServer.AddBusEffect(musicBus, musicLp);
            // loud things in the world duck the music, so a scream is never fighting a chord
            AudioServer.AddBusEffect(musicBus, new AudioEffectCompressor { Sidechain = "World", Threshold = -16, Ratio = 3.5f, AttackUs = 8000, ReleaseMs = 700, Gain = 0 });
            ambLp = new AudioEffectLowPassFilter { CutoffHz = 20000, Resonance = 0.5f };
            AudioServer.AddBusEffect(ambBus, ambLp);
            stingLp = new AudioEffectLowPassFilter { CutoffHz = 20000, Resonance = 0.5f };
            AudioServer.AddBusEffect(stingBus, stingLp);
        }
        for (int i = 0; i < 3; i++) { var p = new AudioStreamPlayer { Bus = "Sting" }; AddChild(p); stingers.Add(p); }
        Active = ResourceLoader.Exists("res://audio/score/dread.ogg");
        SetMusicVolume(MusicVolume);
    }
    static int MakeBus(string name)
    {
        int i = AudioServer.BusCount;
        AudioServer.AddBus(i); AudioServer.SetBusName(i, name); AudioServer.SetBusSend(i, "Master");
        return i;
    }
    public static void SetMusicVolume(float v)
    {
        MusicVolume = v;
        float db = v <= 0.001f ? -80 : Mathf.LinearToDb(v);
        if (musicBus >= 0) { AudioServer.SetBusVolumeDb(musicBus, db); AudioServer.SetBusVolumeDb(stingBus, db); }
    }

    Layer Get(string key, string bus, float up, float down, float gain)
    {
        if (layers.TryGetValue(key, out var l)) return l;
        string path = "res://audio/" + key + ".ogg";
        if (!ResourceLoader.Exists(path)) { layers[key] = null; return null; }
        var s = GD.Load<AudioStream>(path);
        if (s is AudioStreamOggVorbis o) o.Loop = true;
        var p = new AudioStreamPlayer { Stream = s, Bus = bus, VolumeDb = -80 };
        AddChild(p);
        l = new Layer { key = key, p = p, up = up, down = down, gain = gain };
        layers[key] = l;
        return l;
    }
    void Want(string key, string bus, float target, float up, float down, float gain)
    {
        var l = Get(key, bus, up, down, gain);
        if (l != null) l.target = target;
    }

    // ---- what the score should be doing ------------------------------------------------------------------------
    static string RegionKey(RoomDef r) => r.Outdoor ? "outdoor" : r.Region;
    void Decide(float dt)
    {
        foreach (var l in layers.Values) if (l != null) l.target = 0;
        Bed = null;
        if (G.mode == "title") { Want("score/title", "Music", 1, 4, 3, 1); return; }
        var r = G.room; var p = G.player;
        if (G.mode != "play" || r == null || p == null) return;
        string region = RegionKey(r);
        bool bell = p.inBellRoom, dead = p.dead;
        // the hunt: anything hostile that has seen her, and how close it is
        float aware = 0, near = 0;
        foreach (var e in G.ents)
        {
            if (e is not Body b || b.gone || !b.Alive() || b.corpse || b.passive || !b.spotted) continue;
            float d = H.Dist(b.Cx(), b.Cy(), p.Cx(), p.Cy());
            aware = MathF.Max(aware, 1 - Math.Clamp((d - 6 * H.TILE) / (24 * H.TILE), 0, 1));
            if (d < 11 * H.TILE && b.mode is "hunt" or "attack" or "windup" or "grab" or "inhale" or "sweep") near = 1;
        }
        bool boss = G.bossBar != null || G.undertow?.live != null;
        if (G.bossBar != null && !hadBoss) Sting("boss");
        hadBoss = G.bossBar != null;
        if (near > 0 || boss) chaseHold = 4.5f; else chaseHold -= dt;
        tense = Math.Clamp(G.tension * 0.8f + aware * 0.75f, 0, 1);
        float tide = r.Row == 4 || r.Outdoor ? MathF.Pow(Tide.Level01(), 1.5f) : Tide.Level01() * 0.3f;
        if (dead)
        {
            // drowned: the music goes, only the room is left
            Want("amb/" + region, "Ambience", 0.4f, 2, 3, 0.9f);
            return;
        }
        Bed = region;
        float chase = !bell && chaseHold > 0 ? 1 : 0;
        Want("score/bed_" + region, "Music", bell ? 0.18f : 1 - chase * 0.45f, 4, 3, 1);
        Want("amb/" + region, "Ambience", bell ? 0.45f : 1, 3, 3, 0.9f);
        Want("score/dread", "Music", bell ? 0 : MathF.Max(tense, chase * 0.6f), 3, 7, 0.85f);
        Want("score/chase", "Music", chase, 1.2f, 5, 0.9f);
        Want("score/tide", "Music", (bell ? 0.25f : 1) * tide, 5, 5, 0.75f);
        Want("score/sanctuary", "Music", bell ? 1 : 0, 3, 3, 1);
    }

    public override void _Process(double delta)
    {
        float dt = MathF.Min(G.stepDt, 0.1f);   // game time, so a test's fixed step and a real frame fade alike
        Decide(dt);
        if (duckT > 0) { duckT -= dt; duck = MathF.Max(0.35f, duck - dt * 4); } else duck = MathF.Min(1, duck + dt * 0.5f);
        foreach (var l in layers.Values)
        {
            if (l == null) continue;
            float rate = l.target > l.vol ? 1 / l.up : 1 / l.down;
            l.vol = l.vol < l.target ? MathF.Min(l.target, l.vol + dt * rate) : MathF.Max(l.target, l.vol - dt * rate);
            float g = l.vol * l.gain * (l.key.StartsWith("score/") ? duck : 1);
            if (g > 0.001f && !Snd.Muted)
            {
                if (!l.p.Playing) l.p.Play((float)(H.Rand() * Math.Max(0, l.p.Stream.GetLength() - 1)));
                l.p.VolumeDb = Mathf.LinearToDb(g);
            }
            else if (l.p.Playing && l.vol <= 0.001f) l.p.Stop();
        }
        // under water the score and the room go dull; at the last pip the world narrows
        var p = G.player;
        bool headUnder = G.mode == "play" && p != null && !p.dead && p.HeadUnder();
        under += ((headUnder ? 1 : 0) - under) * (1 - MathF.Exp(-dt / 0.3f));
        float hurt = G.mode == "play" && p != null && !p.dead && p.hp <= 1 ? 1 : 0;
        if (musicLp != null)
        {
            float cut = 20000 * MathF.Pow(1100f / 20000, under);
            cut = MathF.Min(cut, hurt > 0 ? 2600 : 20000);
            musicLp.CutoffHz = cut; stingLp.CutoffHz = MathF.Max(cut, 2000);
            ambLp.CutoffHz = 20000 * MathF.Pow(650f / 20000, under);
        }
    }

    // ---- stingers ------------------------------------------------------------------------------------------------
    static readonly Dictionary<string, float> stingCool = new();
    public static void Sting(string id, float vol = 1, float cooldown = 0)
    {
        if (inst == null) return;
        if (cooldown > 0 && stingCool.TryGetValue(id, out var until) && until > G.Now) return;
        if (cooldown > 0) stingCool[id] = G.Now + cooldown;
        Stung.Add(id); if (Stung.Count > 50) Stung.RemoveAt(0);
        if (Snd.Muted) return;
        string path = "res://audio/sting/" + id + ".ogg";
        if (!ResourceLoader.Exists(path)) return;
        var pl = inst.stingers[inst.nextSting++ % inst.stingers.Count];
        pl.Stream = GD.Load<AudioStream>(path);
        pl.VolumeDb = Mathf.LinearToDb(vol);
        pl.Play();
        inst.duckT = id is "scare" or "seen" or "boss" ? 2.5f : 1.5f;
    }
}
