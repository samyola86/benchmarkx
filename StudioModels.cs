using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace Overclock {
 sealed class StudioMode {
  public string Name; public int Value; public uint Flags,SpeedMin,SpeedMax,BrightnessMin,BrightnessMax,ColorsMin,ColorsMax,Speed,Brightness,Direction,ColorMode; public uint[] Colors=new uint[0];
  public override string ToString(){return Name;}
 }
 sealed class StudioZone {public string Id,Name;public int Index,LedCount;public override string ToString(){return Name;}}
 sealed class StudioDevice {
  public string Id,Name,Detail,Status,Profile,ProfileDevice,StateSource,Diagnostics;public DateTime ObservedUtc;public bool Ambiguous,Internal,RestoreIcc;public bool? PerUserProfiles;
  public int? Brightness,Contrast,Temperature;public uint TemperatureFlags;public uint BrightnessMin,BrightnessMax,ContrastMin,ContrastMax;
  public bool HdrSupported,HdrCanToggle,HdrActive;public bool? HdrEnabled;public string HdrStatus;
  public uint Index;public int ActiveMode;public List<StudioMode> Modes=new List<StudioMode>();public List<StudioZone> Zones=new List<StudioZone>();public uint[] Colors=new uint[0];
  public override string ToString(){return Name+"  /  "+Status;}
 }
 sealed class StudioCommand {public string Action="discover",Id,Mode,Zone,Hex,Profile;public int? Brightness,Contrast,Temperature,Speed;public bool? Hdr;public StudioDevice Snapshot;}
 sealed class StudioReply {public List<StudioDevice> Devices=new List<StudioDevice>();public string[] Profiles=new string[0];public string Error,Message;}
 interface IStudioProvider:IDisposable {Task<StudioReply> Execute(StudioCommand command,CancellationToken cancel);}
 // A bounded asynchronous mailbox. All operations (including discovery) are serialized per provider.
 sealed class StudioQueue:IDisposable {
  readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);readonly CancellationTokenSource lifetime=new CancellationTokenSource();int pending;bool disposed;
  public async Task<T> Run<T>(Func<CancellationToken,Task<T>> action,CancellationToken cancel){if(disposed)throw new ObjectDisposedException("Studio");if(Interlocked.Increment(ref pending)>8){Interlocked.Decrement(ref pending);throw new InvalidOperationException("Studio queue full. Wait for the current operation.");}try{using(var linked=CancellationTokenSource.CreateLinkedTokenSource(cancel,lifetime.Token)){await gate.WaitAsync(linked.Token).ConfigureAwait(false);try{linked.Token.ThrowIfCancellationRequested();return await action(linked.Token).ConfigureAwait(false);}finally{gate.Release();}}}finally{Interlocked.Decrement(ref pending);}}
  public void Dispose(){disposed=true;lifetime.Cancel();}
 }
 sealed class DisplayStudioClient:IStudioProvider {
  readonly StudioQueue queue=new StudioQueue();readonly string hostArguments;readonly int timeout;Process host;bool disposed,failed;
  public DisplayStudioClient():this("--studio-host",12000){}
  internal DisplayStudioClient(string arguments,int timeout){hostArguments=arguments;this.timeout=timeout;}
  public Task<StudioReply> Execute(StudioCommand command,CancellationToken cancel){return queue.Run(async token=>{
   if(failed&&command.Action!="discover")throw new InvalidOperationException("Display helper unavailable. Refresh explicitly before retrying.");
   try {if(disposed)throw new ObjectDisposedException("Display Studio");if(host==null||host.HasExited){host=new Process{StartInfo=new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,hostArguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true}};host.Start();}
    host.StandardInput.WriteLine(StudioJson.Serialize(command));host.StandardInput.Flush();var read=host.StandardOutput.ReadLineAsync();var winner=await Task.WhenAny(read,Task.Delay(timeout,token)).ConfigureAwait(false);if(winner!=read){token.ThrowIfCancellationRequested();throw new TimeoutException("Monitor operation timed out; helper stopped. Refresh to retry.");}string line=await read.ConfigureAwait(false);if(line==null||line.Length>2*1024*1024)throw new IOException("Display helper disconnected or oversized response");var result=StudioJson.Parse<StudioReply>(line);failed=false;return result;
   }catch{failed=true;Stop();throw;}
  },cancel);}
  void Stop(){var p=host;host=null;if(p!=null){try{if(!p.HasExited)p.Kill();}catch{}p.Dispose();}}
  public void Dispose(){disposed=true;queue.Dispose();Stop();}
 }
 static class StudioJson {
  public static string Serialize(object value){return new JavaScriptSerializer{MaxJsonLength=2*1024*1024,RecursionLimit=32}.Serialize(value);}
  public static T Parse<T>(string text){if(text==null||text.Length>2*1024*1024)throw new InvalidDataException("Studio data too large");return new JavaScriptSerializer{MaxJsonLength=2*1024*1024,RecursionLimit=32}.Deserialize<T>(text);}
  public static T Clone<T>(T value){return Parse<T>(Serialize(value));}
 }
 sealed class StudioProfile {public int Version=1;public string Name,Kind;public DateTime Utc;public List<StudioDevice> Observations=new List<StudioDevice>();public List<StudioCommand> Targets=new List<StudioCommand>();}
 static class StudioProfiles {
  public static string Root=Path.Combine(ReportStore.Root,"Studios");
  public static void Validate(StudioProfile p){if(p==null||p.Version!=1||(p.Kind!="RGB"&&p.Kind!="Display")||string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>80||p.Targets==null||p.Targets.Count==0||p.Targets.Count>128)throw new InvalidDataException("Invalid studio profile");foreach(var c in p.Targets){if(c==null||string.IsNullOrWhiteSpace(c.Id)||c.Id.Length>2048||c.Action!="apply"||c.Snapshot!=null)throw new InvalidDataException("Invalid target");foreach(var n in new[]{c.Brightness,c.Contrast,c.Speed})if(n.HasValue&&(n<0||n>100))throw new InvalidDataException("Value outside 0-100");if(c.Hex!=null&&!System.Text.RegularExpressions.Regex.IsMatch(c.Hex,"^#[0-9a-fA-F]{6}$"))throw new InvalidDataException("Invalid RGB colour");if(c.Temperature.HasValue&&(c.Temperature<1||c.Temperature>8))throw new InvalidDataException("Invalid colour temperature");if(c.Profile!=null&&(!Path.IsPathRooted(c.Profile)||!(c.Profile.EndsWith(".icc",StringComparison.OrdinalIgnoreCase)||c.Profile.EndsWith(".icm",StringComparison.OrdinalIgnoreCase))))throw new InvalidDataException("Invalid ICC path");}}
  public static void Save(StudioProfile p,string file){Validate(p);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,StudioJson.Serialize(p));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public static StudioProfile Load(string file){if(new FileInfo(file).Length>1024*1024)throw new InvalidDataException("Profile too large");var p=StudioJson.Parse<StudioProfile>(File.ReadAllText(file));Validate(p);return p;}
 }
}
