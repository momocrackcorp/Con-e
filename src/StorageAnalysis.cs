using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

internal sealed class StorageGroup {public string Name;public long Bytes;}
internal sealed class StorageScan
{
    public readonly List<StorageGroup> Groups=new List<StorageGroup>();
    public long Files,Directories,Skipped,LinksSkipped;public TimeSpan Elapsed;public long Total,Free;
}
internal sealed class StorageScanProgress {public long Files,Skipped;public string Group="";}
internal static class StorageScanner
{
    [StructLayout(LayoutKind.Sequential)] struct StandardInfo {public long AllocationSize,EndOfFile;public uint Links;public byte DeletePending,Directory;}
    [StructLayout(LayoutKind.Sequential)] struct FileInfo
    {public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Access,Written;public uint Serial,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,int kind,out StandardInfo info,uint size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInfo info);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetCompressedFileSize(string name,out uint high);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern bool CreateHardLink(string link,string existing,IntPtr reserved);
    static string LongPath(string path){return path.StartsWith(@"\\?\",StringComparison.Ordinal)?path:@"\\?\"+Path.GetFullPath(path);}
    static bool Recoverable(Exception ex){return ex is UnauthorizedAccessException||ex is IOException||ex is System.Security.SecurityException;}
    static long Allocated(string path,FileAttributes attributes,HashSet<string> links,out bool duplicate)
    {
        duplicate=false;
        using(var file=CreateFile(LongPath(path),0x80,7,IntPtr.Zero,3,0x00300000,IntPtr.Zero)) // attributes only; never read contents or recall placeholders
        {
            if(file.IsInvalid)throw new IOException("No se pudieron consultar atributos del archivo.");
            FileInfo id;if(!GetFileInformationByHandle(file,out id))throw new IOException("No se pudo identificar el archivo.");
            if(id.Links>1&&!links.Add(id.Serial+":"+id.IndexHigh+":"+id.IndexLow)){duplicate=true;return 0;}
            StandardInfo info;if(!GetFileInformationByHandleEx(file,1,out info,(uint)Marshal.SizeOf(typeof(StandardInfo))))throw new IOException("No se pudo consultar espacio asignado.");
            if((attributes&(FileAttributes.Compressed|FileAttributes.SparseFile))!=0)
            {uint high,low=GetCompressedFileSize(LongPath(path),out high);if(low==uint.MaxValue&&Marshal.GetLastWin32Error()!=0)throw new IOException("No se pudo consultar tamaño comprimido.");return checked((long)(((ulong)high<<32)|low));}
            return Math.Max(0,info.AllocationSize);
        }
    }
    internal static StorageScan Scan(string path,CancellationToken token,IProgress<StorageScanProgress> progress=null)
    {
        token.ThrowIfCancellationRequested();var root=new DirectoryInfo(Path.GetFullPath(path));if(!root.Exists)throw new DirectoryNotFoundException("La unidad ya no está disponible.");
        var result=new StorageScan();var groups=new Dictionary<string,StorageGroup>(StringComparer.OrdinalIgnoreCase);var links=new HashSet<string>();var watch=Stopwatch.StartNew();long reported=0;
        var pending=new Stack<Tuple<DirectoryInfo,string>>();pending.Push(Tuple.Create(root,""));
        while(pending.Count>0)
        {
            token.ThrowIfCancellationRequested();var next=pending.Pop();result.Directories++;
            try
            {
                foreach(var entry in next.Item1.EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();string group=next.Item2.Length==0?(entry is DirectoryInfo?entry.Name:"Archivos en raíz"):next.Item2;
                    try
                    {
                        FileAttributes attributes=entry.Attributes;
                        if((attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0){result.Skipped++;continue;}
                        var directory=entry as DirectoryInfo;if(directory!=null){pending.Push(Tuple.Create(directory,group));continue;}
                        bool duplicate;long bytes=Allocated(entry.FullName,attributes,links,out duplicate);if(duplicate){result.LinksSkipped++;continue;}
                        StorageGroup bucket;if(!groups.TryGetValue(group,out bucket)){bucket=new StorageGroup {Name=group};groups.Add(group,bucket);}bucket.Bytes=checked(bucket.Bytes+bytes);result.Files++;
                    }
                    catch(Exception ex) when(Recoverable(ex)){result.Skipped++;}
                    if(watch.ElapsedMilliseconds-reported>=300){reported=watch.ElapsedMilliseconds;progress?.Report(new StorageScanProgress {Files=result.Files,Skipped=result.Skipped,Group=group});}
                    if(result.Files>0&&result.Files%500==0)Thread.Sleep(1);
                }
            }
            catch(Exception ex) when(Recoverable(ex))
            {if(next.Item1.FullName==root.FullName)throw new IOException("No se puede leer la raíz de la unidad; puede estar bloqueada o desconectada.",ex);result.Skipped++;}
        }
        result.Groups.AddRange(groups.Values.Where(g=>g.Bytes>0).OrderByDescending(g=>g.Bytes));result.Elapsed=watch.Elapsed;
        try{var drive=new DriveInfo(root.Root.FullName);result.Total=drive.TotalSize;result.Free=drive.TotalFreeSpace;}catch{}
        return result;
    }
}
