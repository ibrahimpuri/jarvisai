// The menus' moving parts, as Godot controls: the sea behind every menu, the growth over a panel's edges, the look
// under each button, the satchel's pockets, an item turned in the hand, the suit's heartbeat gauge, a Bell Room bell.
// Art is drawn by MenuArt; these decide where and when, and redraw while something in them moves.
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

// Behind every menu: the game dimmed as if seen from under the water, a few shafts of light, motes drifting up, and
// kelp swaying up from the bottom of the screen.
public partial class MenuBackdrop : Control
{
    readonly Canvas cv = new();
    public float Dim = 0.55f;
    public bool Kelp = true, Shafts = true;
    public Color Tint = new(0.01f, 0.03f, 0.04f);
    public MenuBackdrop() { MouseFilter = MouseFilterEnum.Stop; }
    public override void _Process(double d) => QueueRedraw();
    public override void _Draw()
    {
        var c = cv.Begin(this); var sz = Size; float t = MenuArt.Now;
        c.FillRect(0, 0, sz.X, sz.Y, new Color(Tint.R, Tint.G, Tint.B, Dim));
        // the edges of the view darker, as through a helmet glass
        c.Glow(0, 0, sz.X * 0.55f, sz.Y * 0.6f, new Color(0, 0, 0, 0.45f * Dim)); c.Glow(sz.X, 0, sz.X * 0.55f, sz.Y * 0.6f, new Color(0, 0, 0, 0.45f * Dim));
        c.Glow(0, sz.Y, sz.X * 0.55f, sz.Y * 0.6f, new Color(0, 0, 0, 0.6f * Dim)); c.Glow(sz.X, sz.Y, sz.X * 0.55f, sz.Y * 0.6f, new Color(0, 0, 0, 0.6f * Dim));
        if (Shafts)
            for (int i = 0; i < 4; i++)
            {
                float x = sz.X * (0.15f + i * 0.24f) + MathF.Sin(t * 0.13f + i) * 30, w0 = 18 + i * 6, a = 0.035f + 0.015f * MathF.Sin(t * 0.4f + i * 2);
                c.FillPolyGrad(new[] { new Vector2(x, 0), new Vector2(x + w0, 0), new Vector2(x + w0 * 3 - 60, sz.Y), new Vector2(x - 60, sz.Y) },
                    new[] { new Color(0.6f, 0.8f, 0.8f, a), new Color(0.6f, 0.8f, 0.8f, a), new Color(0.6f, 0.8f, 0.8f, 0), new Color(0.6f, 0.8f, 0.8f, 0) });
            }
        // motes, drifting up
        for (int i = 0; i < 46; i++)
        {
            float sp = 4 + TileArt.Hash(i, 5) * 10, x = (TileArt.Hash(i, 3) * sz.X + MathF.Sin(t * 0.5f + i) * 6) % sz.X;
            float y = sz.Y - ((TileArt.Hash(i, 4) * sz.Y + t * sp) % (sz.Y + 20)) + 10;
            float a = 0.15f + 0.25f * TileArt.Hash(i, 6);
            c.Fill = new Color(0.75f, 0.88f, 0.85f, a); c.FillRect(x, y, 0.8f, 0.8f);
        }
        if (Kelp)
        {
            // dark kelp up from the floor at either side, the nearest the darkest
            for (int i = 0; i < 9; i++)
            {
                bool left = i % 2 == 0;
                float x = left ? 6 + TileArt.Hash(i, 9) * 90 : sz.X - 6 - TileArt.Hash(i, 9) * 90;
                float len = 60 + TileArt.Hash(i, 10) * 120;
                MenuArt.Kelp(c, new Vector2(x, sz.Y + 4), -H.PI / 2 + (left ? 0.12f : -0.12f) + (TileArt.Hash(i, 11) - 0.5f) * 0.3f, len, t * 0.6f, i * 17 + 3, 3 + TileArt.Hash(i, 12) * 2, 0.55f + 0.4f * TileArt.Hash(i, 13));
            }
        }
    }
}

// The growth over a panel's edges, in front of what is in it. It sits in the panel as its last child and draws in the
// panel's own coordinates, reaching out past the margins.
public partial class FrameGrowth : Control
{
    readonly Canvas cv = new();
    public int Seed; public float Amount = 1; public bool Drips = true;
    public FrameGrowth() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Process(double d) => QueueRedraw();
    public override void _Draw()
    {
        if (GetParent() is not Control p) return;
        var c = cv.Begin(this);
        c.Translate(-Position.X, -Position.Y);
        MenuArt.Overgrowth(c, p.Size.X, p.Size.Y, Seed, MenuArt.Now, Amount, Drips);
    }
}

// The look under a button's words. It is the button's child, drawn behind it, and reads the button's state.
public partial class ButtonArt : Control
{
    readonly Canvas cv = new();
    public MenuArt.Btn Style; public bool On; public int Seed;
    public Action<Canvas, Button, int> Extra;    // more drawing for a particular button (a card's emblem, a slot's item)
    Button b;
    public ButtonArt() { MouseFilter = MouseFilterEnum.Ignore; ShowBehindParent = true; }
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        b = GetParent<Button>();
        b.FocusEntered += QueueRedraw; b.FocusExited += QueueRedraw; b.MouseEntered += QueueRedraw; b.MouseExited += QueueRedraw;
    }
    public int State => b == null ? 0 : b.ButtonPressed || b.GetDrawMode() == BaseButton.DrawMode.Pressed ? 3 : b.HasFocus() ? 2 : b.IsHovered() ? 1 : 0;
    public override void _Process(double d) { if (b != null && (b.HasFocus() || b.IsHovered() || Extra != null)) QueueRedraw(); }
    public override void _Draw()
    {
        if (b == null) return;
        var c = cv.Begin(this);
        MenuArt.Button(c, Size.X, Size.Y, Style, State, On, b.Disabled, Seed, MenuArt.Now);
        Extra?.Invoke(c, b, State);
    }
}

// An item as the satchel holds it, drawn large, or turning slowly in the hand when examined.
public partial class ItemView : Control
{
    readonly Canvas cv = new();
    public string Id; public bool Turn; public float Glow = 0.5f;
    public ItemView(string id) { Id = id; MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Process(double d) => QueueRedraw();
    public override void _Draw()
    {
        if (Id == null) return;
        var c = cv.Begin(this); var sz = Size; float t = MenuArt.Now;
        c.Glow(sz.X / 2, sz.Y / 2, sz.X * 0.55f, sz.Y * 0.55f, new Color(1, 0.8f, 0.5f, 0.12f * Glow));
        c.Glow(sz.X / 2, sz.Y * 0.85f, sz.X * 0.4f, sz.Y * 0.08f, new Color(0, 0, 0, 0.5f));
        c.Save(); c.Translate(sz.X / 2, sz.Y / 2 + (Turn ? MathF.Sin(t * 1.3f) * 1.5f : 0));
        if (Turn) c.Scale(0.35f + 0.65f * MathF.Abs(MathF.Cos(t * 0.6f)), 1);
        Icons.Draw(c, Id, 0, 0, MathF.Min(sz.X, sz.Y) * 0.8f);
        c.Restore();
    }
}

// The diving suit's heartbeat gauge.
public partial class HeartGauge : Control
{
    readonly Canvas cv = new();
    public HeartGauge() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Process(double d) => QueueRedraw();
    public override void _Draw()
    {
        var p = G.player; if (p == null) return;
        var c = cv.Begin(this);
        MenuArt.Heartbeat(c, 0, 0, Size.X, Size.Y, p.hp / (float)Math.Max(1, p.maxHp), MenuArt.Now);
    }
}

// Free drawing in a menu: a callback with the time, redrawn every frame.
public partial class Painted : Control
{
    readonly Canvas cv = new();
    readonly Action<Canvas, Vector2, float> paint;
    public Painted(Action<Canvas, Vector2, float> paint, Vector2 min) { this.paint = paint; CustomMinimumSize = min; MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Process(double d) => QueueRedraw();
    public override void _Draw() => paint(cv.Begin(this), Size, MenuArt.Now);
}
