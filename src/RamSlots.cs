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
        foreach(var slot in current.RamSlots)
        {
            var panel=new FlowLayoutPanel {FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,BackColor=Color.FromArgb(239,244,251),Padding=new Padding(8),Margin=new Padding(0,5,0,5)};
            panel.Controls.Add(new SlotPicture {Occupied=slot.Module!=null||slot.State.StartsWith("Ocupada"),Width=145,Height=38,AccessibleName=slot.Name+": "+slot.State});
            Line(panel,slot.Name,true);Line(panel,slot.State);
            if(slot.Module!=null)
            {
                string capacity,type,speed;slot.Module.Properties.TryGetValue("Capacidad (bytes)",out capacity);slot.Module.Properties.TryGetValue("Tipo SMBIOS (código)",out type);slot.Module.Properties.TryGetValue("Velocidad configurada (MHz)",out speed);double bytes;
                Line(panel,(double.TryParse(capacity,out bytes)?(bytes/1073741824d).ToString("0.##")+" GiB":"Capacidad no disponible")+" · "+RamType(type??"0")+" · "+speed+" MHz");
                var details=slot.Module.Properties.Select(p=>Line(panel,p.Key+": "+p.Value)).ToArray();foreach(var l in details)l.Visible=false;
                Button toggle=null;toggle=ActionButton("Ver detalle del módulo",()=>{bool show=!details[0].Visible;foreach(var l in details)l.Visible=show;toggle.Text=show?"Ocultar detalle":"Ver detalle del módulo";ResizeCards();});panel.Controls.Add(toggle);
            }
            card.Controls.Add(panel);
        }
        Line(card,"SMBIOS puede incluir memoria soldada. Una posición libre informada no garantiza que sea accesible o ampliable.");
    }
}
internal sealed class SlotPicture : Control
{
    public bool Occupied;
    public SlotPicture(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var box=new Rectangle(3,5,Width-8,25);
        using(var brush=new SolidBrush(Occupied?Color.FromArgb(52,127,85):Color.FromArgb(218,225,235)))e.Graphics.FillRectangle(brush,box);
        using(var pen=new Pen(Color.FromArgb(75,95,115),2))e.Graphics.DrawRectangle(pen,box);
        if(Occupied)for(int i=0;i<4;i++){e.Graphics.FillRectangle(Brushes.DarkSlateGray,12+i*28,10,20,14);e.Graphics.FillRectangle(Brushes.Goldenrod,12+i*28,30,20,4);}
        else TextRenderer.DrawText(e.Graphics,"Sin módulo",Font,box,Color.FromArgb(60,75,90),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
}
