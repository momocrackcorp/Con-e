using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows.Forms;

internal sealed class HardwareItem
{
    public string Name = "No identificado", Category = "Otros dispositivos", DeviceId = "", SearchId = "";
    public int? ErrorCode;
    public readonly Dictionary<string, string> Properties = new Dictionary<string, string>();
}

internal sealed partial class MainForm
{
    readonly TreeView hardwareTree = new TreeView { Dock = DockStyle.Left, Width = 185, HideSelection = false, FullRowSelect = true, BorderStyle = BorderStyle.None };
    readonly ListView hardwareList = MakeList(new[] { "Componente", "Estado" });
    readonly ListView hardwareDetails = MakeList(new[] { "Propiedad", "Valor" });
    readonly Label hardwareTitle = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10), ForeColor = TextColor };
    HardwareItem selectedHardware;
    bool renderingHardware;

    Control BuildHardwareBrowser()
    {
        var area = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = CanvasColor };
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 7, Size = new Size(650, 600), SplitterDistance = 220, Panel1MinSize = 80, Panel2MinSize = 120 };
        split.Panel1.Controls.Add(hardwareList); split.Panel2.Controls.Add(hardwareDetails);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, Padding = new Padding(0, 5, 0, 5) };
        toolbar.Controls.Add(ActionButton("Copiar ficha", () => { if (selectedHardware != null) Clipboard.SetText(selectedHardware.Name + Environment.NewLine + string.Join(Environment.NewLine, selectedHardware.Properties.Select(p => p.Key + ": " + p.Value))); }));
        toolbar.Controls.Add(ActionButton("Buscar ID en internet", () =>
        {
            if (selectedHardware == null || string.IsNullOrEmpty(selectedHardware.SearchId)) { status.Text = "Este componente no proporciona un ID de hardware para buscar."; return; }
            Open("https://www.bing.com/search?q=" + Uri.EscapeDataString(selectedHardware.SearchId + " hardware fabricante controlador"));
        }));
        right.Controls.Add(split); right.Controls.Add(hardwareTitle); right.Controls.Add(toolbar);
        area.Controls.Add(right); area.Controls.Add(hardwareTree);
        var images = new ImageList { ImageSize = new Size(24, 24), ColorDepth = ColorDepth.Depth32Bit };
        foreach (string category in Categories) images.Images.Add(category, ComponentPicture.Icon(category));
        hardwareTree.ImageList = images;
        area.Disposed += (s, e) => images.Dispose();
        hardwareTree.AfterSelect += (s, e) => { if (!renderingHardware) ShowHardwareCategory(Convert.ToString(e.Node.Tag)); };
        hardwareList.SelectedIndexChanged += (s, e) =>
        {
            if (hardwareList.SelectedItems.Count == 0) return;
            selectedHardware = hardwareList.SelectedItems[0].Tag as HardwareItem;
            if (selectedHardware == null) return;
            hardwareTitle.Text = selectedHardware.Name;
            Fill(hardwareDetails, selectedHardware.Properties.Select(p => new[] { p.Key, p.Value }).ToList());
        };
        return area;
    }
    static readonly string[] Categories = { "Todos los componentes", "Por revisar", "Procesadores", "Memoria RAM", "Placa base", "BIOS", "Almacenamiento", "Gráficos y monitores", "Red", "Audio", "USB", "Sistema", "Otros dispositivos" };
    void RenderHardwareBrowser()
    {
        string selected = hardwareTree.SelectedNode == null ? Categories[0] : Convert.ToString(hardwareTree.SelectedNode.Tag);
        renderingHardware = true; hardwareTree.BeginUpdate();
        try
        {
            hardwareTree.Nodes.Clear();
            foreach (string category in Categories)
            {
                int count = current == null ? 0 : HardwareInCategory(category).Count();
                var node = new TreeNode(category + " (" + count + ")") { Tag = category, ImageKey = category, SelectedImageKey = category };
                hardwareTree.Nodes.Add(node); if (category == selected) hardwareTree.SelectedNode = node;
            }
        }
        finally { hardwareTree.EndUpdate(); renderingHardware = false; }
        ShowHardwareCategory(selected);
    }
    IEnumerable<HardwareItem> HardwareInCategory(string category)
    {
        if (current == null) return Enumerable.Empty<HardwareItem>();
        return current.Hardware.Where(h => category == Categories[0] || (category == "Por revisar" ? h.ErrorCode.HasValue && h.ErrorCode.Value != 0 : h.Category == category));
    }
    void ShowHardwareCategory(string category)
    {
        selectedHardware = null; hardwareDetails.Items.Clear(); hardwareTitle.Text = "Selecciona un componente para ver sus características y su controlador.";
        hardwareList.BeginUpdate();
        try
        {
            hardwareList.Items.Clear();
            foreach (var item in HardwareInCategory(category).OrderByDescending(h => h.ErrorCode.GetValueOrDefault() != 0).ThenBy(h => h.Name))
            {
                var row = new ListViewItem(new[] { item.Name, item.ErrorCode.HasValue ? PnpMeaning(item.ErrorCode.Value) : "Sin estado PnP disponible" }) { Tag = item };
                if (item.ErrorCode.GetValueOrDefault() != 0) row.ForeColor = Color.FromArgb(145, 85, 0);
                hardwareList.Items.Add(row);
            }
            if (hardwareList.Items.Count > 0) hardwareList.Items[0].Selected = true;
        }
        finally { hardwareList.EndUpdate(); }
    }
    static string ArrayValue(ManagementBaseObject obj, string key)
    {
        var array = obj[key] as string[];
        return array == null || array.Length == 0 ? "No disponible" : string.Join(Environment.NewLine, array);
    }
    static string PnpMeaning(int code)
    {
        switch (code)
        {
            case 0: return "Sin error PnP informado";
            case 10: return "Código 10: no puede iniciar";
            case 22: return "Código 22: dispositivo deshabilitado";
            case 28: return "Código 28: controladores no instalados";
            case 31: return "Código 31: Windows no puede cargar el controlador";
            case 43: return "Código 43: el dispositivo informó un problema";
            default: return "Código PnP " + code + ": requiere revisión";
        }
    }
    static string CategoryFor(string pnpClass, string id)
    {
        switch (pnpClass.ToUpperInvariant())
        {
            case "PROCESSOR": return "Procesadores";
            case "DISPLAY": case "MONITOR": return "Gráficos y monitores";
            case "NET": return "Red";
            case "MEDIA": case "AUDIOENDPOINT": return "Audio";
            case "DISKDRIVE": case "HDC": case "SCSIADAPTER": case "VOLUME": return "Almacenamiento";
            case "USB": return "USB";
            case "SYSTEM": case "COMPUTER": case "FIRMWARE": return "Sistema";
            default: return id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ? "USB" : "Otros dispositivos";
        }
    }
    static HardwareItem HardwareFromPnp(ManagementBaseObject o)
    {
        var item = new HardwareItem { Name = Value(o, "Name"), DeviceId = Value(o, "DeviceID") };
        item.Category = CategoryFor(Value(o, "PNPClass"), item.DeviceId);
        int code; if (int.TryParse(Value(o, "ConfigManagerErrorCode"), out code)) item.ErrorCode = code;
        item.Properties["Nombre informado por Windows"] = item.Name;
        item.Properties["Descripción"] = Value(o, "Description");
        item.Properties["Fabricante informado por Windows"] = Value(o, "Manufacturer");
        item.Properties["Clase PnP"] = Value(o, "PNPClass");
        item.Properties["Estado PnP"] = item.ErrorCode.HasValue ? PnpMeaning(code) : "No disponible";
        item.Properties["Servicio"] = Value(o, "Service");
        item.Properties["ID de instancia"] = item.DeviceId;
        item.Properties["IDs de hardware"] = ArrayValue(o, "HardwareID");
        item.Properties["IDs compatibles"] = ArrayValue(o, "CompatibleID");
        var ids = o["HardwareID"] as string[];
        item.SearchId = ids == null ? "" : ids.FirstOrDefault() ?? "";
        ParseIdentifiers(item);
        item.Properties["Controlador"] = "No encontrado en el inventario de controladores firmados; no confirma ausencia";
        return item;
    }
    static void ParseIdentifiers(HardwareItem item)
    {
        string raw = item.SearchId.Length == 0 ? item.DeviceId : item.SearchId;
        foreach (var pair in new[] { new[] { "VEN", "Fabricante PCI (VEN)" }, new[] { "DEV", "Dispositivo PCI (DEV)" }, new[] { "VID", "Fabricante USB (VID)" }, new[] { "PID", "Producto USB (PID)" }, new[] { "SUBSYS", "Subsistema PCI" }, new[] { "REV", "Revisión de hardware" } })
        {
            var match = Regex.Match(raw, @"(?:\\|&)" + pair[0] + @"_([0-9A-F]+)", RegexOptions.IgnoreCase);
            if (match.Success) item.Properties[pair[1]] = match.Groups[1].Value.ToUpperInvariant();
        }
        item.Properties["Identificación"] = "IDs leídos de Windows. Sin base de modelos integrada: el ID no confirma por sí solo el nombre comercial ni las prestaciones.";
    }
    static void AttachDriver(Snapshot data, ManagementBaseObject o)
    {
        string id = Value(o, "DeviceID");
        var item = data.Hardware.FirstOrDefault(h => string.Equals(h.DeviceId, id, StringComparison.OrdinalIgnoreCase));
        if (item == null)
        {
            item = new HardwareItem { Name = Value(o, "DeviceName"), DeviceId = id };
            item.Properties["ID de instancia"] = id; data.Hardware.Add(item);
        }
        item.Properties["Controlador"] = "Registro de controlador localizado";
        item.Properties["Proveedor del controlador"] = Value(o, "DriverProviderName");
        item.Properties["Versión del controlador"] = Value(o, "DriverVersion");
        item.Properties["Fecha del controlador"] = Date(Value(o, "DriverDate"));
        item.Properties["Archivo INF"] = Value(o, "InfName");
        item.Properties["Firmado según Windows"] = Value(o, "IsSigned");
    }
    static void ComponentQuery(Snapshot data, string category, string className, string nameKey, string[,] fields)
    {
        string columns = string.Join(",", Enumerable.Range(0, fields.GetLength(0)).Select(i => fields[i, 0]).Concat(new[] { nameKey }).Distinct());
        Query(data, category, "SELECT " + columns + " FROM " + className, o =>
        {
            string id = "";
            for (int i = 0; i < fields.GetLength(0); i++) if (fields[i, 0] == "PNPDeviceID") id = Value(o, "PNPDeviceID");
            var item = id == "" || id == "No disponible" ? null : data.Hardware.FirstOrDefault(h => string.Equals(h.DeviceId, id, StringComparison.OrdinalIgnoreCase));
            if (item == null) { item = new HardwareItem { Name = Value(o, nameKey), Category = category, DeviceId = id }; data.Hardware.Add(item); }
            else item.Category = category;
            item.Properties["Fuente de características"] = className + " (WMI de Windows)";
            for (int i = 0; i < fields.GetLength(0); i++) item.Properties[fields[i, 1]] = Value(o, fields[i, 0]);
        });
    }
    static void CollectComponents(Snapshot data)
    {
        ComponentQuery(data, "Procesadores", "Win32_Processor", "Name", new[,] { { "Name", "Modelo" }, { "Manufacturer", "Fabricante" }, { "NumberOfCores", "Núcleos" }, { "NumberOfLogicalProcessors", "Hilos" }, { "MaxClockSpeed", "Frecuencia máxima informada (MHz)" }, { "SocketDesignation", "Socket" }, { "L2CacheSize", "Caché L2 (KiB)" }, { "L3CacheSize", "Caché L3 (KiB)" } });
        ComponentQuery(data, "Memoria RAM", "Win32_PhysicalMemory", "DeviceLocator", new[,] { { "DeviceLocator", "Ranura" }, { "Manufacturer", "Fabricante" }, { "PartNumber", "Número de pieza" }, { "SerialNumber", "Número de serie" }, { "Capacity", "Capacidad (bytes)" }, { "Speed", "Velocidad informada (MHz)" }, { "ConfiguredClockSpeed", "Velocidad configurada (MHz)" }, { "SMBIOSMemoryType", "Tipo SMBIOS (código)" } });
        ComponentQuery(data, "Placa base", "Win32_BaseBoard", "Product", new[,] { { "Manufacturer", "Fabricante" }, { "Product", "Modelo" }, { "SerialNumber", "Número de serie" }, { "Version", "Versión" } });
        ComponentQuery(data, "BIOS", "Win32_BIOS", "SMBIOSBIOSVersion", new[,] { { "Manufacturer", "Fabricante" }, { "SMBIOSBIOSVersion", "Versión" }, { "SerialNumber", "Serie informada por BIOS" } });
        ComponentQuery(data, "Almacenamiento", "Win32_DiskDrive", "Model", new[,] { { "PNPDeviceID", "ID PnP" }, { "Model", "Modelo" }, { "SerialNumber", "Número de serie" }, { "Size", "Capacidad (bytes)" }, { "InterfaceType", "Interfaz informada" }, { "FirmwareRevision", "Firmware" }, { "MediaType", "Tipo de medio informado" } });
        ComponentQuery(data, "Gráficos y monitores", "Win32_VideoController", "Name", new[,] { { "PNPDeviceID", "ID PnP" }, { "Name", "Modelo informado" }, { "VideoProcessor", "Procesador gráfico informado" }, { "DriverVersion", "Versión gráfica" }, { "CurrentHorizontalResolution", "Resolución horizontal" }, { "CurrentVerticalResolution", "Resolución vertical" } });
        ComponentQuery(data, "Red", "Win32_NetworkAdapter", "Name", new[,] { { "PNPDeviceID", "ID PnP" }, { "Name", "Nombre" }, { "Manufacturer", "Fabricante" }, { "AdapterType", "Tipo de adaptador" }, { "PhysicalAdapter", "Adaptador físico según Windows" } });
        ComponentQuery(data, "Audio", "Win32_SoundDevice", "Name", new[,] { { "PNPDeviceID", "ID PnP" }, { "Name", "Nombre" }, { "Manufacturer", "Fabricante" }, { "Status", "Estado informado" } });
    }
}

internal sealed class ComponentPicture : Control
{
    public string Kind = "";
    public ComponentPicture() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    public static Bitmap Icon(string kind)
    {
        var bmp = new Bitmap(48, 48); using (var g = Graphics.FromImage(bmp)) PaintSymbol(g, new Rectangle(0, 0, 48, 48), kind); return bmp;
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); PaintSymbol(e.Graphics, ClientRectangle, Kind); }
    static void PaintSymbol(Graphics g, Rectangle bounds, string kind)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var state = g.Save(); g.TranslateTransform(bounds.Left, bounds.Top); g.ScaleTransform(bounds.Width / 64f, bounds.Height / 64f);
        using (var fill = new SolidBrush(Color.FromArgb(232, 240, 253))) g.FillEllipse(fill, 0, 0, 64, 64);
        using (var pen = new Pen(Color.FromArgb(40, 91, 181), 2.5f))
        {
            if (kind.Contains("RAM") || kind.Contains("Memoria"))
            {
                g.DrawRectangle(pen, 9, 21, 46, 22); for (int i = 0; i < 3; i++) g.DrawRectangle(pen, 15 + i * 13, 26, 8, 10);
                for (int i = 0; i < 7; i++) g.DrawLine(pen, 13 + i * 6, 44, 13 + i * 6, 48);
            }
            else if (kind.Contains("Procesador") || kind.Contains("Procesadores") || kind.Contains("BIOS") || kind.Contains("placa") || kind.Contains("Placa"))
            {
                g.DrawRectangle(pen, 18, 18, 28, 28); g.DrawRectangle(pen, 24, 24, 16, 16);
                for (int i = 0; i < 4; i++) { int x = 21 + i * 7; g.DrawLine(pen, x, 11, x, 17); g.DrawLine(pen, x, 47, x, 53); g.DrawLine(pen, 11, x, 17, x); g.DrawLine(pen, 47, x, 53, x); }
            }
            else if (kind.Contains("Almacenamiento")) { g.DrawRectangle(pen, 15, 12, 34, 40); g.DrawEllipse(pen, 22, 18, 20, 20); g.DrawLine(pen, 32, 28, 43, 42); g.DrawLine(pen, 21, 46, 28, 46); }
            else if (kind.Contains("Red") || kind.Contains("USB")) { g.DrawRectangle(pen, 24, 9, 16, 12); g.DrawLine(pen, 32, 22, 32, 36); g.DrawLine(pen, 13, 36, 51, 36); g.DrawLine(pen, 13, 36, 13, 44); g.DrawLine(pen, 51, 36, 51, 44); g.DrawRectangle(pen, 7, 44, 13, 10); g.DrawRectangle(pen, 44, 44, 13, 10); }
            else if (kind.Contains("Audio")) { g.DrawLines(pen, new[] { new Point(15, 26), new Point(23, 26), new Point(34, 16), new Point(34, 48), new Point(23, 38), new Point(15, 38), new Point(15, 26) }); g.DrawArc(pen, 27, 14, 25, 35, -60, 120); }
            else if (kind.Contains("revisar") || kind.Contains("parcial")) { g.DrawPolygon(pen, new[] { new Point(32, 11), new Point(55, 51), new Point(9, 51) }); g.DrawLine(pen, 32, 25, 32, 37); g.DrawEllipse(pen, 31, 43, 2, 2); }
            else { g.DrawRectangle(pen, 10, 13, 44, 30); g.DrawLine(pen, 32, 44, 32, 51); g.DrawLine(pen, 23, 52, 41, 52); }
        }
        g.Restore(state);
    }
}
internal sealed class UsageDial : Control
{
    public double Value;
    public UsageDial() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int size = Math.Min(Width, Height) - 16; var box = new Rectangle((Width - size) / 2, 8, size, size);
        using (var pen = new Pen(Color.FromArgb(231, 237, 245), 8)) e.Graphics.DrawEllipse(pen, box);
        using (var pen = new Pen(Color.FromArgb(70, 121, 201), 8)) e.Graphics.DrawArc(pen, box, -90, (float)Math.Max(0, Math.Min(360, Value * 3.6)));
        TextRenderer.DrawText(e.Graphics, Value.ToString("0") + " %", Font, ClientRectangle, Color.FromArgb(35, 50, 71), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
