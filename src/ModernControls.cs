// Lightweight, repository-owned WinForms controls. No external UI package required.
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UIModernization
{
    public static class ModernTheme
    {
        public static readonly Color Canvas=Color.FromArgb(18,21,27),Surface=Color.FromArgb(30,34,43),Raised=Color.FromArgb(39,45,57),Border=Color.FromArgb(57,65,80),Text=Color.FromArgb(235,240,248),Muted=Color.FromArgb(166,181,203),Accent=Color.FromArgb(89,166,255),Track=Color.FromArgb(48,57,72);
        internal static GraphicsPath Rounded(RectangleF r,float radius)
        {
            var p=new GraphicsPath();float d=Math.Max(1,Math.Min(radius*2,Math.Min(r.Width,r.Height)));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
    }
    public class ModernCardPanel : FlowLayoutPanel
    {
        Color borderColor=ModernTheme.Border;int borderRadius=12;
        public Color CardBackColor {get{return BackColor;}set{BackColor=value;Invalidate();}}
        public Color BorderColor {get{return borderColor;}set{borderColor=value;Invalidate();}}
        [DefaultValue(12)] public int BorderRadius {get{return borderRadius;}set{borderRadius=Math.Max(0,value);Invalidate();}}
        public ModernCardPanel(){BackColor=ModernTheme.Surface;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint,true);}
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?ModernTheme.Canvas:Parent.BackColor);if(Width<2||Height<2)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=ModernTheme.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),BorderRadius*DeviceDpi/96f))using(var fill=new SolidBrush(CardBackColor))using(var pen=new Pen(BorderColor)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
        }
    }
    public class ModernProgressBar : Control
    {
        float value;bool available=true;Color fill=ModernTheme.Accent,track=ModernTheme.Track;
        public float Value {get{return value;}set{this.value=float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,Math.Min(100,value));Invalidate();}}
        public bool IsAvailable {get{return available;}set{available=value;Invalidate();}}
        public Color FillColor {get{return fill;}set{fill=value;Invalidate();}}
        public Color TrackColor {get{return track;}set{track=value;Invalidate();}}
        public ModernProgressBar(){Size=new Size(220,14);SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint,true);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(Width<2||Height<2)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=ModernTheme.Rounded(new RectangleF(0,0,Width-1,Height-1),Height/2f))
            using(var background=new SolidBrush(TrackColor))
            {e.Graphics.FillPath(background,path);var state=e.Graphics.Save();e.Graphics.SetClip(path);if(IsAvailable&&Value>0)using(var brush=new SolidBrush(FillColor))e.Graphics.FillRectangle(brush,0,0,Width*Value/100,Height);e.Graphics.Restore(state);}
        }
    }
    internal sealed class ModernTabControl : TabControl
    {
        public ModernTabControl(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=ModernTheme.Canvas;}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(ModernTheme.Canvas);
            for(int i=0;i<TabCount;i++)
            {
                var box=GetTabRect(i);using(var brush=new SolidBrush(i==SelectedIndex?ModernTheme.Raised:ModernTheme.Surface))e.Graphics.FillRectangle(brush,Rectangle.Inflate(box,-2,-2));
                TextRenderer.DrawText(e.Graphics,TabPages[i].Text,Font,box,i==SelectedIndex?ModernTheme.Text:ModernTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                if(i==SelectedIndex)using(var pen=new Pen(ModernTheme.Accent,3))e.Graphics.DrawLine(pen,box.Left+8,box.Bottom-3,box.Right-8,box.Bottom-3);
            }
        }
    }
}
