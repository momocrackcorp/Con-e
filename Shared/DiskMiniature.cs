// SPDX-License-Identifier: MIT
// Illustrative storage artwork approved for this project; protocol/form labels are independent data.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

internal sealed class DiskMiniature : Control
{
    internal string Kind="generic";
    static readonly Lazy<Bitmap> Atlas=new Lazy<Bitmap>(()=>{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("StorageAtlas"))using(var image=new Bitmap(stream))return new Bitmap(image);});
    static readonly Lazy<Bitmap> Usb=new Lazy<Bitmap>(()=>{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("UsbMiniature"))using(var image=new Bitmap(stream))return new Bitmap(image);});
    internal DiskMiniature(){Size=new Size(104,88);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Draw(e.Graphics,ClientRectangle,Kind);}
    internal static void Draw(Graphics g,Rectangle bounds,string kind)
    {
        if(kind=="usb")
        {
            int side=Math.Min(bounds.Width,bounds.Height);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.DrawImage(Usb.Value,new Rectangle(bounds.X+(bounds.Width-side)/2,bounds.Y+(bounds.Height-side)/2,side,side));return;
        }
        int index=kind=="hdd"?0:kind=="ssd"?1:kind=="m2-sata"?2:kind=="m2-nvme"?3:-1;
        if(index>=0){var a=Atlas.Value;int w=a.Width/2,h=a.Height/2;var src=new Rectangle(index%2*w,index/2*h,w,h);int side=Math.Min(bounds.Width,bounds.Height);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(a,new Rectangle(bounds.X+(bounds.Width-side)/2,bounds.Y+(bounds.Height-side)/2,side,side),src,GraphicsUnit.Pixel);return;}
        // Generic illustrations avoid asserting an unknown form factor or medium.
        g.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(bounds.X+bounds.Width/6,bounds.Y+bounds.Height/4,Math.Max(12,bounds.Width*2/3),Math.Max(12,bounds.Height/2));
        using(var fill=new SolidBrush(Color.FromArgb(233,240,251)))using(var pen=new Pen(Color.FromArgb(48,94,164),2))
        {g.FillRectangle(fill,r);g.DrawRectangle(pen,r);if(kind=="virtual"){g.DrawEllipse(pen,r.X+8,r.Y+7,Math.Max(8,r.Height-14),Math.Max(8,r.Height-14));g.DrawRectangle(pen,r.X+6,r.Y-6,r.Width,r.Height);}else if(kind=="usb"){g.DrawRectangle(pen,r.X+r.Width/3,r.Y-10,r.Width/3,10);}else if(kind=="sd"){g.DrawLine(pen,r.X+4,r.Y+5,r.Right-4,r.Y+5);}else{g.DrawLine(pen,r.X+6,r.Bottom-8,r.Right-6,r.Bottom-8);}}
    }
    internal static Bitmap Make(string kind,int size){var b=new Bitmap(size,size);using(var g=Graphics.FromImage(b))Draw(g,new Rectangle(0,0,size,size),kind);return b;}
}
