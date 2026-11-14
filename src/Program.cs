using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (Environment.GetCommandLineArgs().Contains("--smoke-test")) { MainForm.SmokeTest(); return; }
        Application.Run(new MainForm());
    }
}

internal sealed class Snapshot
{
    public readonly List<string[]> Summary = new List<string[]>();
    public readonly List<string[]> Drivers = new List<string[]>();
    public readonly List<HardwareItem> Hardware = new List<HardwareItem>();
    public readonly List<string> Warnings = new List<string>();
    public string Manufacturer = "", Model = "";
    public double? RamTotalGiB, RamAvailableGiB;
    public readonly List<VolumeInfo> Volumes = new List<VolumeInfo>();
    public bool PnpReadComplete;
    public readonly HashSet<string> ProblemDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

internal sealed partial class MainForm : Form
{
    readonly ListView summary = MakeList(new[] { "Propiedad", "Valor" });
    readonly ListView drivers = MakeList(new[] { "Dispositivo", "Proveedor", "Versión", "Fecha", "Estado PnP", "INF" });
    readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(10), AutoEllipsis = true };
    readonly FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(8) };
    Snapshot current;

    public MainForm()
    {
        Text = "Con-E · Diagnóstico y herramientas · v0.4.0";
        BackColor = CanvasColor;
        Size = new Size(1100, 800); MinimumSize = new Size(650, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 9) };
        var home = new TabPage("Mi equipo") { BackColor = CanvasColor }; home.Controls.Add(dashboard); tabs.TabPages.Add(home);
        var first = new TabPage("Detalles técnicos"); first.Controls.Add(summary);
        driverPage = new TabPage("Hardware y controladores"); driverPage.Controls.Add(BuildHardwareBrowser()); tabs.TabPages.Add(driverPage);
        var toolsPage = new TabPage("Herramientas") { BackColor = CanvasColor }; toolsPage.Controls.Add(BuildTools()); tabs.TabPages.Add(toolsPage);
        tabs.TabPages.Add(first);
        AddButton("Explicaciones", ToggleHelp);
        AddButton("Actualizar", async () => await RefreshData());
        AddButton("Guardar informe", Export);
        AddButton("Buscar fabricante", SearchManufacturer);
        AddButton("Abrir herramientas USB", () =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            if (Directory.Exists(path)) Open(path);
            else MessageBox.Show("No se encontró la carpeta Tools junto a Con-E.exe.");
        });
        Controls.Add(tabs); Controls.Add(actions); Controls.Add(BuildHeader()); Controls.Add(status);
        dashboard.Resize += (s, e) => ResizeCards();
        RenderDashboard();
        Shown += async (s, e) => { if (!Environment.GetCommandLineArgs().Contains("--smoke-test")) await RefreshData(); };
    }

    static ListView MakeList(string[] columns)
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false };
        foreach (var col in columns) list.Columns.Add(col, col == "Valor" ? 600 : col == "Dispositivo" ? 270 : 170);
        return list;
    }
    void AddButton(string title, Action action)
    {
        var button = new Button { Text = title, AutoSize = true, Padding = new Padding(5), Margin = new Padding(4) };
        button.Click += (s, e) => { try { action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Con-E"); } };
        actions.Controls.Add(button);
    }
    async Task RefreshData()
    {
        actions.Enabled = false; status.Text = "Leyendo información del equipo…";
        try
        {
            var data = await Task.Run(() => Collect());
            if (IsDisposed || Disposing) return;
            current = data;
            Fill(summary, data.Summary); Fill(drivers, data.Drivers); RenderDashboard(); RenderHardwareBrowser();
            status.Text = data.Warnings.Count == 0 ? "Lectura completada · " + data.Drivers.Count + " registros de controladores" : "Lectura parcial · " + string.Join(" | ", data.Warnings);
        }
        catch (Exception ex) { status.Text = "No fue posible completar la lectura: " + ex.Message; }
        finally { if (!IsDisposed && !Disposing) actions.Enabled = true; }
    }
    static void Fill(ListView list, List<string[]> rows)
    {
        list.BeginUpdate();
        try { list.Items.Clear(); foreach (var row in rows) list.Items.Add(new ListViewItem(row)); }
        finally { list.EndUpdate(); }
    }
    static string Value(ManagementBaseObject obj, string key)
    {
        var raw = obj[key]; var value = raw == null ? "" : Convert.ToString(raw).Trim();
        return string.IsNullOrWhiteSpace(value) ? "No disponible" : value;
    }
    static string Date(string value)
    {
        try { return ManagementDateTimeConverter.ToDateTime(value).ToString("dd-MM-yyyy HH:mm"); }
        catch { return "No disponible"; }
    }
    static void Query(Snapshot data, string label, string query, Action<ManagementBaseObject> accept)
    {
        try
        {
            using (var search = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\cimv2"), new ObjectQuery(query), new EnumerationOptions { Timeout = TimeSpan.FromSeconds(20), ReturnImmediately = false }))
            using (var rows = search.Get())
            {
                if (rows.Count == 0) data.Warnings.Add(label + ": sin datos");
                foreach (ManagementObject row in rows) using (row) accept(row);
            }
        }
        catch (Exception ex) { data.Warnings.Add(label + ": " + ex.Message); }
    }
    static Snapshot Collect()
    {
        var data = new Snapshot();
        Action<string, string> add = (k, v) => data.Summary.Add(new[] { k, v });
        add("Fecha de consulta", System.DateTime.Now.ToString("dd-MM-yyyy HH:mm"));
        Query(data, "Equipo", "SELECT Manufacturer,Model,Name,TotalPhysicalMemory,SystemType FROM Win32_ComputerSystem", o =>
        {
            data.Manufacturer = Value(o, "Manufacturer"); data.Model = Value(o, "Model");
            add("Fabricante del equipo", data.Manufacturer); add("Modelo del equipo", data.Model); add("Nombre del equipo", Value(o, "Name"));
            double bytes; add("Memoria RAM instalada", double.TryParse(Value(o, "TotalPhysicalMemory"), out bytes) ? (bytes / 1073741824d).ToString("0.##") + " GiB" : "No disponible");
            data.RamTotalGiB = double.TryParse(Value(o, "TotalPhysicalMemory"), out bytes) && bytes > 0 ? (double?)(bytes / 1073741824d) : null;
            add("Tipo de sistema", Value(o, "SystemType"));
        });
        Query(data, "Windows", "SELECT Caption,Version,BuildNumber,OSArchitecture,FreePhysicalMemory FROM Win32_OperatingSystem", o =>
        {
            add("Windows", Value(o, "Caption")); add("Versión / compilación", Value(o, "Version") + " / " + Value(o, "BuildNumber"));
            double freeKiB; if (double.TryParse(Value(o, "FreePhysicalMemory"), out freeKiB) && freeKiB >= 0) data.RamAvailableGiB = freeKiB / 1048576d;
            add("RAM disponible al consultar", data.RamAvailableGiB.HasValue ? data.RamAvailableGiB.Value.ToString("0.##") + " GiB" : "No disponible");
            add("Arquitectura de Windows", Value(o, "OSArchitecture"));
        });
        Query(data, "Procesador", "SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor", o =>
        { add("Procesador", Value(o, "Name")); add("Núcleos / procesadores lógicos", Value(o, "NumberOfCores") + " / " + Value(o, "NumberOfLogicalProcessors")); });
        Query(data, "BIOS", "SELECT Manufacturer,SMBIOSBIOSVersion,SerialNumber,ReleaseDate FROM Win32_BIOS", o =>
        { add("Fabricante de BIOS", Value(o, "Manufacturer")); add("Versión de BIOS", Value(o, "SMBIOSBIOSVersion")); add("Serie informada por BIOS", Value(o, "SerialNumber")); add("Fecha de BIOS", Date(Value(o, "ReleaseDate"))); });
        Query(data, "Identidad", "SELECT IdentifyingNumber FROM Win32_ComputerSystemProduct", o => add("Serie del equipo (SMBIOS)", Value(o, "IdentifyingNumber")));
        Query(data, "Placa base", "SELECT Manufacturer,Product,SerialNumber FROM Win32_BaseBoard", o =>
        { add("Fabricante de placa base", Value(o, "Manufacturer")); add("Modelo de placa base", Value(o, "Product")); add("Serie de placa base", Value(o, "SerialNumber")); });
        Query(data, "Discos", "SELECT Model,Size FROM Win32_DiskDrive", o =>
        { double bytes; add("Disco físico", Value(o, "Model") + (double.TryParse(Value(o, "Size"), out bytes) ? " · " + (bytes / 1073741824d).ToString("0.##") + " GiB" : "")); });
        Query(data, "Unidades", "SELECT DeviceID,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3", o =>
        { double total, free; if (double.TryParse(Value(o, "Size"), out total) && total > 0 && double.TryParse(Value(o, "FreeSpace"), out free) && free >= 0 && free <= total)
          { data.Volumes.Add(new VolumeInfo { Name = Value(o, "DeviceID"), TotalGiB = total / 1073741824d, FreeGiB = free / 1073741824d }); add("Unidad " + Value(o, "DeviceID"), (free / 1073741824d).ToString("0.##") + " GiB libres de " + (total / 1073741824d).ToString("0.##") + " GiB"); }
        });
        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var deviceRows = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        int pnpWarningCount = data.Warnings.Count;
        Query(data, "Estado PnP", "SELECT DeviceID,Name,Description,Manufacturer,PNPClass,HardwareID,CompatibleID,Service,Status,ConfigManagerErrorCode FROM Win32_PnPEntity", o =>
        {
            var id = Value(o, "DeviceID"); var code = Value(o, "ConfigManagerErrorCode");
            data.Hardware.Add(HardwareFromPnp(o));
            states[id] = code == "0" ? "Sin error PnP" : "Código PnP: " + code;
            int errorCode;
            if (int.TryParse(code, out errorCode) && errorCode != 0) { data.ProblemDevices.Add(id); deviceRows[id] = new[] { Value(o, "Name"), "No disponible", "No disponible", "No disponible", states[id], "Ver Administrador de dispositivos" }; }
        });
        data.PnpReadComplete = data.Warnings.Count == pnpWarningCount;
        Query(data, "Controladores", "SELECT DeviceID,DeviceName,DriverProviderName,DriverVersion,DriverDate,InfName,IsSigned FROM Win32_PnPSignedDriver", o =>
        {
            string state; states.TryGetValue(Value(o, "DeviceID"), out state);
            AttachDriver(data, o);
            deviceRows[Value(o, "DeviceID")] = new[] { Value(o, "DeviceName"), Value(o, "DriverProviderName"), Value(o, "DriverVersion"), Date(Value(o, "DriverDate")), state ?? "No disponible", Value(o, "InfName") };
        });
        data.Drivers.AddRange(deviceRows.OrderByDescending(pair => data.ProblemDevices.Contains(pair.Key)).ThenBy(pair => pair.Value[0]).Select(pair => pair.Value));
        CollectComponents(data);
        CollectExtra(data);
        return data;
    }
    void Export()
    {
        if (current == null) return;
        using (var dialog = new SaveFileDialog { Filter = "Informe de texto (*.txt)|*.txt", FileName = "Con-E_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt" })
        {
            if (dialog.ShowDialog() != DialogResult.OK) return;
            var text = new StringBuilder("Con-E v0.4.0\r\n\r\n");
            foreach (var row in current.Summary) text.AppendLine(row[0] + ": " + row[1]);
            text.AppendLine("\r\nCONTROLADORES (estado PnP; no garantiza buen funcionamiento)");
            foreach (var row in current.Drivers) text.AppendLine(string.Join(" | ", row));
            text.AppendLine("\r\nHARDWARE Y COMPONENTES");
            foreach (var item in current.Hardware) { text.AppendLine("\r\n[" + item.Category + "] " + item.Name); foreach (var row in item.Properties) text.AppendLine(row.Key + ": " + row.Value); }
            text.AppendLine("\r\nINCIDENCIAS DE LECTURA"); foreach (var warning in current.Warnings) text.AppendLine(warning);
            File.WriteAllText(dialog.FileName, text.ToString(), Encoding.UTF8);
            status.Text = "Informe guardado.";
        }
    }
    void SearchManufacturer()
    {
        if (current == null || string.IsNullOrWhiteSpace(current.Model) || current.Model == "No disponible") { MessageBox.Show("No se detectó un modelo utilizable."); return; }
        Open("https://www.bing.com/search?q=" + Uri.EscapeDataString(current.Manufacturer + " " + current.Model + " sitio oficial especificaciones"));
    }
    static void Open(string target) { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
}
