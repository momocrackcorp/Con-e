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
        header.Controls.Add(new Label { Text = "Tu computador, más claro.  ·  v0.6.0", AutoSize = true, ForeColor = MutedColor, Margin = new Padding(0, 12, 0, 0) });
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
        panel.Controls.Add(ActionButton("Consultar aplicaciones actualizables (WinGet)", () => OpenWinget(false)));
        panel.Controls.Add(ActionButton("Actualizar aplicaciones con WinGet", () => OpenWinget(true)));
        panel.Controls.Add(ActionButton("Controladores: abrir Windows Update", () => Open("ms-settings:windowsupdate-optionalupdates")));
        panel.Controls.Add(new Label { Text = "WinGet actualiza aplicaciones. Los controladores se revisan mediante Windows Update o el fabricante.", AutoSize = true, MaximumSize = new Size(500, 0) });
        return panel;
    }
    void OpenWinget(bool upgrade)
    {
        string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        if (!File.Exists(alias)) { status.Text = "WinGet no está disponible en este usuario. No se instalará automáticamente."; return; }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/k winget upgrade" + (upgrade ? " --all" : "")) { UseShellExecute = true });
    }
    void AddBrand(FlowLayoutPanel card, string manufacturer)
    {
        string brand = BrandLogo.Resolve(manufacturer);
        if(brand == null || card.Controls.OfType<BrandLogo>().Any(c => c.Brand == brand)) return;
        var logo = new BrandLogo(brand); card.Controls.Add(logo); card.Controls.SetChildIndex(logo, Math.Min(2,card.Controls.Count-1));
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
        var heading = new Label { Text = title, AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = BlueColor, Margin = new Padding(0, 0, 0, 14) };
        card.Controls.Add(new ComponentPicture { Kind = title == "Almacenamiento" ? title + " " + Read("Tipos de almacenamiento") : title, Width = 108, Height = 78, AccessibleName = title, Margin = new Padding(0, 0, 0, 10) });
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
        var meter = new UsageBar { Value = Math.Max(0, Math.Min(100, percent)), Height = 14, Width = 220, AccessibleName = accessibleName + ": " + percent.ToString("0") + " %", Margin = new Padding(0, 4, 0, 12) };
        card.Controls.Add(meter);
    }
    readonly HashSet<string> expandedCards = new HashSet<string>();
    void InlineDetails(FlowLayoutPanel card, string title, params string[] keys)
    {
        var labels = keys.Select(key => Line(card, key + ": " + Read(key))).ToArray();
        foreach (var label in labels) label.Visible = expandedCards.Contains(title);
        Button toggle = null;
        toggle = ActionButton(expandedCards.Contains(title) ? "Ocultar detalles" : title, () =>
        {
            bool expanded = !expandedCards.Contains(title);
            if (expanded) expandedCards.Add(title); else expandedCards.Remove(title);
            foreach (var label in labels) label.Visible = expanded;
            toggle.Text = expanded ? "Ocultar detalles" : title;
            ResizeCards();
        });
        toggle.Tag = "expand:" + title;
        card.Controls.Add(toggle); card.Controls.SetChildIndex(toggle, card.Controls.Count - labels.Length - 1);
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
            AddBrand(identity, current == null ? "" : current.Manufacturer);
            Help(identity, "El modelo y la serie son los datos registrados por el fabricante en el equipo.");

            var cpu = Card("⚙", "Procesador");
            AddBrand(cpu, Read("Marca del procesador"));
            Line(cpu, Read("Marca del procesador"), true);
            Line(cpu, Read("Procesador"), true); Line(cpu, "Núcleos / hilos: " + Read("Núcleos / procesadores lógicos"));
            cpu.Controls.Add(ActionButton("Explorar componentes", () => tabs.SelectedTab = driverPage));
            Help(cpu, "Ejecuta las instrucciones de los programas. Los núcleos permiten repartir tareas.");

            var ram = Card("▤", "Memoria RAM");
            Line(ram, Read("Memoria RAM instalada"), true);
            if (current != null && current.RamTotalGiB.HasValue && current.RamAvailableGiB.HasValue && current.RamTotalGiB.Value > 0 && current.RamAvailableGiB.Value <= current.RamTotalGiB.Value)
            {
                double used = current.RamTotalGiB.Value - current.RamAvailableGiB.Value;
                double percent = used / current.RamTotalGiB.Value * 100;
                ram.Controls.Add(new UsageDial { Value = percent, Width = 140, Height = 100, AccessibleName = "RAM utilizada: " + percent.ToString("0") + " %" });
                Line(ram, used.ToString("0.0") + " GiB en uso · " + percent.ToString("0") + " %");
                Line(ram, "Disponible: " + current.RamAvailableGiB.Value.ToString("0.0") + " GiB");
            }
            else Line(ram, "Uso de RAM: no disponible");
            if(current != null) foreach(var module in current.Hardware.Where(h => h.Category == "Memoria RAM"))
            {
                string maker; if(module.Properties.TryGetValue("Fabricante", out maker)) AddBrand(ram, maker);
            }
            Line(ram, Read("Ranuras RAM"));
            InlineDetails(ram, "Ver módulos por ranura", "Módulos RAM");
            Help(ram, "Es la memoria de trabajo de los programas abiertos. Pulsa Actualizar para obtener otra lectura; no es un monitor continuo.");

            var video = Card("", "Memoria de video");
            AddBrand(video, Read("Adaptadores gráficos"));
            Line(video, Read("Adaptadores gráficos"), true);
            Line(video, "Integrada / dedicada: pendiente de verificación DXGI");
            Line(video, Read("Memoria gráfica DXGI"));
            InlineDetails(video, "Ver datos gráficos", "Información gráfica");
            Help(video, "DXGI distingue memoria de video y memoria compartida. En una GPU integrada, una reserva dedicada no demuestra que exista VRAM en chips separados.");

            var disk = Card("▰", "Almacenamiento");
            if (current == null || current.Volumes.Count == 0) Line(disk, current == null ? "Pendiente de lectura" : "No se pudieron leer las unidades.");
            else foreach (var volume in current.Volumes)
            {
                Line(disk, "Unidad " + volume.Name, true);
                Meter(disk, 100 * (volume.TotalGiB - volume.FreeGiB) / volume.TotalGiB, "Espacio ocupado " + volume.Name);
                Line(disk, volume.FreeGiB.ToString("0.0") + " GiB libres de " + volume.TotalGiB.ToString("0.0") + " GiB");
            }
            Line(disk, Read("Tipos de almacenamiento"));
            disk.Controls.Add(ActionButton("Abrir mapa de particiones", () => tabs.SelectedTab = disksPage));
            InlineDetails(disk, "Ver discos físicos", "Disco físico", "Particiones");
            Help(disk, "La barra indica espacio ocupado. No evalúa la salud del disco. Cada unidad puede ser una partición de un mismo disco físico.");

            var network = Card("", "Tarjeta de red");
            Line(network, Read("Adaptadores de red"), true);
            InlineDetails(network, "Ver características de red", "Características de red");

            var windows = Card("⊞", "Windows");
            Line(windows, Read("Windows"), true); Line(windows, Read("Arquitectura de Windows"));
            InlineDetails(windows, "Ver detalles de Windows", "Versión / compilación", "Tipo de sistema");
            windows.Controls.Add(ActionButton("Mostrar clave OEM disponible", () => RevealOem(windows)));
            Help(windows, "La clave OEM de firmware puede no existir o no corresponder a la licencia activa de Windows.");

            var bios = Card("▦", "BIOS y placa base");
            Line(bios, Read("Versión de BIOS"), true); Line(bios, Read("Fabricante de BIOS")); Line(bios, "Fecha: " + Read("Fecha de BIOS"));
            InlineDetails(bios, "Ver modelo y números de serie", "Fabricante de placa base", "Modelo de placa base", "Serie de placa base", "Serie informada por BIOS");
            Help(bios, "La BIOS inicia el hardware antes de cargar Windows. Las series del equipo y de la placa pueden ser diferentes.");

            var account = Card("", "Usuario");
            Line(account, Environment.UserDomainName + "\\" + Environment.UserName, true);
            Line(account, "Contraseña: Windows no la proporciona en texto legible.");

            // A–H: equipo queda al final del resumen, detrás de las ocho categorías.
            dashboard.Controls.SetChildIndex(cpu, 0); dashboard.Controls.SetChildIndex(ram, 1);
            dashboard.Controls.SetChildIndex(video, 2); dashboard.Controls.SetChildIndex(disk, 3);
            dashboard.Controls.SetChildIndex(network, 4); dashboard.Controls.SetChildIndex(bios, 5);
            dashboard.Controls.SetChildIndex(windows, 6); dashboard.Controls.SetChildIndex(account, 7);

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
                const string key = "Incidencias de lectura";
                current.Summary.RemoveAll(row => row[0] == key); current.Summary.Add(new[] { key, string.Join(Environment.NewLine, current.Warnings) });
                InlineDetails(warning, "Ver incidencias de lectura", key);
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
                    else if (child is UsageDial) child.Width = Math.Min(160, inner);
                    else if (child is BrandLogo) child.Width = Math.Min(125, inner);
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
            form.current = new Snapshot { Manufacturer = "HP", Model = "Modelo de prueba", RamTotalGiB = 16, RamAvailableGiB = 9.6, PnpReadComplete = true };
            foreach (var pair in new[] {
                new[] { "Fabricante del equipo", "HP" }, new[] { "Marca del procesador", "INTEL" }, new[] { "Modelo del equipo", "Computador de prueba con nombre extenso para comprobar ajuste de líneas" },
                new[] { "Serie del equipo (SMBIOS)", "PRUEBA123456" }, new[] { "Nombre del equipo", "MI-PC" }, new[] { "Procesador", "Intel Core i5 · procesador de prueba" },
                new[] { "Núcleos / procesadores lógicos", "4 / 8" }, new[] { "Memoria RAM instalada", "16 GB" }, new[] { "Windows", "Windows 11 Pro" },
                new[] { "Arquitectura de Windows", "64 bits" }, new[] { "Versión de BIOS", "1.20" }, new[] { "Fabricante de BIOS", "Fabricante de prueba" }, new[] { "Fecha de BIOS", "10-03-2025" } }) form.current.Summary.Add(pair);
            form.current.Volumes.Add(new VolumeInfo { Name = "C:", TotalGiB = 500, FreeGiB = 320 });
            form.current.ProblemDevices.Add("audio");
            var unknown = new HardwareItem { Name = "Dispositivo PCI sin identificar (prueba)", Category = "Otros dispositivos", DeviceId = @"PCI\VEN_1234&DEV_5678\INSTANCIA", SearchId = @"PCI\VEN_1234&DEV_5678&SUBSYS_11112222&REV_01", ErrorCode = 28 };
            ParseIdentifiers(unknown);
            if (unknown.Properties["Fabricante PCI (VEN)"] != "1234" || unknown.Properties["Dispositivo PCI (DEV)"] != "5678" || unknown.Properties["Subsistema PCI"] != "11112222") throw new InvalidOperationException("Lectura de IDs PCI incorrecta");
            var usb = new HardwareItem { SearchId = @"USB\VID_1234&PID_ABCD&REV_0100" }; ParseIdentifiers(usb);
            if (usb.Properties["Fabricante USB (VID)"] != "1234" || usb.Properties["Producto USB (PID)"] != "ABCD") throw new InvalidOperationException("Lectura de IDs USB incorrecta");
            unknown.Properties["Estado PnP"] = PnpMeaning(28);
            unknown.Properties["IDs de hardware"] = unknown.SearchId;
            unknown.Properties["Controlador"] = "No instalado (ejemplo)";
            form.current.Hardware.Add(unknown);
            form.RenderDashboard(); form.RenderHardwareBrowser(); form.Show(); Application.DoEvents();
            if(BrandLogo.Resolve("HP Inc.") != "hp" || BrandLogo.Resolve("Advanced Micro Devices, Inc.") != "amd" || BrandLogo.Resolve("Fabricante desconocido") != null) throw new InvalidOperationException("Detección de marca incorrecta");
            foreach(string brand in new[] { "intel", "amd", "arm", "hp", "dell", "lenovo", "asus", "acer", "nvidia", "samsung", "qualcomm" }) if(BrandLogo.Get(brand).Width == 0) throw new InvalidOperationException("Logo inválido");
            var biosCard = form.cards.First(c => c.Controls.OfType<Button>().Any(b => Convert.ToString(b.Tag) == "expand:Ver modelo y números de serie"));
            var expand = biosCard.Controls.OfType<Button>().Single(b => Convert.ToString(b.Tag) == "expand:Ver modelo y números de serie");
            int collapsedHeight = biosCard.Height;
            expand.PerformClick(); Application.DoEvents();
            if (biosCard.Height <= collapsedHeight || expand.Text != "Ocultar detalles") throw new InvalidOperationException("La tarjeta no se expande");
            form.dashboard.ScrollControlIntoView(biosCard); Application.DoEvents();
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(dir, "detalles-expandidos.png"), ImageFormat.Png); }
            expand.PerformClick(); Application.DoEvents();
            if (biosCard.Height != collapsedHeight) throw new InvalidOperationException("La tarjeta no se contrae");
            form.dashboard.AutoScrollPosition = Point.Empty;
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
            var testDisk=new DiskLayout {Number=0,Name="SSD de prueba",Size=500UL*1073741824,Style="GPT",Complete=true};
            var part=new PartitionLayout {Number=1,Offset=1048576,Size=200UL*1073741824,Letter="C",Type="Datos básicos",FileSystem="NTFS",VolumeSize=200UL*1073741824,Free=50UL*1073741824};
            testDisk.Partitions.Add(part);
            if(testDisk.Segments().Count!=3)throw new InvalidOperationException("Intervalos no calculados");
            testDisk.Complete=false;if(testDisk.Segments().Count!=1)throw new InvalidOperationException("Se inventaron intervalos con lectura incompleta");testDisk.Complete=true;
            form.diskLayouts.Add(testDisk);form.RenderDiskMaps();form.tabs.SelectedTab=form.disksPage;Application.DoEvents();form.SelectPartition(testDisk,part);
            if(!form.partitionInfo.Text.Contains("50"))throw new InvalidOperationException("Selección de partición incorrecta");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"mapa-discos.png"),ImageFormat.Png);}
            form.tabs.SelectedIndex=0;
            form.ToggleHelp(); Application.DoEvents();
            if (form.helpLabels.Any(l => !l.Visible)) throw new InvalidOperationException("No se muestran las explicaciones");
            form.tabs.SelectedTab = form.driverPage; Application.DoEvents();
            if (form.tabs.SelectedTab != form.driverPage) throw new InvalidOperationException("Navegación incorrecta");
            form.ShowHardwareCategory("Por revisar"); Application.DoEvents();
            if (form.hardwareList.Items.Count != 1) throw new InvalidOperationException("Dispositivo desconocido ausente");
            form.hardwareList.Items[0].Selected = true; form.hardwareList.Focus(); Application.DoEvents();
            if (form.selectedHardware != unknown || form.hardwareDetails.Items.Count == 0) throw new InvalidOperationException("Ficha de hardware ausente");
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(dir, "hardware-desconocido.png"), ImageFormat.Png); }
            form.tabs.SelectedIndex = 0; form.current = new Snapshot(); form.RenderDashboard(); Application.DoEvents();
            form.Scale(new SizeF(2, 2)); form.ResizeCards(); Application.DoEvents();
            using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(dir, "interfaz-escala-doble.png"), ImageFormat.Png); }
            File.WriteAllText(Path.Combine(dir, "resultado.txt"), "OK: tarjetas expandibles y plegables, IDs PCI/USB, hardware desconocido, diseño adaptable, explicaciones, navegación y datos ausentes. Datos simulados; prueba real por USB pendiente.");
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
