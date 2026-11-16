using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using UIModernization;

internal sealed class VideoAdapterPanel : ModernCardPanel
{
    readonly FlowLayoutPanel header=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=Padding.Empty};
    readonly Label name;
    internal string VendorBrand;
    internal VideoAdapterPanel(VideoAdapterInfo adapter)
    {
        Name="gpu-adapter";Tag=adapter;FlowDirection=FlowDirection.TopDown;WrapContents=false;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;BackColor=ModernTheme.Raised;Padding=new Padding(12);Margin=new Padding(0,4,0,6);Width=260;
        VendorBrand=adapter.Brand;
        if(VendorBrand!=null)header.Controls.Add(new BrandLogo(VendorBrand) {Width=76,Height=38,BackColor=ModernTheme.Raised,Margin=new Padding(0,0,8,2),AccessibleName="Fabricante: "+VendorBrand});
        name=new Label {Text=adapter.Name,UseMnemonic=false,AutoSize=true,Font=new Font("Segoe UI",12f,FontStyle.Bold),ForeColor=ModernTheme.Text,Margin=new Padding(0,7,0,4)};header.Controls.Add(name);Controls.Add(header);
        Add("Memoria dedicada (DXGI): "+VideoMemory.MiB(adapter.DedicatedVideo));
        Add("Memoria compartida informada: "+VideoMemory.MiB(adapter.SharedSystem));
        if(adapter.DedicatedSystem.GetValueOrDefault()>0)Add("Memoria de sistema dedicada: "+VideoMemory.MiB(adapter.DedicatedSystem));
        if(adapter.Source!="DXGI")Add("Identificación WMI · reservas DXGI no disponibles");
        var extra=new Label {Text="Fuente: "+adapter.Source+" · Fabricante PCI: "+adapter.Vendor.ToString("X4"),AutoSize=true,Visible=false,ForeColor=ModernTheme.Muted,UseMnemonic=false};Controls.Add(extra);
        var toggle=new Button {Text="Mostrar detalles ⌄",AutoSize=true,FlatStyle=FlatStyle.Flat,ForeColor=ModernTheme.Accent,BackColor=ModernTheme.Surface,Font=new Font("Segoe UI",9),Margin=new Padding(0,4,0,3)};toggle.FlatAppearance.BorderColor=ModernTheme.Border;
        toggle.Click+=(s,e)=>{extra.Visible=!extra.Visible;toggle.Text=extra.Visible?"Ocultar detalles ⌃":"Mostrar detalles ⌄";};header.Controls.Add(toggle);
        Resize+=(s,e)=>Fit();Fit();AccessibleName=adapter.Name+" · "+(VendorBrand??"Fabricante sin identificar");
    }
    void Add(string text){Controls.Add(new Label {Text=text,UseMnemonic=false,AutoSize=true,ForeColor=ModernTheme.Muted,Margin=new Padding(0,2,0,3)});}
    void Fit()
    {
        int width=Math.Max(100,ClientSize.Width-Padding.Horizontal);header.MinimumSize=header.MaximumSize=new Size(width,0);name.MaximumSize=new Size(Math.Max(90,width-(VendorBrand!=null?84:0)),0);
        foreach(var l in Controls.OfType<Label>())l.MaximumSize=new Size(width,0);
    }
}
internal sealed partial class MainForm
{
    void RenderVideoCard(FlowLayoutPanel card)
    {
        var data=current==null?new List<VideoAdapterInfo>():current.VideoAdapters;
        var physical=data.Where(a=>!a.IsSoftware).ToArray();
        if(physical.Length==0)
        {
            var names=(current?.Summary.Where(r=>r[0]=="Adaptadores gráficos").SelectMany(r=>r[1].Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))??Enumerable.Empty<string>()).Where(n=>!string.IsNullOrWhiteSpace(n)&&n!="No disponible").Distinct(StringComparer.OrdinalIgnoreCase);
            physical=names.Select(n=>new VideoAdapterInfo {Name=n,Source="WMI"}).ToArray();
        }
        if(physical.Length==0)Line(card,"No se pudieron identificar GPU físicas.");
        foreach(var adapter in physical)card.Controls.Add(new VideoAdapterPanel(adapter));
        int software=data.Count(a=>a.IsSoftware);if(software>0)Line(card,software+" adaptador(es) de software · ver datos gráficos");
        InlineDetails(card,"Ver datos gráficos","Información gráfica","Memoria gráfica DXGI");
        Help(card,"Cada adaptador muestra su propio fabricante, según su identificador PCI o nombre individual. Las cifras DXGI son capacidades/reservas, no consumo actual. Una reserva dedicada no confirma VRAM en chips separados ni determina por sí sola si la GPU es integrada.");
    }
}
