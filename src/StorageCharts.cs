using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using UIModernization;

internal static class StorageFormat
{
    internal static string Size(double bytes){return bytes>=1073741824? (bytes/1073741824).ToString("0.0")+" GiB":bytes>=1048576?(bytes/1048576).ToString("0.0")+" MiB":bytes>=1024?(bytes/1024).ToString("0.0")+" KiB":bytes.ToString("0")+" B";}
    internal static readonly Color Used=Color.FromArgb(68,173,126),Free=Color.FromArgb(229,235,242),Other=Color.FromArgb(128,141,160);
    internal static readonly Color[] Palette={Color.FromArgb(31,145,169),Color.FromArgb(151,83,202),Color.FromArgb(236,170,69),Color.FromArgb(232,108,118),Color.FromArgb(71,130,211),Color.FromArgb(72,169,158)};
}
internal sealed class StorageRing : Control
{
    internal double Total,Free;
    internal void SetSpace(double total,double free){Total=total;Free=free;AccessibleName=total>0?"Disco: "+((total-free)/total*100).ToString("0")+" % ocupado":"Capacidad no disponible";Invalidate();}
    internal StorageRing(){Width=220;Height=185;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;int side=Math.Max(30,Math.Min(Width-30,Height-20));var box=new Rectangle((Width-side)/2,7,side,side);double pct=Total<=0?0:Math.Max(0,Math.Min(100,(Total-Free)/Total*100));
        using(var p=new Pen(StorageFormat.Free,11))e.Graphics.DrawEllipse(p,box);
        if(Total>0&&pct>0)using(var p=new Pen(pct>=95?Color.Firebrick:pct>=85?Color.DarkOrange:StorageFormat.Used,11){StartCap=LineCap.Round,EndCap=LineCap.Round})e.Graphics.DrawArc(p,box,-90,(float)(pct*3.6));
        using(var value=new Font(Font.FontFamily,25,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,Total>0?pct.ToString("0")+" %":"—",value,new Rectangle(box.Left,box.Top+side/3,side,side/3+5),ModernTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics,Total>0?"EN USO":"SIN LECTURA",Font,new Rectangle(box.Left,box.Top+side*2/3,side,25),ModernTheme.Muted,TextFormatFlags.HorizontalCenter);
    }
}
internal sealed class StorageSlice {internal string Name;internal long Bytes;internal Color Color;}
internal sealed class FolderChart : Control
{
    internal List<StorageSlice> Slices=new List<StorageSlice>();
    internal FolderChart(){Height=300;Width=700;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);Resize+=(s,e)=>FitHeight();}
    internal void SetSlices(List<StorageSlice> slices){Slices=slices;AccessibleName=string.Join("; ",Slices.Select(s=>s.Name+": "+StorageFormat.Size(s.Bytes)));FitHeight();Invalidate();}
    void FitHeight(){int needed=Width>=630?Math.Max(280,Slices.Count*34+16):250+Slices.Count*42;if(Height!=needed)Height=needed;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        if(Slices.Count==0){TextRenderer.DrawText(e.Graphics,"Pulsa Analizar carpetas para ver la distribución.",Font,ClientRectangle,ModernTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);return;}
        bool wide=Width>=630;int side=wide?Math.Min(280,Width*2/5):Math.Min(225,Width-16);var pie=new Rectangle(wide?6:(Width-side)/2,10,side,side);long total=Slices.Sum(s=>s.Bytes);float start=-90;
        if(total<=0)return;
        foreach(var slice in Slices){float sweep=(float)(slice.Bytes/(double)total*360);if(sweep<=0)continue;using(var fill=new SolidBrush(slice.Color))e.Graphics.FillPie(fill,pie,start,sweep);using(var border=new Pen(ModernTheme.Surface,1))e.Graphics.DrawPie(border,pie,start,sweep);start+=sweep;}
        int x=wide?side+30:4,y=wide?12:245,row=wide?34:42,available=Math.Max(80,Width-x-6);
        for(int i=0;i<Slices.Count;i++)
        {
            var s=Slices[i];using(var brush=new SolidBrush(s.Color))e.Graphics.FillRectangle(brush,x,y+i*row+5,12,12);
            if(wide){TextRenderer.DrawText(e.Graphics,s.Name,Font,new Rectangle(x+20,y+i*row,Math.Max(70,available-200),row),ModernTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);TextRenderer.DrawText(e.Graphics,StorageFormat.Size(s.Bytes)+" · "+(s.Bytes/(double)total*100).ToString("0.0")+" %",Font,new Rectangle(Math.Max(x+90,Width-195),y+i*row,185,row),ModernTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Right);}
            else {TextRenderer.DrawText(e.Graphics,s.Name,Font,new Rectangle(x+20,y+i*row,available-22,20),ModernTheme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);TextRenderer.DrawText(e.Graphics,StorageFormat.Size(s.Bytes)+" · "+(s.Bytes/(double)total*100).ToString("0.0")+" %",Font,new Rectangle(x+20,y+i*row+20,available-22,20),ModernTheme.Muted);}
        }
    }
}
