using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

internal sealed class VideoAdapterInfo
{
    public string Name="",Source="DXGI";
    public uint? Vendor;
    public ulong? DedicatedVideo,DedicatedSystem,SharedSystem;
    public bool IsSoftware;
    internal string Brand {get{return Vendor==0x10DE?"nvidia":Vendor==0x8086?"intel":Vendor==0x1002?"amd":Vendor==0x5143?"qualcomm":Vendor==0x13B5?"arm":BrandLogo.Resolve(Name);}}
}
internal static class VideoMemory
{
    [DllImport("dxgi.dll",CallingConvention=CallingConvention.StdCall)] static extern int CreateDXGIFactory1(ref Guid iid,out IntPtr factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumAdapter1(IntPtr self,uint index,out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDescription1(IntPtr self,out Description1 desc);
    [StructLayout(LayoutKind.Sequential)] struct Luid {public uint Low;public int High;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Description1
    {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Name;
        public uint Vendor,Device,Subsystem,Revision;
        public UIntPtr DedicatedVideo,DedicatedSystem,SharedSystem;
        public Luid Luid;public uint Flags;
    }
    internal static List<VideoAdapterInfo> ReadAdapters(out string issue)
    {
        issue="";var output=new List<VideoAdapterInfo>();IntPtr factory=IntPtr.Zero;
        try
        {
            Guid iid=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref iid,out factory));
            var enumerate=(EnumAdapter1)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory),12*IntPtr.Size),typeof(EnumAdapter1));
            for(uint i=0;i<32;i++)
            {
                IntPtr adapter;int result=enumerate(factory,i,out adapter);if(result<0)break;if(adapter==IntPtr.Zero){issue="DXGI devolvió un adaptador sin descriptor.";break;}
                try
                {
                    var get=(GetDescription1)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(adapter),10*IntPtr.Size),typeof(GetDescription1));Description1 d;Marshal.ThrowExceptionForHR(get(adapter,out d));
                    output.Add(new VideoAdapterInfo {Name=(d.Name??"").Trim(),Vendor=d.Vendor,DedicatedVideo=d.DedicatedVideo.ToUInt64(),DedicatedSystem=d.DedicatedSystem.ToUInt64(),SharedSystem=d.SharedSystem.ToUInt64(),IsSoftware=(d.Flags&2)!=0});
                }
                catch(Exception ex){issue="Lectura gráfica parcial: "+ex.Message;}
                finally{Marshal.Release(adapter);}
            }
        }
        catch(Exception ex){issue="Memoria gráfica DXGI no disponible: "+ex.Message;}
        finally{if(factory!=IntPtr.Zero)Marshal.Release(factory);}
        return output;
    }
    internal static uint? VendorFromPnp(string id){var m=System.Text.RegularExpressions.Regex.Match(id??"",@"VEN_([0-9A-F]{4})",System.Text.RegularExpressions.RegexOptions.IgnoreCase);return m.Success?(uint?)Convert.ToUInt32(m.Groups[1].Value,16):null;}
    internal static string MiB(ulong? bytes){return bytes.HasValue?(bytes.Value/1048576d).ToString("0.##")+" MiB":"No disponible";}
    internal static string Format(IEnumerable<VideoAdapterInfo> adapters,string issue="")
    {
        var output=adapters.Select(d=>d.Name+(d.IsSoftware?" (adaptador de software)":"")+Environment.NewLine+"Memoria de video dedicada informada por DXGI: "+MiB(d.DedicatedVideo)+Environment.NewLine+"Memoria de sistema dedicada: "+MiB(d.DedicatedSystem)+Environment.NewLine+"Memoria compartida informada: "+MiB(d.SharedSystem)).ToList();
        if(issue.Length>0)output.Add(issue);return output.Count>0?string.Join(Environment.NewLine+Environment.NewLine,output):"Sin adaptadores DXGI disponibles.";
    }
    internal static string Read(){string issue;return Format(ReadAdapters(out issue),issue);}
}
