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
        Application.Run(new MainForm());
    }
}

internal sealed class Snapshot
{
    public readonly List<string[]> Summary = new List<string[]>();
    public readonly List<string[]> Drivers = new List<string[]>();
    public readonly List<string> Warnings = new List<string>();
    public string Manufacturer = "", Model = "";
}

internal sealed class MainForm : Form
{
    readonly ListView summary = MakeList(new[] { "Propiedad", "Valor" });
    readonly ListView drivers = MakeList(new[] { "Dispositivo", "Proveedor", "Versión", "Fecha", "Estado PnP", "INF" });
    readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(10), AutoEllipsis = true };
    readonly FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(8) };
    Snapshot current;

    public MainForm()
    {
        Text = "Con-E · Diagnóstico y herramientas · v0.1.0";
        Size = new Size(980, 680); MinimumSize = new Size(650, 450);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var first = new TabPage("Mi equipo"); first.Controls.Add(summary); tabs.TabPages.Add(first);
        var second = new TabPage("Controladores"); second.Controls.Add(drivers); tabs.TabPages.Add(second);
        AddButton("Actualizar", async () => await RefreshData());
        AddButton("Guardar informe", Export);
        AddButton("Buscar fabricante", SearchManufacturer);
        AddButton("Abrir herramientas USB", () =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            if (Directory.Exists(path)) Open(path);
            else MessageBox.Show("No se encontró la carpeta Tools junto a Con-E.exe.");
        });
        Controls.Add(tabs); Controls.Add(actions); Controls.Add(status);
        Shown += async (s, e) => await RefreshData();
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
            current = data;
            Fill(summary, data.Summary); Fill(drivers, data.Drivers);
            status.Text = data.Warnings.Count == 0 ? "Lectura completada · " + data.Drivers.Count + " registros de controladores" : "Lectura parcial · " + string.Join(" | ", data.Warnings);
        }
        catch (Exception ex) { status.Text = "No fue posible completar la lectura: " + ex.Message; }
        finally { actions.Enabled = true; }
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
            double bytes; add("Memoria RAM instalada", double.TryParse(Value(o, "TotalPhysicalMemory"), out bytes) ? (bytes / 1073741824d).ToString("0.##") + " GB" : "No disponible");
            add("Tipo de sistema", Value(o, "SystemType"));
        });
        Query(data, "Windows", "SELECT Caption,Version,BuildNumber,OSArchitecture,InstallDate FROM Win32_OperatingSystem", o =>
        {
            add("Windows", Value(o, "Caption")); add("Versión / compilación", Value(o, "Version") + " / " + Value(o, "BuildNumber"));
            add("Arquitectura de Windows", Value(o, "OSArchitecture")); add("Instalación registrada por Windows", Date(Value(o, "InstallDate")));
        });
        add("Último formateo", "No determinable de forma fiable con estos datos");
        add("Nota sobre instalación", "La fecha registrada puede cambiar con una actualización o reinstalación; no acredita un formateo.");
        Query(data, "Procesador", "SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor", o =>
        { add("Procesador", Value(o, "Name")); add("Núcleos / procesadores lógicos", Value(o, "NumberOfCores") + " / " + Value(o, "NumberOfLogicalProcessors")); });
        Query(data, "BIOS", "SELECT Manufacturer,SMBIOSBIOSVersion,SerialNumber,ReleaseDate FROM Win32_BIOS", o =>
        { add("Fabricante de BIOS", Value(o, "Manufacturer")); add("Versión de BIOS", Value(o, "SMBIOSBIOSVersion")); add("Serie informada por BIOS", Value(o, "SerialNumber")); add("Fecha de BIOS", Date(Value(o, "ReleaseDate"))); });
        Query(data, "Identidad", "SELECT IdentifyingNumber FROM Win32_ComputerSystemProduct", o => add("Serie del equipo (SMBIOS)", Value(o, "IdentifyingNumber")));
        Query(data, "Placa base", "SELECT Manufacturer,Product,SerialNumber FROM Win32_BaseBoard", o =>
        { add("Fabricante de placa base", Value(o, "Manufacturer")); add("Modelo de placa base", Value(o, "Product")); add("Serie de placa base", Value(o, "SerialNumber")); });
        Query(data, "Discos", "SELECT Model,Size FROM Win32_DiskDrive", o =>
        { double bytes; add("Disco físico", Value(o, "Model") + (double.TryParse(Value(o, "Size"), out bytes) ? " · " + (bytes / 1073741824d).ToString("0.##") + " GiB" : "")); });
        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Query(data, "Estado PnP", "SELECT DeviceID,Name,ConfigManagerErrorCode FROM Win32_PnPEntity", o =>
        {
            var id = Value(o, "DeviceID"); var code = Value(o, "ConfigManagerErrorCode");
            states[id] = code == "0" ? "Sin error PnP" : "Código PnP: " + code;
            if (code != "0") data.Drivers.Add(new[] { Value(o, "Name"), "No consultado", "No disponible", "No disponible", states[id], "Ver Administrador de dispositivos" });
        });
        Query(data, "Controladores", "SELECT DeviceID,DeviceName,DriverProviderName,DriverVersion,DriverDate,InfName FROM Win32_PnPSignedDriver", o =>
        {
            string state; states.TryGetValue(Value(o, "DeviceID"), out state);
            data.Drivers.Add(new[] { Value(o, "DeviceName"), Value(o, "DriverProviderName"), Value(o, "DriverVersion"), Date(Value(o, "DriverDate")), state ?? "No disponible", Value(o, "InfName") });
        });
        return data;
    }
    void Export()
    {
        if (current == null) return;
        using (var dialog = new SaveFileDialog { Filter = "Informe de texto (*.txt)|*.txt", FileName = "Con-E_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt" })
        {
            if (dialog.ShowDialog() != DialogResult.OK) return;
            var text = new StringBuilder("Con-E v0.1.0\r\n\r\n");
            foreach (var row in current.Summary) text.AppendLine(row[0] + ": " + row[1]);
            text.AppendLine("\r\nCONTROLADORES (estado PnP; no garantiza buen funcionamiento)");
            foreach (var row in current.Drivers) text.AppendLine(string.Join(" | ", row));
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
