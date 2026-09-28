using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public class DpiAwareForm : Form
    {
        readonly Dictionary<Control, Geometry> geometry = new Dictionary<Control, Geometry>();
        float displayScale = 1;
        bool changingDpi;
        protected virtual float LayoutScale { get { return displayScale; } }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            using (var graphics = CreateGraphics())
            {
                float next = graphics.DpiX / 96f;
                float ratio = next / LayoutScale;
                ApplyDisplayScale(next, new Rectangle(Location, new Size(
                    (int)Math.Round(Width * ratio), (int)Math.Round(Height * ratio))));
            }
        }

        // Own WM_DPICHANGED because these forms use manual geometry, not WinForms autoscaling.
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x02E0 && message.LParam != IntPtr.Zero)
            {
                var rect = (NativeRect)Marshal.PtrToStructure(message.LParam, typeof(NativeRect));
                ApplyDisplayScale((message.WParam.ToInt64() & 0xffff) / 96f,
                    Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom));
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }

        internal void ApplyDisplayScale(float next, Rectangle suggestedBounds)
        {
            if (changingDpi || next <= 0 || Single.IsNaN(next) || Single.IsInfinity(next))
                return;
            float previous = LayoutScale;
            if (Math.Abs(next - previous) < 0.001f)
                return;
            changingDpi = true;
            var controls = new List<Control>();
            Collect(this, controls);
            foreach (var control in controls)
                control.SuspendLayout();
            try
            {
                foreach (var control in controls)
                {
                    Geometry saved;
                    if (!geometry.TryGetValue(control, out saved))
                    {
                        saved = new Geometry();
                        geometry.Add(control, saved);
                    }
                    saved.Apply(control, previous, next, control != this);
                }
                displayScale = next;
                Bounds = suggestedBounds;
            }
            finally
            {
                for (int i = controls.Count - 1; i >= 0; i--)
                    controls[i].ResumeLayout(true);
                // Let anchors settle before the form applies its final manual layout.
                OnDisplayScaleChanged(next);
                foreach (var control in controls)
                    geometry[control].Remember(control);
                changingDpi = false;
            }
            var area = Screen.FromRectangle(Bounds).WorkingArea;
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)),
                Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
            Invalidate(true);
        }

        protected virtual void OnDisplayScaleChanged(float scale) { }

        static void Collect(Control parent, List<Control> controls)
        {
            controls.Add(parent);
            foreach (Control child in parent.Controls)
                Collect(child, controls);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect { public int Left, Top, Right, Bottom; }

        sealed class Metric
        {
            double logical;
            double last = Double.NaN;
            public int Scale(double value, float previous, float next)
            {
                if (value != last)
                    logical = value / previous;
                last = Math.Round(logical * next);
                return (int)last;
            }
            public void Remember(double value) { last = value; }
        }

        sealed class Geometry
        {
            readonly Dictionary<string, Metric> values = new Dictionary<string, Metric>();
            Metric Value(string key)
            {
                Metric metric;
                if (!values.TryGetValue(key, out metric))
                {
                    metric = new Metric();
                    values.Add(key, metric);
                }
                return metric;
            }
            int Scale(string key, int value, float oldScale, float newScale)
            {
                return Value(key).Scale(value, oldScale, newScale);
            }
            Padding ScalePadding(string key, Padding p, float oldScale, float newScale)
            {
                return new Padding(Scale(key + "L", p.Left, oldScale, newScale), Scale(key + "T", p.Top, oldScale, newScale),
                    Scale(key + "R", p.Right, oldScale, newScale), Scale(key + "B", p.Bottom, oldScale, newScale));
            }
            public void Apply(Control c, float oldScale, float newScale, bool bounds)
            {
                if (bounds)
                    c.SetBounds(Scale("X", c.Left, oldScale, newScale), Scale("Y", c.Top, oldScale, newScale),
                        Scale("W", c.Width, oldScale, newScale), Scale("H", c.Height, oldScale, newScale));
                c.Padding = ScalePadding("P", c.Padding, oldScale, newScale);
                c.Margin = ScalePadding("M", c.Margin, oldScale, newScale);
                var table = c as TableLayoutPanel;
                if (table != null)
                {
                    for (int i = 0; i < table.RowStyles.Count; i++)
                        if (table.RowStyles[i].SizeType == SizeType.Absolute)
                            table.RowStyles[i].Height = Value("Row" + i).Scale(table.RowStyles[i].Height, oldScale, newScale);
                    for (int i = 0; i < table.ColumnStyles.Count; i++)
                        if (table.ColumnStyles[i].SizeType == SizeType.Absolute)
                            table.ColumnStyles[i].Width = Value("Col" + i).Scale(table.ColumnStyles[i].Width, oldScale, newScale);
                }
                var combo = c as ComboBox;
                if (combo != null) combo.ItemHeight = Math.Max(1, Scale("Item", combo.ItemHeight, oldScale, newScale));
                var list = c as ListBox;
                if (list != null) list.ItemHeight = Math.Max(1, Scale("Item", list.ItemHeight, oldScale, newScale));
            }
            public void Remember(Control c)
            {
                Value("X").Remember(c.Left); Value("Y").Remember(c.Top);
                Value("W").Remember(c.Width); Value("H").Remember(c.Height);
            }
        }
    }
}
