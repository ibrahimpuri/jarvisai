// Base class for everything that lives in a room: bodies, objects, pickups.
using System;
using System.Collections.Generic;

namespace Hollowmere;

// A light cast this frame: lanterns, lamps, flares, fire.
public class LightSrc
{
    public float x, y, r, i = 1;
    public string color;          // "r,g,b" (null = warm lamp light)
    public bool player;
}

public abstract class Ent : Mover
{
    public string type, key;
    public Props spec;
    public RoomDef room;
    public bool isBody, gone, dead;
    public Loose looseRef;
    public int facing = 1;

    public virtual void Update(float dt) { }
    public virtual void Draw(Canvas c) { }
    public virtual void DrawKnots(Canvas c) { }
    public virtual string Prompt() => null;
    public virtual bool HasPrompt => false;
    public virtual bool Near(Player p) => false;
    public virtual void Interact() { }
    public virtual Props Serialize() => null;
    public virtual void Restore(Props s) { }
    public virtual LightSrc Light() => null;
    public virtual Platform PlatformRect() => null;
    public virtual bool Hears => false;
    public virtual void Hear(float x, float y, float d, float r, string src) { }
    public virtual bool IsCollectible => false;
    public (float x, float y, float w, float h) Box() => (x, y, w, h);
}
