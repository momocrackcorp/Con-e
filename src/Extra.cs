using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    static void FitColumns(ListView list, double ratio)
    {
        if (list.Columns.Count < 2) return;
        int available = Math.Max(300, list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        list.Columns[0].Width = Math.Max(140, (int)(available * ratio));
        list.Columns[1].Width = Math.Max(160, available - list.Columns[0].Width);
    }
    void RevealOem(FlowLayoutPanel card)
    {
        if (card.Controls.OfType<Label>().Any(l => Convert.ToString(l.Tag) == "oem")) return;
        string key = "No hay una clave OEM disponible en firmware.";
        try
        {
            using (var query = new ManagementObjectSearcher("SELECT OA3xOriginalProductKey FROM SoftwareLicensingService"))
            using (var rows = query.Get()) foreach (ManagementObject row in rows) using (row)
            { string value = Convert.ToString(row["OA3xOriginalProductKey"]); if (!string.IsNullOrWhiteSpace(value)) key = value; }
        }
        catch { key = "No se pudo consultar la clave OEM."; }
        var label = Line(card, key); label.Tag = "oem"; ResizeCards();
    }
    static string RamType(string code)
    {
        switch (code) { case "20": return "DDR"; case "21": return "DDR2"; case "24": return "DDR3"; case "26": return "DDR4"; case "30": return "LPDDR4"; case "34": return "DDR5"; case "35": return "LPDDR5"; default: return "Tipo SMBIOS " + code; }
    }
    static void CollectExtra(Snapshot data)
    {
        Action<string,string> add = (k,v) => data.Summary.Add(new[] {k,v});
        int slots = 0; bool slotsKnown = false;
        Query(data, "Ranuras RAM", "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray WHERE Use=3", o => { int n; if (int.TryParse(Value(o,"MemoryDevices"), out n)) { slots += n; slotsKnown = true; } });
        CollectRamSlots(data,slotsKnown?slots:0);
        var modules = data.Hardware.Where(h => h.Category == "Memoria RAM").ToArray();
        add("Ranuras RAM", (slotsKnown ? slots + " posiciones informadas por firmware" : "Total de posiciones no disponible") + " · " + modules.Length + " módulos detectados. Puede incluir memoria soldada.");
        add("Módulos RAM", string.Join(Environment.NewLine, modules.Select(m => {
            string type,capacity,speed; m.Properties.TryGetValue("Tipo SMBIOS (código)",out type);m.Properties.TryGetValue("Capacidad (bytes)",out capacity);m.Properties.TryGetValue("Velocidad configurada (MHz)",out speed);
            double bytes; return m.Name + ": " + (double.TryParse(capacity,out bytes) ? (bytes/1073741824d).ToString("0.##") + " GiB" : "Capacidad no disponible") + " · " + RamType(type ?? "0") + " · " + speed + " MHz";
        })));
        string cpu = string.Join(" ",data.Summary.Where(r=>r[0]=="Procesador").Select(r=>r[1])).ToUpperInvariant();
        add("Marca del procesador", cpu.Contains("INTEL") ? "INTEL" : cpu.Contains("AMD") ? "AMD" : cpu.Contains("ARM") || cpu.Contains("QUALCOMM") ? "ARM" : "Fabricante no identificado");
        string dxgiIssue;data.VideoAdapters.AddRange(VideoMemory.ReadAdapters(out dxgiIssue));
        add("Memoria gráfica DXGI",VideoMemory.Format(data.VideoAdapters,dxgiIssue));
        var graphics = data.Hardware.Where(h=>h.Category=="Gráficos y monitores" && h.Properties.ContainsKey("Procesador gráfico informado")).ToArray();
        if(!data.VideoAdapters.Any(a=>!a.IsSoftware))foreach(var h in graphics)data.VideoAdapters.Add(new VideoAdapterInfo {Name=h.Name,Source="WMI",Vendor=VideoMemory.VendorFromPnp(h.DeviceId+" "+h.SearchId+" "+string.Join(" ",h.Properties.Values))});
        add("Adaptadores gráficos",string.Join(Environment.NewLine,graphics.Select(h=>h.Name)));
        add("Información gráfica",string.Join(Environment.NewLine,graphics.Select(h=>string.Join(Environment.NewLine,h.Properties.Where(p=>p.Key!="ID de instancia").Select(p=>p.Key+": "+p.Value)))));
        var networks=data.Hardware.Where(h=>h.Category=="Red" && h.Properties.ContainsKey("Adaptador físico según Windows") && h.Properties["Adaptador físico según Windows"].Equals("True",StringComparison.OrdinalIgnoreCase)).ToArray();
        add("Adaptadores de red",string.Join(Environment.NewLine,networks.Select(h=>h.Name)));
        add("Características de red",string.Join(Environment.NewLine,networks.Select(h=>string.Join(Environment.NewLine,h.Properties.Select(p=>p.Key+": "+p.Value)))));
        var parts=new List<string>();
        Query(data,"Particiones","SELECT DiskIndex,Index,Size,Type FROM Win32_DiskPartition",o=>{double size;parts.Add("Disco "+Value(o,"DiskIndex")+" · partición "+Value(o,"Index")+" · "+Value(o,"Type")+" · "+(double.TryParse(Value(o,"Size"),out size)?(size/1073741824d).ToString("0.##")+" GiB":"No disponible"));});
        add("Particiones",string.Join(Environment.NewLine,parts));
        var types=new List<string>();
        try
        {
            using(var query=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT FriendlyName,MediaType,BusType FROM MSFT_PhysicalDisk"))
            using(var rows=query.Get()) foreach(ManagementObject row in rows) using(row)
            {
                string media=Value(row,"MediaType"),bus=Value(row,"BusType");
                types.Add(Value(row,"FriendlyName")+" · "+(media=="4"?"SSD":media=="3"?"HDD":"Medio no determinado")+" · "+(bus=="17"?"NVMe":bus=="8"?"RAID":bus=="11"?"SATA":bus=="13"?"MMC/eMMC":bus=="12"?"SD":"Bus "+bus));
            }
        } catch { types.Add("Tipo de medio no disponible desde Storage WMI"); }
        add("Tipos de almacenamiento",string.Join(Environment.NewLine,types)+Environment.NewLine+"M.2 es un formato físico; no se deduce de SSD o NVMe.");
    }
}
