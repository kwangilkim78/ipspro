using System.Drawing.Drawing2D;

namespace IPS_PROBACKOFFICE;

internal sealed class FeeTrendChart : Control
{
    private readonly ToolTip _tip = new();
    private (string Label, decimal Value)[] _data = [];
    private RectangleF[] _bars = [];
    private string _unit = "KRW";
    private string? _message = "조회 후 추세가 표시됩니다.";
    private int _hover = -1;

    public FeeTrendChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Dock = DockStyle.Fill;
        AccessibleName = "수수료 추세 막대그래프";
    }

    public void SetData((string Label, decimal Value)[] data, string unit, string? message = null)
    {
        _data = data;
        _unit = unit;
        _message = message;
        _bars = [];
        _hover = -1;
        _tip.SetToolTip(this, null);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        _bars = [];
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_message is not null || _data.Length == 0 || Width < 220 || Height < 150)
        {
            TextRenderer.DrawText(g, _message ?? "차트 영역을 넓혀 주세요.", Font, ClientRectangle,
                Color.DimGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        double low = Math.Min(0, (double)_data.Min(x => x.Value));
        double high = Math.Max(0, (double)_data.Max(x => x.Value));
        if (high == low) high = low + 1;
        double pad = (high - low) * .1;
        if (low < 0) low -= pad;
        high += pad;
        string Number(double n) => n.ToString("#,##0.##");
        int left = Math.Max(65, Math.Max(TextRenderer.MeasureText(Number(high), Font).Width,
            TextRenderer.MeasureText(Number(low), Font).Width) + 12);
        var plot = new RectangleF(left, 34, Math.Max(20, Width - left - 24), Height - 84);
        using var gridPen = new Pen(Color.FromArgb(229, 234, 240));
        using var baselinePen = new Pen(Color.FromArgb(140, 150, 165));
        using var barBrush = new SolidBrush(Color.FromArgb(45, 112, 175));
        TextRenderer.DrawText(g, _unit, Font, new Point(10, 8), Color.DimGray);
        for (int i = 0; i <= 4; i++)
        {
            float y = plot.Bottom - plot.Height * i / 4;
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            TextRenderer.DrawText(g, Number(low + (high - low) * i / 4), Font,
                new Rectangle(0, (int)y - 10, left - 8, 24), Color.DimGray, TextFormatFlags.Right);
        }
        float zeroY = plot.Bottom - (float)((0 - low) / (high - low)) * plot.Height;
        float slotWidth = plot.Width / _data.Length;
        float barWidth = Math.Min(40, slotWidth * .7f);
        _bars = _data.Select((x, i) =>
        {
            float y = plot.Bottom - (float)(((double)x.Value - low) / (high - low)) * plot.Height;
            return new RectangleF(plot.Left + slotWidth * (i + .5f) - barWidth / 2,
                Math.Min(y, zeroY), barWidth, Math.Abs(y - zeroY));
        }).ToArray();
        g.DrawLine(baselinePen, plot.Left, zeroY, plot.Right, zeroY);
        int stride = Math.Max(1, (int)Math.Ceiling(_data.Length / Math.Max(2.0, plot.Width / 65)));
        for (int i = 0; i < _bars.Length; i++)
        {
            var bar = _bars[i];
            g.FillRectangle(barBrush, bar);
            if (i % stride != 0) continue;
            string label = _data[i].Label.Length == 7 ? _data[i].Label[5..] : _data[i].Label.Substring(5, 5);
            TextRenderer.DrawText(g, label, Font,
                new Rectangle((int)(bar.Left + bar.Width / 2) - 30, (int)plot.Bottom + 12, 60, 24), Color.DimGray,
                TextFormatFlags.HorizontalCenter);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = Array.FindIndex(_bars, bar => e.X >= bar.Left && e.X <= bar.Right
            && e.Y >= bar.Top - 3 && e.Y <= bar.Bottom + 3);
        if (index == _hover) return;
        _hover = index;
        _tip.SetToolTip(this, index < 0 ? null : $"{_data[index].Label}\n{_data[index].Value:N2} {_unit}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
