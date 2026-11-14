using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal sealed class VolumeInfo
{
    public string Name;
    public double TotalGiB, FreeGiB;
}

internal sealed partial class MainForm
{
    static readonly Color CanvasColor = Color.FromArgb(241, 244, 248);
    static readonly Color TextColor = Color.FromArgb(35, 50, 71);
    static readonly Color MutedColor = Color.FromArgb(80, 98, 121);
    static readonly Color BlueColor = Color.FromArgb(40, 91, 181);
    readonly FlowLayoutPanel dashboard = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(14), BackColor = CanvasColor };
    readonly List<FlowLayoutPanel> cards = new List<FlowLayoutPanel>();
    readonly List<Label> helpLabels = new List<Label>();
    TabControl tabs;
    TabPage driverPage;
    bool helpVisible;
    bool resizingCards;

    Control BuildHeader()
    {
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, BackColor = Color.White, Padding = new Padding(20, 12, 20, 12) };
        var name = new Label { Text = "Con-E", AutoSize = true, Font = new Font(Font.FontFamily, 22, FontStyle.Bold), ForeColor = BlueColor, Margin = new Padding(0, 0, 18, 0) };
        header.Controls.Add(name);
        header.Controls.Add(new Label { Text = "Tu computador, más claro.  ·  v0.2.0", AutoSize = true, ForeColor = MutedColor, Margin = new Padding(0, 12, 0, 0) });
        return header;
    }
    Control BuildTools()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(22) };
        panel.Controls.Add(new Label { Text = "Herramientas del USB", AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = TextColor });
        panel.Controls.Add(new Label { Text = "Agrega tus aplicaciones portátiles a la carpeta Tools junto a Con-E.exe.", AutoSize = true, MaximumSize = new Size(500, 0), Margin = new Padding(0, 10, 0, 15) });
        panel.Controls.Add(ActionButton("Abrir carpeta Tools", () =>
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            if (!Directory.Exists(path)) { MessageBox.Show("No se encontró la carpeta Tools junto a Con-E.exe.", "Con-E"); return; }
            Open(path);
        }));
        panel.Controls.Add(ActionButton("Guardar informe del equipo", Export));
        return panel;
    }
    Button ActionButton(string caption, Action action)
    {
        var button = new Button { Text = caption, AutoSize = true, Padding = new Padding(5), Margin = new Padding(0, 8, 0, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = BlueColor };
        button.FlatAppearance.BorderColor = Color.FromArgb(200, 213, 230);
        button.Click += (s, e) => { try { action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Con-E"); } };
        return button;
    }
    string Read(string key)
    {
        if (current == null) return "Pendiente de lectura";
        var values = current.Summary.Where(r => r[0] == key).Select(r => r[1]).ToArray();
        return values.Length == 0 ? "No disponible" : string.Join(" · ", values);
    }
    FlowLayoutPanel Card(string glyph, string title)
    {
        var card = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18), Margin = new Padding(7), BackColor = Color.White, Width = 300 };
        var heading = new Label { Text = glyph + "  " + title, AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = BlueColor, Margin = new Padding(0, 0, 0, 14) };
        card.Controls.Add(heading); cards.Add(card); dashboard.Controls.Add(card); return card;
    }
    Label Line(FlowLayoutPanel card, string text, bool prominent = false)
    {
        var label = new Label { Text = text, AutoSize = true, ForeColor = prominent ? TextColor : MutedColor, Font = prominent ? new Font(Font.FontFamily, 14, FontStyle.Bold) : Font, Margin = new Padding(0, 0, 0, 8), UseMnemonic = false };
        card.Controls.Add(label); return label;
    }
    void Help(FlowLayoutPanel card, string text)
    {
        var label = Line(card, text); label.Visible = helpVisible; helpLabels.Add(label);
    }
    void Meter(FlowLayoutPanel card, double percent, string accessibleName)
    {
        var meter = new UsageBar { Value = Math.Max(0, Math.Min(100, percent)), Height = 10, Width = 220, AccessibleName = accessibleName + ": " + percent.ToString("0") + " %", Margin = new Padding(0, 4, 0, 12) };
        card.Controls.Add(meter);
    }
    void Details(string title, params string[] keys)
    {
        var text = string.Join(Environment.NewLine + Environment.NewLine, keys.Select(k => k + ": " + Read(k)));
        MessageBox.Show(this, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    void ToggleHelp()
    {
        helpVisible = !helpVisible;
        foreach (var label in helpLabels) label.Visible = helpVisible;
        ResizeCards();
    }
    void RenderDashboard()
    {
        dashboard.SuspendLayout();
        try
        {
            foreach (Control control in dashboard.Controls.Cast<Control>().ToArray()) control.Dispose();
            cards.Clear(); helpLabels.Clear();
            var identity = Card("▣", "Mi equipo");
            Line(identity, Read("Fabricante del equipo") + " · " + Read("Modelo del equipo"), true);
            Line(identity, "Serie: " + Read("Serie del equipo (SMBIOS)"));
            Line(identity, "Nombre: " + Read("Nombre del equipo"));
            identity.Controls.Add(ActionButton("Copiar número de serie", () => { if (current != null) Clipboard.SetText(Read("Serie del equipo (SMBIOS)")); }));
            identity.Controls.Add(ActionButton("Buscar fabricante", SearchManufacturer));
            Help(identity, "El modelo y la serie son los datos registrados por el fabricante en el equipo.");

            var cpu = Card("⚙", "Procesador");
            Line(cpu, Read("Procesador"), true); Line(cpu, "Núcleos / hilos: " + Read("Núcleos / procesadores lógicos"));
            Help(cpu, "Ejecuta las instrucciones de los programas. Los núcleos permiten repartir tareas.");

            var ram = Card("▤", "Memoria RAM");
            Line(ram, Read("Memoria RAM instalada"), true);
            if (current != null && current.RamTotalGiB.HasValue && current.RamAvailableGiB.HasValue && current.RamTotalGiB.Value > 0 && current.RamAvailableGiB.Value <= current.RamTotalGiB.Value)
            {
                double used = current.RamTotalGiB.Value - current.RamAvailableGiB.Value;
                double percent = used / current.RamTotalGiB.Value * 100;
                Meter(ram, percent, "RAM utilizada al consultar");
                Line(ram, used.ToString("0.0") + " GiB en uso · " + percent.ToString("0") + " %");
                Line(ram, "Disponible: " + current.RamAvailableGiB.Value.ToString("0.0") + " GiB");
            }
            else Line(ram, "Uso de RAM: no disponible");
            Help(ram, "Es la memoria de trabajo de los programas abiertos. Pulsa Actualizar para obtener otra lectura; no es un monitor continuo.");

            var disk = Card("▰", "Almacenamiento");
            if (current == null || current.Volumes.Count == 0) Line(disk, current == null ? "Pendiente de lectura" : "No se pudieron leer las unidades.");
            else foreach (var volume in current.Volumes)
            {
                Line(disk, "Unidad " + volume.Name, true);
                Meter(disk, 100 * (volume.TotalGiB - volume.FreeGiB) / volume.TotalGiB, "Espacio ocupado " + volume.Name);
                Line(disk, volume.FreeGiB.ToString("0.0") + " GiB libres de " + volume.TotalGiB.ToString("0.0") + " GiB");
            }
            disk.Controls.Add(ActionButton("Ver discos físicos", () => Details("Discos físicos", "Disco físico")));
            Help(disk, "La barra indica espacio ocupado. No evalúa la salud del disco. Cada unidad puede ser una partición de un mismo disco físico.");

            var windows = Card("⊞", "Windows");
            Line(windows, Read("Windows"), true); Line(windows, Read("Arquitectura de Windows"));
            windows.Controls.Add(ActionButton("Instalación y formateo", () => Details("Windows", "Versión / compilación", "Instalación registrada por Windows", "Último formateo", "Nota sobre instalación")));
            Help(windows, "La fecha de instalación no acredita un formateo: puede cambiar con una reinstalación o actualización.");

            var bios = Card("▦", "BIOS y placa base");
            Line(bios, Read("Versión de BIOS"), true); Line(bios, Read("Fabricante de BIOS")); Line(bios, "Fecha: " + Read("Fecha de BIOS"));
            bios.Controls.Add(ActionButton("Ver modelo y números de serie", () => Details("BIOS y placa base", "Fabricante de placa base", "Modelo de placa base", "Serie de placa base", "Serie informada por BIOS")));
            Help(bios, "La BIOS inicia el hardware antes de cargar Windows. Las series del equipo y de la placa pueden ser diferentes.");

            var driver = Card("⚒", "Controladores");
            var driverText = current == null ? "Pendiente de lectura" : !current.PnpReadComplete ? "Lectura PnP incompleta" : current.ProblemDevices.Count == 0 ? "Sin error PnP informado" : current.ProblemDevices.Count + " dispositivo(s) por revisar";
            var driverState = Line(driver, driverText, true);
            if (current != null && (current.ProblemDevices.Count > 0 || !current.PnpReadComplete)) driverState.ForeColor = Color.FromArgb(140, 85, 0);
            Line(driver, current == null ? "" : current.Drivers.Count + " dispositivos en inventario");
            driver.Controls.Add(ActionButton("Revisar dispositivos", () => tabs.SelectedTab = driverPage));
            Help(driver, "Permiten que Windows se comunique con el hardware. La ausencia de errores PnP no garantiza buen funcionamiento.");
            if (current != null && current.Warnings.Count > 0)
            {
                var warning = Card("!", "Lectura parcial");
                Line(warning, current.Warnings.Count + " consulta(s) con datos incompletos", true);
                warning.Controls.Add(ActionButton("Ver incidencias de lectura", () => MessageBox.Show(this, string.Join(Environment.NewLine + Environment.NewLine, current.Warnings), "Incidencias de lectura")));
            }
        }
        finally { dashboard.ResumeLayout(true); ResizeCards(); }
    }
    void ResizeCards()
    {
        if (resizingCards || cards.Count == 0) return;
        resizingCards = true;
        try
        {
            dashboard.SuspendLayout();
            // Reserve a scrollbar even before overflow, so wrapping does not oscillate.
            int usable = Math.Max(220, dashboard.ClientSize.Width - dashboard.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            float scale = DeviceDpi / 96f;
            int columns = usable >= 950 * scale ? 3 : usable >= 590 * scale ? 2 : 1;
            int width = Math.Max(180, usable / columns - 14);
            foreach (var card in cards)
            {
                card.MinimumSize = new Size(width, 0); card.MaximumSize = new Size(width, 0); card.Width = width;
                int inner = Math.Max(80, width - card.Padding.Horizontal);
                foreach (Control child in card.Controls)
                {
                    if (child is Label) child.MaximumSize = new Size(inner, 0);
                    else if (child is UsageBar) child.Width = inner;
                    else if (child is Button) child.MaximumSize = new Size(inner, 0);
                }
            }
            dashboard.ResumeLayout(true);
        }
        finally { resizingCards = false; }
    }
    internal static void SmokeTest()
    {
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "qa"); Directory.CreateDirectory(dir);
        using (var form = new MainForm())
        {
            form.current = new Snapshot { Manufacturer = "Equipo de prueba", Model = "Modelo de prueba", RamTotalGiB = 16, RamAvailableGiB = 9.6, PnpReadComplete = true };
            foreach (var pair in new[] {
                new[] { "Fabricante del equipo", "Fabricante de prueba" }, new[] { "Modelo del equipo", "Computador de prueba con nombre extenso para comprobar ajuste de líneas" },
                new[] { "Serie del equipo (SMBIOS)", "PRUEBA123456" }, new[] { "Nombre del equipo", "MI-PC" }, new[] { "Procesador", "Intel Core i5 · procesador de prueba" },
                new[] { "Núcleos / procesadores lógicos", "4 / 8" }, new[] { "Memoria RAM instalada", "16 GB" }, new[] { "Windows", "Windows 11 Pro" },
                new[] { "Arquitectura de Windows", "64 bits" }, new[] { "Versión de BIOS", "1.20" }, new[] { "Fabricante de BIOS", "Fabricante de prueba" }, new[] { "Fecha de BIOS", "10-03-2025" } }) form.current.Summary.Add(pair);
            form.current.Volumes.Add(new VolumeInfo { Name = "C:", TotalGiB = 500, FreeGiB = 320 });
            form.current.ProblemDevices.Add("audio");
            form.RenderDashboard(); form.Show(); Application.DoEvents();
            foreach (int width in new[] { 1100, 760, 650 })
            {
                form.Width = width; form.ResizeCards(); Application.DoEvents();
                foreach (var card in form.cards)
                {
                    if (card.Width > form.dashboard.ClientSize.Width) throw new InvalidOperationException("Tarjeta fuera de la ventana");
                    foreach (Control child in card.Controls) if (child.Visible && child.Right > card.ClientSize.Width) throw new InvalidOperationException("Contenido fuera de la tarjeta");
                }
                using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(dir, "interfaz-" + width + ".png"), ImageFormat.Png); }
            }
            form.ToggleHelp(); Application.DoEvents();
            if (form.helpLabels.Any(l => !l.Visible)) throw new InvalidOperationException("No se muestran las explicaciones");
            form.tabs.SelectedTab = form.driverPage; Application.DoEvents();
            if (form.tabs.SelectedTab != form.driverPage) throw new InvalidOperationException("Navegación incorrecta");
            form.tabs.SelectedIndex = 0; form.current = new Snapshot(); form.RenderDashboard(); Application.DoEvents();
            form.Scale(new SizeF(2, 2)); form.ResizeCards(); Application.DoEvents();
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(dir, "interfaz-escala-doble.png"), ImageFormat.Png); }
            File.WriteAllText(Path.Combine(dir, "resultado.txt"), "OK: diseño adaptable, explicaciones, navegación y datos ausentes. Datos simulados; prueba real por USB pendiente.");
            form.Close();
        }
    }
}
internal sealed class UsageBar : Control
{
    public double Value;
    public UsageBar() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var background = new SolidBrush(Color.FromArgb(231, 237, 245))) e.Graphics.FillRectangle(background, ClientRectangle);
        using (var fill = new SolidBrush(Color.FromArgb(70, 121, 201))) e.Graphics.FillRectangle(fill, 0, 0, (int)(Width * Value / 100), Height);
    }
}
