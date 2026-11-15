using UIModernization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;

internal sealed class RamSlot
{
    public string Name,State;
    public HardwareItem Module;
}
internal sealed partial class MainForm
{
    static List<RamSlot> ParseRamSlots(byte[] table)
    {
        var slots=new List<RamSlot>();int offset=0;
        var systemArrays=new HashSet<ushort>();
        for(int scan=0;scan+4<=table.Length;)
        {
            int length=table[scan+1];if(length<4||scan+length>table.Length)break;
            if(table[scan]==16&&length>6&&table[scan+5]==3)systemArrays.Add(BitConverter.ToUInt16(table,scan+2));
            int next=scan+length;while(next+1<table.Length&&(table[next]!=0||table[next+1]!=0))next++;scan=next+2;
        }
        while(offset+4<=table.Length)
        {
            int type=table[offset],len=table[offset+1];if(len<4||offset+len>table.Length)break;
            int end=offset+len;while(end+1<table.Length&&(table[end]!=0||table[end+1]!=0))end++;
            if(type==17&&len>=0x15&&(systemArrays.Count==0||systemArrays.Contains(BitConverter.ToUInt16(table,offset+4))))
            {
                var strings=new List<string>();int i=offset+len;
                while(i<end&&table[i]!=0){int j=i;while(j<=end&&table[j]!=0)j++;strings.Add(Encoding.ASCII.GetString(table,i,j-i));i=j+1;}
                int nameIndex=table[offset+0x10];string name=nameIndex>0&&nameIndex<=strings.Count?strings[nameIndex-1]:"Posición SMBIOS "+(slots.Count+1);
                ushort size=BitConverter.ToUInt16(table,offset+0x0C);
                slots.Add(new RamSlot {Name=name,State=size==0?"Libre según SMBIOS":size==0xFFFF?"Estado no determinado":"Ocupada según SMBIOS"});
            }
            if(type==127)break;offset=end+2;
        }
        return slots;
    }
    static void CollectRamSlots(Snapshot data,int reportedSlots)
    {
        var modules=data.Hardware.Where(h=>h.Category=="Memoria RAM").ToArray();var slots=new List<RamSlot>();
        try
        {
            using(var query=new ManagementObjectSearcher(@"root\wmi","SELECT SMBiosData FROM MSSmBios_RawSMBiosTables"))
            using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)if(row["SMBiosData"] is byte[]) {slots=ParseRamSlots((byte[])row["SMBiosData"]);break;}
        }catch{}
        foreach(var module in modules)
        {
            var slot=slots.FirstOrDefault(s=>string.Equals(s.Name.Trim(),module.Name.Trim(),StringComparison.OrdinalIgnoreCase));
            if(slot==null){slot=new RamSlot {Name=module.Name,State="Ocupada · módulo detectado"};slots.Add(slot);}
            slot.Module=module;slot.State="Ocupada · módulo detectado";
        }
        // Do not invent physical labels or certify an accessible empty socket from a count alone.
        if(slots.Count<reportedSlots)for(int i=slots.Count;i<reportedSlots;i++)slots.Add(new RamSlot {Name="Posición adicional "+(i+1),State="Sin módulo detectado · ubicación física no informada"});
        data.RamSlots.AddRange(slots);
        double total=0;foreach(var module in modules){string raw;double bytes;if(module.Properties.TryGetValue("Capacidad (bytes)",out raw)&&double.TryParse(raw,out bytes))total+=bytes;}
        if(total>0)data.Summary.Add(new[]{"Memoria instalada en módulos",(total/1073741824d).ToString("0.##")+" GiB"});
    }
    void RenderRamSlots(FlowLayoutPanel card)
    {
        if(current==null)return;
        var grid=new CompactRamGrid {Margin=new Padding(0,3,0,4)};
        foreach(var slot in current.RamSlots)
        {
            string capacity="",type="",speed="";double bytes;
            if(slot.Module!=null){slot.Module.Properties.TryGetValue("Capacidad (bytes)",out capacity);slot.Module.Properties.TryGetValue("Tipo SMBIOS (código)",out type);slot.Module.Properties.TryGetValue("Velocidad configurada (MHz)",out speed);}
            string summary=slot.Module==null?slot.State:(double.TryParse(capacity,out bytes)?(bytes/1073741824d).ToString("0.##")+" GiB":"Capacidad no disponible")+" · "+RamType(type??"0")+(string.IsNullOrWhiteSpace(speed)?"":" · "+speed+" MHz");
            grid.AddSlot(slot,summary,ResizeCards);
        }
        card.Controls.Add(grid);
        Help(card,"SMBIOS puede incluir memoria soldada. Una posición libre informada no garantiza que sea accesible o ampliable.");
    }
    static void RamCompactSmokeTest(MainForm form,string dir)
    {
        DiskMediumDetection.VerifyFallbacks();
        var before=form.current;var originalSize=form.Size;var test=new Snapshot {Manufacturer=before.Manufacturer,Model="RAM compacta · datos simulados",RamTotalGiB=32,RamAvailableGiB=19.6,PnpReadComplete=true};
        foreach(var p in before.Summary.Where(p=>p[0]!="Memoria RAM instalada"&&p[0]!="Memoria instalada en módulos"&&p[0]!="Ranuras RAM"&&p[0]!="Procesador"&&p[0]!="Núcleos / procesadores lógicos"))test.Summary.Add(p);
        test.Summary.Add(new[]{"Memoria RAM instalada","32 GiB"});test.Summary.Add(new[]{"Ranuras RAM","4 posiciones informadas · 4 módulos detectados"});test.Summary.Add(new[]{"Procesador","Intel Core i7-4770K · ejemplo"});test.Summary.Add(new[]{"Núcleos / procesadores lógicos","4 / 8"});test.CoreThreads.AddRange(new[]{2,2,2,2});
        foreach(string name in new[]{"DIMM_A1","DIMM_A2","DIMM_B1","DIMM_B2"}){var m=new HardwareItem {Name=name,Category="Memoria RAM"};m.Properties["Capacidad (bytes)"]="8589934592";m.Properties["Tipo SMBIOS (código)"]="24";m.Properties["Velocidad configurada (MHz)"]="1600";m.Properties["Fabricante"]="Ejemplo";m.Properties["Serie"]="SERIE-PRUEBA";test.RamSlots.Add(new RamSlot {Name=name,State="Ocupada · módulo detectado",Module=m});}
        form.current=test;form.Width=1100;form.tabs.SelectedIndex=0;form.RenderDashboard();form.ResizeCards();Application.DoEvents();
        var card=form.cards.First(c=>Convert.ToString(c.Tag)=="Memoria RAM");var grid=card.Controls.OfType<CompactRamGrid>().Single();var tiles=grid.Controls.OfType<CompactRamTile>().ToArray();
        // Exercise the same production card at a fixed logical width, independent of runner screen limits.
        using(var preview=new Form {Text="Memoria RAM compacta · datos simulados",ClientSize=new Size(630,650),AutoScroll=true,BackColor=ModernTheme.Canvas,Font=form.Font})
        {
            form.dashboard.Controls.Remove(card);preview.Controls.Add(card);card.Location=new Point(12,12);var modern=card as ModernCardPanel;if(modern!=null)modern.Selected=true;
            Action<int> fit=width=>{card.MinimumSize=card.MaximumSize=new Size(width,0);card.Width=width;int inner=width-card.Padding.Horizontal;foreach(Control child in card.Controls){if(child is Label)child.MaximumSize=new Size(inner,0);else if(child is FlowLayoutPanel){child.MinimumSize=child.MaximumSize=new Size(inner,0);}}preview.PerformLayout();card.PerformLayout();Application.DoEvents();};
            preview.Show();fit(600);
            using(var image=new Bitmap(preview.Width,preview.Height)){preview.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(System.IO.Path.Combine(dir,"ram-distribucion.png"));}
            if(tiles.Length!=4||tiles[0].Top!=tiles[1].Top||tiles[2].Top<=tiles[0].Top||grid.Height>280*form.DeviceDpi/96f)throw new Exception("RAM no compacta: grid="+grid.Size+", tiles="+string.Join(";",tiles.Select(t=>t.Bounds.ToString())));
            int collapsed=tiles[0].Height;tiles[0].ToggleDetailsForTest();Application.DoEvents();if(tiles[0].Height<=collapsed)throw new Exception("No expande detalle de módulo RAM");tiles[0].ToggleDetailsForTest();Application.DoEvents();if(tiles[0].Height!=collapsed)throw new Exception("No contrae detalle de módulo RAM");
            using(var image=new Bitmap(preview.Width,preview.Height)){preview.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(System.IO.Path.Combine(dir,"ram-compacta-ancha.png"));}
            fit(290);preview.ClientSize=new Size(320,650);Application.DoEvents();if(tiles[0].Top==tiles[1].Top)throw new Exception("RAM no se adapta a una columna en tarjetas estrechas");
            using(var image=new Bitmap(preview.Width,preview.Height)){preview.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(System.IO.Path.Combine(dir,"ram-compacta-estrecha.png"));}preview.Close();
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir,"ram-hdd.txt"),"OK: cuatro módulos RAM en filas compactas, dos/una columna, detalle expandible y plegable. HDD WD10EARX/WD10EAVS detrás de RAID reconocidos por catálogo; RPM, descriptor válido/inválido, SSD y VHD sin reclasificar indebidamente.");
        form.current=before;form.Size=originalSize;form.RenderDashboard();
    }

}
internal sealed class CompactRamGrid : FlowLayoutPanel
{
    bool fitting;
    internal CompactRamGrid(){FlowDirection=FlowDirection.LeftToRight;WrapContents=true;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Padding=Padding.Empty;BackColor=ModernTheme.Surface;Resize+=(s,e)=>Fit();}
    internal void AddSlot(RamSlot slot,string summary,Action changed)
    {var tile=new CompactRamTile(slot,summary,changed);Controls.Add(tile);Fit();}
    void Fit()
    {
        if(fitting)return;fitting=true;
        try{int columns=ClientSize.Width>=420*DeviceDpi/96f?2:1;int width=Math.Max(120,(ClientSize.Width-columns*6)/columns);foreach(var tile in Controls.OfType<CompactRamTile>())tile.SetWidth(width);}
        finally{fitting=false;}
    }
}
internal sealed class CompactRamTile : TableLayoutPanel
{
    readonly FlowLayoutPanel text=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
    readonly FlowLayoutPanel detail=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0,6,0,0),Visible=false};
    readonly LinkLabel toggle;
    internal CompactRamTile(RamSlot slot,string summary,Action changed)
    {
        AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;ColumnCount=2;RowCount=2;BackColor=ModernTheme.Raised;Padding=new Padding(6);Margin=new Padding(0,3,6,3);Tag=slot;
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,62));ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));RowStyles.Add(new RowStyle(SizeType.AutoSize));RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bool occupied=slot.Module!=null||slot.State.StartsWith("Ocupada");
        Controls.Add(new SlotPicture {Occupied=occupied,Width=54,Height=23,Margin=new Padding(0,6,4,0),AccessibleName=slot.Name+": "+slot.State},0,0);
        string state=occupied?"Ocupada":slot.State.StartsWith("Libre")?"Libre":"Sin confirmar";
        text.Controls.Add(new Label {Text=slot.Name+" · "+state,UseMnemonic=false,AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold),ForeColor=ModernTheme.Text,Margin=new Padding(0,0,0,2)});
        text.Controls.Add(new Label {Text=summary,UseMnemonic=false,AutoSize=true,Font=new Font("Segoe UI",9.5f),ForeColor=ModernTheme.Muted,Margin=Padding.Empty});
        if(slot.Module!=null){foreach(var p in slot.Module.Properties)detail.Controls.Add(new Label {Text=p.Key+": "+p.Value,UseMnemonic=false,AutoSize=true,ForeColor=ModernTheme.Muted,Font=new Font("Segoe UI",9.5f),Margin=new Padding(0,0,0,3)});toggle=new LinkLabel {Text="Detalles",AutoSize=true,Font=new Font("Segoe UI",9.5f),LinkColor=ModernTheme.Accent,Margin=new Padding(0,2,0,0)};toggle.LinkClicked+=(s,e)=>{detail.Visible=!detail.Visible;toggle.Text=detail.Visible?"Ocultar detalles":"Detalles";PerformLayout();changed?.Invoke();};text.Controls.Add(toggle);}
        Controls.Add(text,1,0);Controls.Add(detail,0,1);SetColumnSpan(detail,2);AccessibleName=slot.Name+": "+slot.State+" · "+summary;
    }
    internal void SetWidth(int width)
    {
        MinimumSize=MaximumSize=new Size(width,0);Width=width;int available=Math.Max(65,width-Padding.Horizontal-66);
        text.MinimumSize=text.MaximumSize=new Size(available,0);foreach(Control c in text.Controls)c.MaximumSize=new Size(available,0);detail.MinimumSize=detail.MaximumSize=new Size(Math.Max(80,width-Padding.Horizontal),0);foreach(Control c in detail.Controls)c.MaximumSize=new Size(Math.Max(80,width-Padding.Horizontal),0);
    }
    internal void ToggleDetailsForTest(){if(toggle!=null){detail.Visible=!detail.Visible;toggle.Text=detail.Visible?"Ocultar detalles":"Detalles";PerformLayout();}}
}
internal sealed class SlotPicture : Control
{
    public bool Occupied;
    public SlotPicture(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);int h=Math.Max(8,Height-7);var box=new Rectangle(2,2,Math.Max(8,Width-5),h);
        using(var brush=new SolidBrush(Occupied?Color.FromArgb(52,127,85):ModernTheme.Track))e.Graphics.FillRectangle(brush,box);
        using(var pen=new Pen(Color.FromArgb(75,95,115)))e.Graphics.DrawRectangle(pen,box);
        int chip=Math.Max(2,(box.Width-10)/4);
        if(Occupied)for(int i=0;i<4;i++){e.Graphics.FillRectangle(Brushes.DarkSlateGray,box.Left+4+i*(chip+1),box.Top+3,Math.Max(2,chip-2),Math.Max(4,h-6));e.Graphics.FillRectangle(Brushes.Goldenrod,box.Left+4+i*(chip+1),box.Bottom+1,Math.Max(2,chip-2),3);}
        else TextRenderer.DrawText(e.Graphics,"Libre",Font,box,ModernTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
}
