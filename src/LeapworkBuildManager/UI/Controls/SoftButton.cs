using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed class SoftButton : Button
    {
        bool hover, pressed;
        public bool SymbolOnly { get; set; }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = false;
            pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            pressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressed = false;
            Invalidate();
        }

        public SoftButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.Button;
            AutoSize = true;
            Padding = new Padding(12, 5, 12, 5);
            Height = LayoutMetrics.ButtonHeight;
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, LayoutMetrics.Gap, 0);
        }

        Color GetBackgroundColor()
        {
            if (!Enabled)
                return Theme.DisabledSurface;
            if (pressed)
                return ControlPaint.Dark(BackColor, 0.15f);
            if (hover)
                return ControlPaint.Light(BackColor, 0.15f);
            return BackColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Color.White : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 12 || Height < 12)
                return;
            using (var outline = new GraphicsPath())
            {
                outline.AddArc(0, 0, 12, 12, 180, 90);
                outline.AddArc(Width - 13, 0, 12, 12, 270, 90);
                outline.AddArc(Width - 13, Height - 13, 12, 12, 0, 90);
                outline.AddArc(0, Height - 13, 12, 12, 90, 90);
                outline.CloseFigure();
                using (var background = new SolidBrush(GetBackgroundColor()))
                    e.Graphics.FillPath(background, outline);
            }

            var textBounds = SymbolOnly ? ClientRectangle : new Rectangle(
                Padding.Left, Padding.Top,
                Math.Max(0, ClientSize.Width - Padding.Horizontal),
                Math.Max(0, ClientSize.Height - Padding.Vertical));
            var textFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            textFlags |= SymbolOnly ? TextFormatFlags.NoPadding : TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds,
                Enabled ? ForeColor : Theme.DisabledText, textFlags);
            if (Focused)
                using (var focusPen = new Pen(Theme.Focus, 2))
                    e.Graphics.DrawRectangle(focusPen, 3, 3, Width - 7, Height - 7);
        }
    }
}
