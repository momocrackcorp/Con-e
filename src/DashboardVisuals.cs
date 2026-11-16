using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UIModernization;

// Keep disclosure controls at the right edge, reserving their own row on narrow cards.
internal sealed class CardHeadingPanel : FlowLayoutPanel
{
    bool arranging;
    protected override void OnLayout(LayoutEventArgs e)
    {
        if(arranging)return;arranging=true;
        try
        {
            base.OnLayout(e);
            var toggle=Controls.OfType<Button>().FirstOrDefault();
            int available=Math.Max(40,ClientSize.Width-Padding.Horizontal),x=Padding.Left,y=Padding.Top,rowHeight=0;
            int toggleWidth=toggle==null?0:Math.Min(available,toggle.GetPreferredSize(Size.Empty).Width);
            foreach(Control child in Controls)
            {
                if(child==toggle||!child.Visible)continue;
                int room=available-(toggle==null?0:toggleWidth+10);
                if(room<160)room=available;
                if(child is Label){child.MaximumSize=new Size(Math.Max(70,room),0);child.Size=child.GetPreferredSize(new Size(Math.Max(70,room),0));}
                if(x>Padding.Left&&x+child.Width+child.Margin.Horizontal>Padding.Left+room){y+=rowHeight+4;x=Padding.Left;rowHeight=0;}
                child.Location=new Point(x+child.Margin.Left,y+child.Margin.Top);x+=child.Width+child.Margin.Horizontal;rowHeight=Math.Max(rowHeight,child.Height+child.Margin.Vertical);
            }
            if(toggle!=null)
            {
                toggle.Size=toggle.GetPreferredSize(new Size(toggleWidth,0));
                if(x+10>Padding.Left+available-toggleWidth){y+=rowHeight+4;rowHeight=0;}
                toggle.Location=new Point(Padding.Left+available-toggle.Width,y+3);rowHeight=Math.Max(rowHeight,toggle.Height+6);
            }
            Height=y+rowHeight+Padding.Bottom;
        }
        finally{arranging=false;}
    }
}
internal static class DiskDisplaySize
{
    // Decimal storage units: GB/MB/TB, consistently matching the legend percentages.
    internal static string Format(ulong bytes)
    {
        string[] units={"B","KB","MB","GB","TB","PB"};double n=bytes;int i=0;
        while(n>=1000&&i<units.Length-1){n/=1000;i++;}
        return n.ToString(i==0?"0":"0.#")+" "+units[i];
    }
}
internal sealed class PartitionOverview : Control
{
    internal sealed class Slice {internal string Name,Type;internal ulong Bytes;internal Color Color;}
    internal readonly List<Slice> Slices=new List<Slice>();
    readonly ulong total;
    static readonly Color[] Palette={Color.FromArgb(45,101,196),Color.FromArgb(32,155,139),Color.FromArgb(226,160,45),Color.FromArgb(143,95,191),Color.FromArgb(204,94,104),Color.FromArgb(79,152,188)};
    internal PartitionOverview(PhysicalDiskInfo disk)
    {
        total=disk.Size;int index=0;ulong sum=0;bool valid=true;
        foreach(var p in disk.Partitions.OrderBy(p=>p.Offset))
        {
            Slices.Add(new Slice {Name=p.Letter.Length>0?p.Letter+":":p.Role,Type=p.FileSystem=="No disponible"?"":p.FileSystem,Bytes=p.Size,Color=Palette[index++%Palette.Length]});
            if(p.Size>ulong.MaxValue-sum)valid=false;else sum+=p.Size;
        }
        if(disk.Complete&&valid&&sum<total)Slices.Add(new Slice {Name="Sin asignar",Type="",Bytes=total-sum,Color=Color.FromArgb(209,217,229)});
        Height=Math.Max(190,Slices.Count*42+12);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        AccessibleName="Distribución de particiones del disco, tamaños asignados";
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;int top=6,h=Height-12;float y=top;
        if(total==0||Slices.Count==0){TextRenderer.DrawText(g,"Sin particiones informadas",Font,ClientRectangle,ModernTheme.Muted);return;}
        foreach(var slice in Slices)
        {
            float segment=(float)((double)slice.Bytes/total*h);segment=Math.Max(0,Math.Min(segment,top+h-y));
            using(var brush=new SolidBrush(slice.Color))g.FillRectangle(brush,6,y,42,segment);y+=segment;
        }
        using(var pen=new Pen(ModernTheme.Border))g.DrawRectangle(pen,6,top,42,h);
        int row=6;
        foreach(var slice in Slices)
        {
            using(var brush=new SolidBrush(slice.Color))g.FillRectangle(brush,60,row+4,9,9);
            int w=Math.Max(20,Width-80);
            TextRenderer.DrawText(g,slice.Name+(slice.Type.Length>0?" · "+slice.Type:""),Font,new Rectangle(76,row,w,20),ModernTheme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g,DiskDisplaySize.Format(slice.Bytes)+" · "+((double)slice.Bytes/total*100).ToString("0.##")+" %",Font,new Rectangle(76,row+20,w,20),ModernTheme.Muted,TextFormatFlags.EndEllipsis);row+=42;
        }
    }
}
internal sealed class WindowsVersionLogo : Control
{
    internal readonly string Family;
    internal static string Detect(string caption)
    {
        var match=Regex.Match(caption??"",@"\bWindows\s+(11|10|8(?:\.1)?|7)\b",RegexOptions.IgnoreCase);
        return match.Success?match.Groups[1].Value:"generic";
    }
    internal WindowsVersionLogo(string caption){Family=Detect(caption);AccessibleName="Windows "+(Family=="generic"?"(versión sin logo específico)":Family);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float size=Math.Min(Width,Height)-10,x=(Width-size)/2,y=(Height-size)/2,gap=2,half=(size-gap)/2;
        var colors=Family=="7"?new[]{Color.FromArgb(242,80,34),Color.FromArgb(127,186,0),Color.FromArgb(0,164,239),Color.FromArgb(255,185,0)}:Enumerable.Repeat(Family=="generic"?ModernTheme.Muted:Color.FromArgb(0,120,215),4).ToArray();
        for(int row=0;row<2;row++)for(int col=0;col<2;col++)using(var brush=new SolidBrush(colors[row*2+col]))
        {
            float left=x+col*(half+gap),right=left+half,up=y+row*(half+gap),down=up+half;
            if(Family=="10"||Family.StartsWith("8"))
            {
                Func<float,float,float> perspective=(xx,yy)=>y+size/2+(yy-y-size/2)*(0.72f+0.28f*(xx-x)/size);
                e.Graphics.FillPolygon(brush,new[]{new PointF(left,perspective(left,up)),new PointF(right,perspective(right,up)),new PointF(right,perspective(right,down)),new PointF(left,perspective(left,down))});
            }
            else e.Graphics.FillRectangle(brush,left,up,half,half);
        }
    }
}
