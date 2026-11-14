using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal static class VideoMemory
{
    [DllImport("dxgi.dll", CallingConvention=CallingConvention.StdCall)] static extern int CreateDXGIFactory(ref Guid iid, out IntPtr factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumAdapter(IntPtr self,uint index,out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDescription(IntPtr self,out Description desc);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Description
    {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Name;
        public uint Vendor,Device,Subsystem,Revision;
        public UIntPtr DedicatedVideo,DedicatedSystem,SharedSystem;
        public long Luid;
    }
    internal static string Read()
    {
        IntPtr factory=IntPtr.Zero;
        var output=new List<string>();
        try
        {
            Guid iid=new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
            Marshal.ThrowExceptionForHR(CreateDXGIFactory(ref iid,out factory));
            IntPtr table=Marshal.ReadIntPtr(factory);
            var enumerate=(EnumAdapter)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(table,7*IntPtr.Size),typeof(EnumAdapter));
            for(uint i=0;i<32;i++)
            {
                IntPtr adapter;int result=enumerate(factory,i,out adapter);if(result<0)break;
                try
                {
                    var get=(GetDescription)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(adapter),8*IntPtr.Size),typeof(GetDescription));
                    Description d;Marshal.ThrowExceptionForHR(get(adapter,out d));
                    output.Add(d.Name+Environment.NewLine+"Memoria de video dedicada informada por DXGI: "+(d.DedicatedVideo.ToUInt64()/1048576d).ToString("0.##")+" MiB"+Environment.NewLine+"Memoria de sistema dedicada: "+(d.DedicatedSystem.ToUInt64()/1048576d).ToString("0.##")+" MiB"+Environment.NewLine+"Memoria compartida disponible: "+(d.SharedSystem.ToUInt64()/1048576d).ToString("0.##")+" MiB");
                }
                finally{if(adapter!=IntPtr.Zero)Marshal.Release(adapter);}
            }
        }
        catch {return "Memoria gráfica DXGI no disponible.";}
        finally {if(factory!=IntPtr.Zero)Marshal.Release(factory);}
        return output.Count==0?"Sin adaptadores DXGI disponibles.":string.Join(Environment.NewLine+Environment.NewLine,output);
    }
}
