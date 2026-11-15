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
internal sealed class FolderChart : UserControl
{
    internal List<StorageSlice> Slices=new List<StorageSlice>();
    readonly SliceBar bar=new SliceBar {Dock=DockStyle.Top,Height=23};
    readonly ListView list=new ListView {Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.None,HeaderStyle=ColumnHeaderStyle.Nonclickable};
    readonly Label selection=new Label {Dock=DockStyle.Bottom,Height=42,ForeColor=ModernTheme.Muted,AutoEllipsis=true,UseMnemonic=false,Padding=new Padding(3,6,0,0)};
    ImageList swatches;readonly List<Bitmap> swatchImages=new List<Bitmap>();
    internal FolderChart()
    {
        Height=235;Width=440;BackColor=ModernTheme.Surface;list.BackColor=ModernTheme.Surface;list.ForeColor=ModernTheme.Text;list.ShowItemToolTips=true;list.Columns.Add("Carpeta / grupo",200);list.Columns.Add("Tamaño",110);list.Columns.Add("%",65);
        Controls.Add(list);Controls.Add(selection);Controls.Add(bar);Resize+=(s,e)=>{int w=Math.Max(200,list.ClientSize.Width-20);list.Columns[0].Width=Math.Max(100,w-160);list.Columns[1].Width=100;list.Columns[2].Width=60;};
        list.SelectedIndexChanged+=(s,e)=>{if(list.SelectedItems.Count==0)return;int index=list.SelectedItems[0].Index;bar.Selected=index;bar.Invalidate();var item=Slices[index];double total=Slices.Sum(v=>(double)v.Bytes);selection.Text=item.Name+" · "+StorageFormat.Size(item.Bytes)+" · "+(total>0?item.Bytes/total*100:0).ToString("0.0")+" %";};
        bar.Picked+=i=>{if(i>=0&&i<list.Items.Count){list.Items[i].Selected=true;list.Items[i].EnsureVisible();}};Disposed+=(s,e)=>{swatches?.Dispose();foreach(var b in swatchImages)b.Dispose();};
    }
    internal void SetSlices(List<StorageSlice> slices)
    {
        Slices=slices;bar.Slices=slices;bar.Selected=-1;bar.Invalidate();AccessibleName=string.Join("; ",slices.Select(s=>s.Name+": "+StorageFormat.Size(s.Bytes)));list.BeginUpdate();
        try{list.Items.Clear();list.SmallImageList=null;swatches?.Dispose();foreach(var image in swatchImages)image.Dispose();swatchImages.Clear();swatches=new ImageList {ImageSize=new Size(12,12),ColorDepth=ColorDepth.Depth32Bit};list.SmallImageList=swatches;double total=slices.Sum(s=>(double)s.Bytes);int i=0;foreach(var slice in slices){var image=new Bitmap(12,12);using(var g=Graphics.FromImage(image))g.Clear(slice.Color);swatchImages.Add(image);swatches.Images.Add(image);list.Items.Add(new ListViewItem(new[]{slice.Name,StorageFormat.Size(slice.Bytes),(total>0?slice.Bytes/total*100:0).ToString("0.0")+" %"},i++){ToolTipText=slice.Name});}}
        finally{list.EndUpdate();}selection.Text=slices.Count==0?"Sin analizar · sólo se mostrarán carpetas de la raíz.":"Selecciona un color o una carpeta para ver su tamaño.";Height=slices.Count==0?78:Math.Min(310,92+Math.Max(3,slices.Count)*25);
    }
    sealed class SliceBar : Control
    {
        internal List<StorageSlice> Slices=new List<StorageSlice>();internal int Selected=-1;internal event Action<int> Picked;
        internal SliceBar(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);double total=Slices.Sum(s=>(double)s.Bytes);if(total<=0){using(var b=new SolidBrush(ModernTheme.Track))e.Graphics.FillRectangle(b,ClientRectangle);return;}float x=0;for(int i=0;i<Slices.Count;i++){float width=(float)(Slices[i].Bytes/total*Width);using(var b=new SolidBrush(i==Selected?ControlPaint.Dark(Slices[i].Color):Slices[i].Color))e.Graphics.FillRectangle(b,x,1,width,Height-3);x+=width;}}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);double total=Slices.Sum(s=>(double)s.Bytes),x=0;for(int i=0;i<Slices.Count;i++){x+=total>0?Slices[i].Bytes/total*Width:0;if(e.X<x){Picked?.Invoke(i);return;}}}
    }
}
internal sealed class CapacityBar : Control
{
    double total,free;
    internal CapacityBar(){Height=12;Width=220;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);}
    internal void SetSpace(double t,double f){total=t;free=f;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var b=new SolidBrush(StorageFormat.Free))e.Graphics.FillRectangle(b,ClientRectangle);if(total>0)using(var b=new SolidBrush(StorageFormat.Used))e.Graphics.FillRectangle(b,0,0,(int)(Width*Math.Max(0,Math.Min(1,(total-free)/total))),Height);}
}
