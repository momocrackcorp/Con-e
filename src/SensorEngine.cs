using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;

// The worker accepts only sample requests. No file paths, commands, device writes or fan controls.
public sealed class SensorReading
{
    public string Id,Group,Device,Name,Unit;
    public float? Value,Min,Max;
}
public sealed class DiskReading
{
    public int Number;public string Model,Serial,Status;
    public int? Temperature;public double? Life,ReadGB,WrittenGB;public ulong? Hours;
}
public sealed class SensorFrame
{
    public DateTime Time;public string Error="";public bool PawnIO;
    public List<SensorReading> Sensors=new List<SensorReading>();
    public List<DiskReading> Disks=new List<DiskReading>();
}
internal static class SensorWorker
{
    internal static int Run(string pipe)
    {
        Guid id;if(!pipe.StartsWith("ConE-sensors-")||!Guid.TryParseExact(pipe.Substring(13),"N",out id))return 2;
        try
        {
            using(var connection=new NamedPipeClientStream(".",pipe,PipeDirection.InOut,PipeOptions.None))
            {
                connection.Connect(15000);
                using(var reader=new StreamReader(connection))using(var writer=new StreamWriter(connection){AutoFlush=true})
                {
                    var computer=new Computer {IsCpuEnabled=true,IsGpuEnabled=true,IsMotherboardEnabled=true,IsStorageEnabled=true,IsMemoryEnabled=false,IsNetworkEnabled=false,IsControllerEnabled=false};
                    var json=new JavaScriptSerializer {MaxJsonLength=2097152};string openError="";
                    try{computer.Open();}catch(Exception ex){openError=ex.GetType().Name+": "+ex.Message;}
                    try
                    {
                        while(reader.ReadLine()=="sample")
                        {
                            var frame=new SensorFrame {Time=DateTime.Now,Error=openError,PawnIO=PawnInstalled()};
                            foreach(var hardware in computer.Hardware.ToArray())try{Collect(hardware,frame);}catch(Exception ex){frame.Error="Lectura parcial: "+ex.Message;}
                            writer.WriteLine(json.Serialize(frame));
                        }
                    }
                    finally{computer.Close();}
                }
            }
            return 0;
        }
        catch{return 1;}
    }
    internal static bool PawnInstalled(){using(var k=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"))return k!=null;}
    static string Group(IHardware h){string t=h.HardwareType.ToString();return t=="Cpu"?"CPU":t.StartsWith("Gpu")?"GPU":t=="Storage"?"Disco":"Placa / chasis";}
    static void Collect(IHardware h,SensorFrame frame)
    {
        h.Update();
        foreach(var sensor in h.Sensors)
        {
            bool temperature=sensor.SensorType==SensorType.Temperature,fan=sensor.SensorType==SensorType.Fan;
            // Warning/critical temperatures are limits, not measurements.
            if((!temperature&&!fan)||sensor.Name.IndexOf("Warning",StringComparison.OrdinalIgnoreCase)>=0||sensor.Name.IndexOf("Critical",StringComparison.OrdinalIgnoreCase)>=0)continue;
            float? value=sensor.Value;
            if(value.HasValue&&(float.IsNaN(value.Value)||float.IsInfinity(value.Value)||(temperature&&(value.Value< -50||value.Value>180))||(fan&&value.Value<0)))value=null;
            frame.Sensors.Add(new SensorReading {Id=sensor.Identifier.ToString(),Group=Group(h),Device=h.Name,Name=sensor.Name,Unit=temperature?"°C":"RPM",Value=value});
        }
        var disk=h as StorageDevice;
        if(disk!=null)
        {
            var s=disk.Storage.Smart;bool valid=s.SmartAttributes.Count>0&&s.DiskStatus.ToString()!="Unknown";
            var d=new DiskReading {Number=disk.Storage.DriveNumber,Model=disk.Storage.Model,Serial=disk.Storage.SerialNumber,Status=valid?s.DiskStatus.ToString():"Unknown"};
            if(valid)
            {
                if(s.Temperature.HasValue&&s.Temperature.Value>0&&s.Temperature.Value<150)d.Temperature=s.Temperature;
                // Toolkit may default counters to zero when unsupported. Never display these as proven zeros.
                if(s.Life.HasValue&&s.Life.Value>0&&s.Life.Value<=100)d.Life=s.Life.Value;
                if(s.HostReads.HasValue&&s.HostReads.Value>0)d.ReadGB=s.HostReads.Value;
                if(s.HostWrites.HasValue&&s.HostWrites.Value>0)d.WrittenGB=s.HostWrites.Value;
                ulong hours=Math.Max(s.MeasuredPowerOnHours,s.DetectedPowerOnHours);if(hours>0)d.Hours=hours;
            }
            frame.Disks.Add(d);
        }
        foreach(var child in h.SubHardware)Collect(child,frame);
    }
}
internal sealed class SensorClient : IDisposable
{
    NamedPipeServerStream pipe;CancellationTokenSource cancellation;Task polling;
    readonly Dictionary<string,Tuple<float,float>> extrema=new Dictionary<string,Tuple<float,float>>();
    internal event Action<SensorFrame> Updated;
    internal static SensorFrame Latest;
    internal bool Active {get{return cancellation!=null;}}
    internal Task Start(bool elevated)
    {
        Stop();extrema.Clear();cancellation=new CancellationTokenSource();polling=Poll(elevated,cancellation.Token);return polling;
    }
    async Task Poll(bool elevated,CancellationToken token)
    {
        NamedPipeServerStream local=null;
        try
        {
            string name="ConE-sensors-"+Guid.NewGuid().ToString("N");var security=new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
            local=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);pipe=local;
            using(var worker=Process.Start(new ProcessStartInfo {FileName=System.Windows.Forms.Application.ExecutablePath,Arguments="--sensor-worker "+name,UseShellExecute=true,Verb=elevated?"runas":"",WindowStyle=ProcessWindowStyle.Hidden})){}
            var connection=local.WaitForConnectionAsync(token);if(await Task.WhenAny(connection,Task.Delay(30000,token))!=connection)throw new TimeoutException("El módulo no respondió o se canceló el permiso.");await connection;
            using(var reader=new StreamReader(local))using(var writer=new StreamWriter(local){AutoFlush=true})
            {
                var json=new JavaScriptSerializer {MaxJsonLength=2097152};
                while(!token.IsCancellationRequested)
                {
                    await writer.WriteLineAsync("sample");var read=reader.ReadLineAsync();if(await Task.WhenAny(read,Task.Delay(20000,token))!=read)throw new TimeoutException("Tiempo de lectura de sensores agotado.");
                    string line=await read;if(line==null||line.Length>2097152)throw new IOException("El módulo de sensores terminó.");var frame=json.Deserialize<SensorFrame>(line);if(token.IsCancellationRequested)return;ApplyExtrema(frame);Latest=frame;Updated?.Invoke(frame);await Task.Delay(3000,token);
                }
            }
        }
        catch(Exception ex){if(!token.IsCancellationRequested){Latest=null;Updated?.Invoke(new SensorFrame {Time=DateTime.Now,Error="Sensores no disponibles: "+ex.Message});}}
        finally{local?.Dispose();if(ReferenceEquals(pipe,local)){pipe=null;var source=cancellation;cancellation=null;source?.Dispose();}}
    }
    internal void ApplyExtrema(SensorFrame frame)
    {
        foreach(var s in frame.Sensors)if(s.Value.HasValue){Tuple<float,float> range;extrema.TryGetValue(s.Id,out range);float low=range==null?s.Value.Value:Math.Min(range.Item1,s.Value.Value),high=range==null?s.Value.Value:Math.Max(range.Item2,s.Value.Value);extrema[s.Id]=Tuple.Create(low,high);s.Min=low;s.Max=high;}
    }
    internal void Stop(){var old=cancellation;cancellation=null;old?.Cancel();pipe?.Dispose();pipe=null;Latest=null;if(old!=null){if(polling!=null)polling.ContinueWith(t=>old.Dispose(),TaskScheduler.Default);else old.Dispose();}}
    public void Dispose(){Stop();}
}
