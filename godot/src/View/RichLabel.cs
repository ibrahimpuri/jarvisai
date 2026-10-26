// A line of text with button glyphs, for menus. It redraws itself when the player changes device.
using System;
using Godot;

namespace Hollowmere;

public partial class RichLabel : Control
{
    readonly Func<string> text;
    readonly int size;
    readonly Color color;
    public int Align;              // 0 left, 1 centre, 2 right
    readonly Canvas cv = new();
    string shown; Glyphs.Style style;
    float minW;
    public float MinWidth { get => minW; set { minW = value; Refresh(); } }

    public RichLabel() { text = () => ""; }
    public RichLabel(Func<string> text, int size, Color color, int align = 0)
    {
        this.text = text; this.size = size; this.color = color; Align = align;
        MouseFilter = MouseFilterEnum.Ignore;
        Refresh();
    }
    string Font => size + "px serif";
    void Refresh()
    {
        shown = text(); style = Glyphs.Current;
        cv.Font = Font;
        float w = Glyphs.Measure(cv, shown, style);
        CustomMinimumSize = new Vector2(MathF.Max(minW, MathF.Ceiling(w)), MathF.Ceiling(size * 2.1f));
        QueueRedraw();
    }
    public override void _Process(double delta)
    {
        if (Glyphs.Current != style || text() != shown) Refresh();
    }
    public override void _Draw()
    {
        var c = cv.Begin(this);
        c.Font = Font; c.Fill = color; c.TextAlign = Align;
        float x = Align == 1 ? Size.X / 2 : Align == 2 ? Size.X : 0;
        Glyphs.DrawText(c, shown, x, Size.Y / 2 + size * 0.36f, 1, style);
    }
}
