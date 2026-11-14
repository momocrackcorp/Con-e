using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Windows.Forms;

internal sealed partial class MainForm
{
    static bool IsAdmin() { using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }
    internal static bool CanResize(DiskLayout disk,PartitionLayout p)
    {return disk.Complete && !string.IsNullOrWhiteSpace(disk.UniqueId) && disk.UniqueId!="No disponible" && !p.Gap && p.Letter.Length==1 && p.FileSystem.Equals("NTFS",StringComparison.OrdinalIgnoreCase) && p.Type=="Datos básicos";}
    static ManagementObject CurrentPartition(DiskLayout disk,PartitionLayout part)
    {
        using(var search=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT UniqueId,Size FROM MSFT_Disk WHERE Number="+disk.Number))
        using(var rows=search.Get())
        {
            if(rows.Count!=1)throw new InvalidOperationException("El disco ya no está disponible.");
            foreach(ManagementObject row in rows)using(row)if(Value(row,"UniqueId")!=disk.UniqueId || Convert.ToUInt64(row["Size"])!=disk.Size)throw new InvalidOperationException("El disco cambió. Actualiza el mapa.");
        }
        var path=@"\\.\root\Microsoft\Windows\Storage:MSFT_Partition.DiskNumber="+disk.Number+",PartitionNumber="+part.Number;
        // Use the provider's complete object path; the class has additional inherited keys.
        using(var search=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Partition WHERE DiskNumber="+disk.Number+" AND PartitionNumber="+part.Number))
        using(var rows=search.Get())
        {
            foreach(ManagementObject row in rows)using(row)
            {
                if(Convert.ToUInt64(row["Offset"])!=part.Offset||Convert.ToUInt64(row["Size"])!=part.Size)throw new InvalidOperationException("La partición cambió. Actualiza el mapa.");
                string guid=Value(row,"GptType").Trim('{','}');
                if(!guid.Equals("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Sólo se admite partición GPT de datos básicos.");
                return new ManagementObject(row.Path);
            }
        }
        throw new InvalidOperationException("Partición no encontrada.");
    }
    internal static ulong[] ResizeBounds(DiskLayout disk,PartitionLayout part)
    {
        if(!CanResize(disk,part))throw new InvalidOperationException("Selecciona una partición GPT NTFS de datos básicos con letra, leída mediante Storage WMI.");
        using(var obj=CurrentPartition(disk,part))
        using(var result=obj.InvokeMethod("GetSupportedSize",(ManagementBaseObject)null,null))
        {
            if(Convert.ToUInt32(result["ReturnValue"])!=0)throw new InvalidOperationException("Windows no pudo determinar los tamaños permitidos. Código "+result["ReturnValue"]);
            return new[]{Convert.ToUInt64(result["SizeMin"]),Convert.ToUInt64(result["SizeMax"])};
        }
    }
    internal static void ApplyResize(DiskLayout disk,PartitionLayout part,ulong bytes)
    {
        var bounds=ResizeBounds(disk,part);
        if(bytes<bounds[0]||bytes>bounds[1]||bytes==part.Size)throw new InvalidOperationException("El tamaño está fuera de los límites permitidos o no cambia la partición.");
        using(var obj=CurrentPartition(disk,part))
        using(var input=obj.GetMethodParameters("Resize"))
        {
            input["Size"]=bytes;
            using(var result=obj.InvokeMethod("Resize",input,null))
                if(Convert.ToUInt32(result["ReturnValue"])!=0)throw new InvalidOperationException("Windows rechazó el cambio. Código "+result["ReturnValue"]+". Actualiza el mapa para comprobar el estado.");
        }
    }
    internal static void ResizeIntegrationTest()
    {
        string id=Environment.GetEnvironmentVariable("CONE_TEST_DISK_ID");uint number;
        if(string.IsNullOrWhiteSpace(id)||!uint.TryParse(Environment.GetEnvironmentVariable("CONE_TEST_DISK_NUMBER"),out number))throw new InvalidOperationException("Falta identidad del disco virtual de prueba");
        Func<DiskLayout> reload=()=>ReadDiskLayouts(new Snapshot()).Single(d=>d.Number==number&&d.UniqueId==id);
        var disk=reload();var part=disk.Partitions.Single(p=>CanResize(disk,p));
        string file=part.Letter+@":\cone-resize-test.txt";
        System.IO.File.WriteAllText(file,"Con-E: conservar datos durante reducción y ampliación");
        string original=System.IO.File.ReadAllText(file);ulong oldSize=part.Size;
        ApplyResize(disk,part,128UL*1048576);
        if(System.IO.File.ReadAllText(file)!=original)throw new InvalidOperationException("Contenido cambió tras reducción");
        disk=reload();part=disk.Partitions.Single(p=>CanResize(disk,p));
        if(part.Size!=128UL*1048576)throw new InvalidOperationException("Tamaño reducido incorrecto");
        ApplyResize(disk,part,oldSize);
        if(System.IO.File.ReadAllText(file)!=original)throw new InvalidOperationException("Contenido cambió tras ampliación");
        disk=reload();if(!disk.Partitions.Any(p=>p.Size==oldSize))throw new InvalidOperationException("Tamaño ampliado incorrecto");
        System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qa","resize-ok.txt"),"OK: disco virtual GPT NTFS, reducción a 128 MiB, ampliación al tamaño inicial y conservación del archivo.");
    }
    async void EditPartition(DiskLayout disk,PartitionLayout part)
    {
        if(!CanResize(disk,part)){partitionInfo.Text="Modificar tamaño sólo está habilitado para particiones GPT de datos básicos, NTFS y con letra. No se modifica EFI, MSR ni recuperación.";return;}
        if(!IsAdmin())
        {
            if(MessageBox.Show(this,"Para modificar particiones, Con-E debe ejecutarse como administrador. ¿Reabrir con permisos? Después deberás seleccionar de nuevo la partición.","Permisos",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            try{Process.Start(new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=true,Verb="runas"});Close();}catch(Exception ex){partitionInfo.Text="No se inició con permisos: "+ex.Message;}return;
        }
        try
        {
            partitionInfo.Text="Consultando los límites de Windows…";
            var bounds=await System.Threading.Tasks.Task.Run(()=>ResizeBounds(disk,part));if(IsDisposed)return;
            using(var dialog=new Form {Text="Modificar tamaño · "+part.Letter+":",Size=new Size(620,460),MinimumSize=new Size(500,400),StartPosition=FormStartPosition.CenterParent,Font=Font,AutoScaleMode=AutoScaleMode.Dpi})
            {
                var content=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(18)};
                var info=new Label {AutoSize=true,MaximumSize=new Size(530,0),Text="Disco "+disk.Number+" · "+disk.Name+Environment.NewLine+"Partición "+part.Number+" · "+part.Letter+": · NTFS"+Environment.NewLine+"Actual: "+Space(part.Size)+". Permitido: "+Space(bounds[0])+" a "+Space(bounds[1])+Environment.NewLine+"El tamaño final incluye el espacio ocupado. Respaldar los datos antes de continuar. No se mueven otras particiones."};content.Controls.Add(info);
                content.Controls.Add(new Label {Text="Tamaño final en MiB",AutoSize=true});
                decimal min=decimal.Ceiling(bounds[0]/1048576m),max=decimal.Floor(bounds[1]/1048576m);
                if(min>max)throw new InvalidOperationException("No hay un tamaño válido en MiB.");
                var size=new NumericUpDown {Minimum=min,Maximum=max,Value=Math.Max(min,Math.Min(max,decimal.Floor(part.Size/1048576m))),Width=220,ThousandsSeparator=true};content.Controls.Add(size);
                content.Controls.Add(new Label {Text="Escribe APLICAR para confirmar el tamaño final indicado.",AutoSize=true,MaximumSize=new Size(520,0)});
                var confirmation=new TextBox {Width=250};content.Controls.Add(confirmation);
                var apply=new Button {Text="Aplicar cambio de tamaño",AutoSize=true,Enabled=false};content.Controls.Add(apply);
                var outcome=new Label {AutoSize=true,MaximumSize=new Size(520,0)};content.Controls.Add(outcome);
                confirmation.TextChanged+=(s,e)=>apply.Enabled=confirmation.Text=="APLICAR";
                apply.Click+=async(s,e)=>
                {
                    ulong target=checked((ulong)size.Value*1048576UL);
                    if(MessageBox.Show(dialog,"Se modificará "+part.Letter+": en el disco "+disk.Number+" de "+Space(part.Size)+" a "+Space(target)+". ¿Aplicar ahora?","Confirmar cambio",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
                    apply.Enabled=false;confirmation.Enabled=false;size.Enabled=false;dialog.ControlBox=false;outcome.Text="Aplicando cambio… No desconectes el disco.";
                    try{await System.Threading.Tasks.Task.Run(()=>ApplyResize(disk,part,target));dialog.DialogResult=DialogResult.OK;dialog.Close();}
                    catch(Exception ex){outcome.Text=ex.Message;dialog.ControlBox=true;}
                };
                dialog.Controls.Add(content);dialog.ShowDialog(this);
            }
            await RefreshData();
        }
        catch(Exception ex){partitionInfo.Text="No se pudo preparar la modificación: "+ex.Message;}
    }
}
