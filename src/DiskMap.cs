using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;

internal sealed class DiskLayout
{
    public uint Number;
    public string Name, Style;
    public ulong Size;
    public bool Complete;
    public readonly List<PartitionLayout> Partitions = new List<PartitionLayout>();
    public List<PartitionLayout> Segments()
    {
        var result = new List<PartitionLayout>(); ulong end = 0;
        foreach (var p in Partitions.OrderBy(p => p.Offset))
        {
            if (p.Offset < end || p.Offset > Size || p.Size > Size - p.Offset) return new List<PartitionLayout>();
            if (Complete && p.Offset > end) result.Add(new PartitionLayout { Offset=end, Size=p.Offset-end, Type="Intervalo no particionado", Gap=true });
            result.Add(p); end=p.Offset+p.Size;
        }
        if (Complete && end < Size) result.Add(new PartitionLayout { Offset=end, Size=Size-end, Type="Intervalo no particionado", Gap=true });
        return result;
    }
}
internal sealed class PartitionLayout
{
    public uint Number;
    public ulong Offset, Size;
    public string Letter="", Type="No determinado", FileSystem="No disponible", Label="";
    public ulong? Free, VolumeSize;
    public bool Gap;
    public string Title { get { return Gap ? Type : (Letter.Length>0 ? Letter + ": " : "Partición " + Number + " · ") + Type; } }
}
internal sealed partial class MainForm
{
    readonly FlowLayoutPanel diskMaps = new FlowLayoutPanel { Dock=DockStyle.Fill, AutoScroll=true, FlowDirection=FlowDirection.TopDown, WrapContents=false, Padding=new Padding(14), BackColor=CanvasColor };
    readonly TextBox partitionInfo = new TextBox { Dock=DockStyle.Bottom, Multiline=true, ReadOnly=true, Height=135, ScrollBars=ScrollBars.Vertical, BackColor=Color.White, BorderStyle=BorderStyle.FixedSingle };
    readonly List<DiskLayout> diskLayouts = new List<DiskLayout>();
    TabPage disksPage;
    Control BuildDiskMap()
    {
        var panel=new Panel { Dock=DockStyle.Fill };
        panel.Controls.Add(diskMaps);panel.Controls.Add(partitionInfo);
        diskMaps.Resize+=(s,e)=> { foreach(Control group in diskMaps.Controls) group.Width=Math.Max(240,diskMaps.ClientSize.Width-diskMaps.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8); };
        return panel;
    }
    static string Space(ulong bytes) { return (bytes/1073741824d).ToString("0.##")+" GiB"; }
    void SelectPartition(DiskLayout disk, PartitionLayout p)
    {
        partitionInfo.Text="Disco "+disk.Number+" · "+disk.Name+Environment.NewLine+p.Title+Environment.NewLine+
            "Tamaño del bloque: "+Space(p.Size)+" · Offset: "+p.Offset.ToString(CultureInfo.InvariantCulture)+" bytes"+Environment.NewLine+
            (p.Gap ? "Intervalo sin partición en el mapa leído; puede incluir espacios de alineación o metadatos." :
            "Sistema de archivos: "+p.FileSystem+" · Etiqueta: "+p.Label+Environment.NewLine+
            "Tamaño del volumen: "+(p.VolumeSize.HasValue?Space(p.VolumeSize.Value):"No disponible")+" · Libre: "+(p.Free.HasValue?Space(p.Free.Value):"No disponible")+
            (p.VolumeSize.HasValue&&p.Free.HasValue&&p.Free.Value<=p.VolumeSize.Value?" · Ocupado: "+Space(p.VolumeSize.Value-p.Free.Value):""))+
            Environment.NewLine+"Vista de consulta: no se modifican particiones.";
    }
    void RenderDiskMaps()
    {
        diskMaps.SuspendLayout();
        try
        {
            foreach(Control old in diskMaps.Controls.Cast<Control>().ToArray())old.Dispose();
            partitionInfo.Text="Selecciona un bloque del mapa o una fila para ver sus datos. Las particiones pequeñas se seleccionan también en la lista.";
            if(diskLayouts.Count==0) { diskMaps.Controls.Add(new Label { Text="No se pudo obtener el mapa físico de discos. Revisa las incidencias de lectura.", AutoSize=true, MaximumSize=new Size(500,0) });return; }
            foreach(var disk in diskLayouts)
            {
                var group=new Panel { Width=Math.Max(240,diskMaps.ClientSize.Width-60), Height=260, BackColor=Color.White, Margin=new Padding(0,0,0,16), Padding=new Padding(12) };
                var heading=new Label { Text="Disco "+disk.Number+" · "+disk.Name+" · "+Space(disk.Size)+" · "+disk.Style, Dock=DockStyle.Top, AutoSize=true, MaximumSize=new Size(group.Width-30,0) };
                var segments=disk.Segments();
                var map=new PartitionStrip(disk,segments) { Dock=DockStyle.Top, Height=72, Margin=new Padding(0,8,0,8) };
                var list=MakeList(new[]{"Partición / intervalo","Tamaño","Libre"});list.GridLines=false;list.ShowItemToolTips=true;
                list.Columns[0].Width=350;list.Columns[1].Width=120;list.Columns[2].Width=120;
                foreach(var p in segments.Count>0?segments:disk.Partitions)
                {
                    var item=new ListViewItem(new[]{p.Title,Space(p.Size),p.Free.HasValue?Space(p.Free.Value):"No disponible"}) {Tag=p,ToolTipText=p.Title};list.Items.Add(item);
                }
                map.Selected+=(p)=> { SelectPartition(disk,p); foreach(ListViewItem row in list.Items)row.Selected=ReferenceEquals(row.Tag,p); };
                list.SelectedIndexChanged+=(s,e)=>{if(list.SelectedItems.Count==0)return;var p=(PartitionLayout)list.SelectedItems[0].Tag;map.Selection=p;map.Invalidate();SelectPartition(disk,p);};
                list.Resize+=(s,e)=>{int width=Math.Max(260,list.ClientSize.Width-20);list.Columns[0].Width=Math.Max(140,width-240);};
                group.Resize+=(s,e)=>heading.MaximumSize=new Size(Math.Max(200,group.Width-30),0);
                group.Controls.Add(list);group.Controls.Add(map);group.Controls.Add(heading);diskMaps.Controls.Add(group);
            }
        }
        finally{diskMaps.ResumeLayout(true);}
    }
    static List<DiskLayout> ReadDiskLayouts(Snapshot data)
    {
        var result=new List<DiskLayout>();
        try
        {
            using(var query=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT Number,FriendlyName,Size,PartitionStyle FROM MSFT_Disk"))
            using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
            { result.Add(new DiskLayout { Number=Convert.ToUInt32(row["Number"]),Name=Value(row,"FriendlyName"),Size=Convert.ToUInt64(row["Size"]),Style=Value(row,"PartitionStyle")=="2"?"GPT":Value(row,"PartitionStyle")=="1"?"MBR":"RAW / no determinado" }); }
            using(var query=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT DiskNumber,PartitionNumber,Offset,Size,DriveLetter,GptType,Type FROM MSFT_Partition"))
            using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
            {
                var disk=result.FirstOrDefault(d=>d.Number==Convert.ToUInt32(row["DiskNumber"]));if(disk==null)continue;
                string letter=Convert.ToString(row["DriveLetter"]).Trim('\0',' ');
                disk.Partitions.Add(new PartitionLayout { Number=Convert.ToUInt32(row["PartitionNumber"]),Offset=Convert.ToUInt64(row["Offset"]),Size=Convert.ToUInt64(row["Size"]),Letter=letter,Type=PartitionType(Value(row,"GptType"),Value(row,"Type")) });
            }
            foreach(var disk in result)disk.Complete=true;
        }
        catch(Exception ex) {data.Warnings.Add("Mapa de discos: "+ex.Message);}
        foreach(var disk in result)foreach(var p in disk.Partitions.Where(p=>p.Letter.Length==1&&char.IsLetter(p.Letter[0])))
        {
            try
            {
                using(var query=new ManagementObjectSearcher("SELECT Size,FreeSpace,FileSystem,VolumeName FROM Win32_LogicalDisk WHERE DeviceID='"+p.Letter+":'"))
                using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)
                {ulong value;if(ulong.TryParse(Value(row,"Size"),out value))p.VolumeSize=value;if(ulong.TryParse(Value(row,"FreeSpace"),out value))p.Free=value;p.FileSystem=Value(row,"FileSystem");p.Label=Value(row,"VolumeName");}
            }catch{data.Warnings.Add("No se pudo leer el volumen "+p.Letter+":");}
        }
        return result;
    }
    static string PartitionType(string guid,string type)
    {
        switch(guid.Trim('{','}').ToUpperInvariant())
        {case "C12A7328-F81F-11D2-BA4B-00A0C93EC93B":return "EFI";case "DE94BBA4-06D1-4D40-A16A-BFD50179D6AC":return "Recuperación";case "E3C9E316-0B5C-4DB8-817D-F92DF00215AE":return "Reservada MSR";case "EBD0A0A2-B9E5-4433-87C0-68B6B72699C7":return "Datos básicos";default:return type+" · "+guid;}
    }
}
internal sealed class PartitionStrip : Control
{
    readonly DiskLayout disk;readonly List<PartitionLayout> segments;
    public PartitionLayout Selection;
    public event Action<PartitionLayout> Selected;
    public PartitionStrip(DiskLayout d,List<PartitionLayout> p) {disk=d;segments=p;TabStop=true;AccessibleName="Mapa de particiones del disco "+d.Number;SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if(disk.Size==0||segments.Count==0){TextRenderer.DrawText(e.Graphics,"Mapa proporcional no disponible (lectura parcial, solapamiento o disco sin tamaño).",Font,ClientRectangle,Color.DimGray,TextFormatFlags.WordBreak);return;}
        foreach(var p in segments)
        {
            int left=(int)(p.Offset/(double)disk.Size*Width),right=(int)((p.Offset+(double)p.Size)/disk.Size*Width);
            var box=new Rectangle(left,6,Math.Max(1,right-left),Height-12);
            using(var fill=new SolidBrush(p.Gap?Color.FromArgb(210,214,220):ReferenceEquals(p,Selection)?Color.FromArgb(36,87,163):Color.FromArgb(70,121,201)))e.Graphics.FillRectangle(fill,box);
            if(box.Width>70)TextRenderer.DrawText(e.Graphics,p.Title,Font,Rectangle.Inflate(box,-5,-4),p.Gap?Color.FromArgb(35,50,71):Color.White,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
        }
        if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(disk.Size==0)return;double point=e.X/(double)Math.Max(1,Width)*disk.Size;var p=segments.FirstOrDefault(s=>point>=s.Offset&&point<s.Offset+(double)s.Size);if(p!=null)Choose(p);}
    protected override bool IsInputKey(Keys keyData){return keyData==Keys.Left||keyData==Keys.Right||base.IsInputKey(keyData);}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(segments.Count==0)return;int index=segments.IndexOf(Selection);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){index=Math.Max(0,Math.Min(segments.Count-1,index+(e.KeyCode==Keys.Left?-1:1)));Choose(segments[index]);e.Handled=true;}}
    void Choose(PartitionLayout p){Selection=p;Invalidate();if(Selected!=null)Selected(p);}
}
