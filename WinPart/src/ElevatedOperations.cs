// SPDX-License-Identifier: GPL-2.0-or-later
// WinPart contributors, 2026. Distributed without warranty; see COPYING.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

internal sealed partial class MainForm
{
    internal static string EncodeOperations(IEnumerable<PendingResize> changes)
    {
        var plan=new XElement("WinPartPlan",new XAttribute("version","1"));
        foreach(var c in changes)
        {
            if(c.IsVirtual){plan.Add(new XElement("operation",new XAttribute("virtual",c.VirtualAction),new XAttribute("path",c.VirtualPath),new XAttribute("bytes",c.Bytes)));continue;}
            plan.Add(new XElement("operation",
            new XAttribute("disk",c.Disk.Number),new XAttribute("diskSize",c.Disk.Size),new XAttribute("diskId",c.Disk.UniqueId),new XAttribute("diskName",c.Disk.Name??""),
            new XAttribute("partition",c.Part.Number),new XAttribute("offset",c.Part.Offset),new XAttribute("size",c.Part.Size),new XAttribute("letter",c.Part.Letter),
            new XAttribute("type",c.Part.Type),new XAttribute("oldFs",c.Part.FileSystem),new XAttribute("targetSize",c.Bytes),new XAttribute("newFs",c.FileSystem??""),new XAttribute("volumeId",c.VolumeId??"")));
        }
        string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(plan.ToString(SaveOptions.DisableFormatting)));
        if(encoded.Length>24000)throw new InvalidOperationException("Demasiadas operaciones para una sola aplicación; prepara una cola más pequeña.");return encoded;
    }
    internal static List<PendingResize> DecodeOperations(string encoded)
    {
        if(encoded.Length>24000)throw new InvalidOperationException("Plan demasiado grande.");
        XElement plan;
        using(var text=new System.IO.StringReader(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))))
        using(var reader=System.Xml.XmlReader.Create(text,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=50000}))plan=XElement.Load(reader,LoadOptions.None);
        if(plan.Name!="WinPartPlan"||(string)plan.Attribute("version")!="1")throw new InvalidOperationException("Plan no compatible.");
        var result=new List<PendingResize>();
        foreach(var row in plan.Elements("operation"))
        {
            string action=(string)row.Attribute("virtual");
            if(action!=null){var v=new PendingResize {VirtualAction=action,VirtualPath=(string)row.Attribute("path"),Bytes=(ulong)row.Attribute("bytes")};ValidateVirtualSyntax(v);result.Add(v);continue;}
            string fs=(string)row.Attribute("newFs");if(fs!=""&&!new[]{"NTFS","FAT32","exFAT"}.Contains(fs,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Operación no admitida.");
            result.Add(new PendingResize {Disk=new DiskLayout {Number=(uint)row.Attribute("disk"),Size=(ulong)row.Attribute("diskSize"),UniqueId=(string)row.Attribute("diskId"),Name=(string)row.Attribute("diskName"),Complete=true},
                Part=new PartitionLayout {Number=(uint)row.Attribute("partition"),Offset=(ulong)row.Attribute("offset"),Size=(ulong)row.Attribute("size"),Letter=(string)row.Attribute("letter"),Type=(string)row.Attribute("type"),FileSystem=(string)row.Attribute("oldFs")},
                Bytes=(ulong)row.Attribute("targetSize"),FileSystem=fs==""?null:fs,VolumeId=(string)row.Attribute("volumeId")});
        }
        if(result.Count==0||result.GroupBy(c=>c.IsVirtual?"v:"+c.VirtualPath.ToUpperInvariant():"p:"+c.Disk.UniqueId+":"+c.Part.Number).Any(g=>g.Count()>1))throw new InvalidOperationException("Plan vacío o con destinos duplicados.");return result;
    }
    internal static void ExecuteOperations(IEnumerable<PendingResize> changes)
    {
        if(!IsAdmin())throw new InvalidOperationException("La aplicación de cambios requiere permisos de administrador.");
        var queue=changes.ToList();
        foreach(var c in queue)
        {
            if(c.IsVirtual){ValidateVirtual(c);continue;}
            if(c.FileSystem!=null)ValidateFormat(c);
            else {var bounds=ResizeBounds(c.Disk,c.Part);if(c.Bytes<bounds[0]||c.Bytes>bounds[1]||c.Bytes==c.Part.Size)throw new InvalidOperationException("Tamaño fuera del intervalo permitido o sin cambios.");}
        }
        if(queue.Any(v=>v.VirtualAction=="detach")&&queue.Any(v=>!v.IsVirtual))throw new InvalidOperationException("Aplica la desconexión de VHD por separado de los cambios de particiones.");
        foreach(var c in queue){if(c.IsVirtual)ApplyVirtual(c);else if(c.FileSystem!=null)ApplyFormat(c);else ApplyResize(c.Disk,c.Part,c.Bytes);}
    }
    internal static int RunElevatedHelper(string encoded)
    {
        try{ExecuteOperations(DecodeOperations(encoded));return 0;}
        catch(Exception ex){MessageBox.Show("No se completó la cola: "+ex.Message+Environment.NewLine+"Las operaciones ya terminadas no se deshacen automáticamente. WinPart actualizará el mapa.","WinPart · Resultado de cambios",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
    }
    static int LaunchElevated(string encoded)
    {
        var start=new ProcessStartInfo(Application.ExecutablePath,"--apply-plan "+encoded){UseShellExecute=true,Verb="runas"};
        using(var helper=Process.Start(start))
        {if(helper==null)throw new InvalidOperationException("No se inició el proceso de cambios.");helper.WaitForExit();return helper.ExitCode;}
    }
}
