using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using LibreHardwareMonitor.Hardware;

namespace Overclock {
 public sealed class SensorReading {
  public string Id, Hardware, Name, Type; public float? Value, Min, Max;
 }
 public sealed class SensorSnapshot {
  public DateTime Utc; public string Error; public List<SensorReading> Sensors=new List<SensorReading>();
 }
 static class MonitorService {
  public static void Run(string path,int parentId) {
   var computer=new Computer { IsCpuEnabled=true,IsGpuEnabled=true,IsMemoryEnabled=true,IsMotherboardEnabled=true,IsStorageEnabled=true,IsNetworkEnabled=true,IsControllerEnabled=true }; {
    try {
     computer.Open();
     while(ParentAlive(parentId)&&!File.Exists(path+".cancel")) {
      if(File.Exists(path+".reset")){foreach(var h in computer.Hardware)Reset(h);File.Delete(path+".reset");}
      var snapshot=new SensorSnapshot{Utc=DateTime.UtcNow};
      foreach(var h in computer.Hardware) {try{Collect(h,snapshot.Sensors);}catch(Exception e){snapshot.Error=e.Message;}}
      if(snapshot.Sensors.Count==0)snapshot.Error="No supported sensors available. Some motherboard/CPU sensors require administrator privileges and the upstream PawnIO driver.";
      Atomic(path,new JavaScriptSerializer().Serialize(snapshot));
      int interval=2000;try{int.TryParse(File.ReadAllText(path+".interval"),out interval);}catch{}interval=Math.Max(1000,Math.Min(10000,interval));
      for(int i=0;i<interval/100&&ParentAlive(parentId)&&!File.Exists(path+".cancel");i++)Thread.Sleep(100);
     }
    }finally{computer.Close();}
   }
  }
  static bool ParentAlive(int id){try{using(var p=Process.GetProcessById(id))return !p.HasExited;}catch{return false;}}
  static void Reset(IHardware h){foreach(var s in h.Sensors){s.ResetMin();s.ResetMax();}foreach(var child in h.SubHardware)Reset(child);}
  static void Collect(IHardware h,List<SensorReading> list){h.Update();bool cpuAccessUnavailable=h.HardwareType==HardwareType.Cpu&&!h.Sensors.Any(s=>s.SensorType==SensorType.Clock&&s.Value.HasValue&&s.Value.Value>0);foreach(var s in h.Sensors){bool unavailable=cpuAccessUnavailable&&(s.SensorType==SensorType.Clock||s.SensorType==SensorType.Power||s.SensorType==SensorType.Voltage||s.SensorType==SensorType.Factor);list.Add(new SensorReading{Id=s.Identifier.ToString(),Hardware=h.Name,Name=s.Name,Type=s.SensorType.ToString(),Value=unavailable?null:s.Value,Min=unavailable?null:s.Min,Max=unavailable?null:s.Max});}if(cpuAccessUnavailable)list.Add(new SensorReading{Id=h.Identifier+"/temperature/unavailable",Hardware=h.Name,Name="CPU temperature / low-level access unavailable (restart Windows if setup requested it)",Type="Temperature"});foreach(var child in h.SubHardware)Collect(child,list);}
  public static void Atomic(string path,string text){string tmp=path+".tmp";File.WriteAllText(tmp,text,Encoding.UTF8);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
 }
 sealed class SensorClient:IDisposable {
  readonly string path=Path.Combine(Path.GetTempPath(),"overclock-sensors-"+Guid.NewGuid().ToString("N"));
  Process process; DateTime started,retry; volatile bool busy; public volatile SensorSnapshot Snapshot=new SensorSnapshot{Error="Starting isolated sensor service"};
  public readonly Dictionary<string,Queue<float>> History=new Dictionary<string,Queue<float>>();
  DateTime recorded; public int Interval=2000;
  public void Poll(){
   if(busy)return;busy=true;
   System.Threading.Tasks.Task.Run(()=>{
    try{
     if(process==null||process.HasExited){if(DateTime.UtcNow<retry)return;if(process!=null)process.Dispose();process=Workers.StartSelf("sensor-service",path,Process.GetCurrentProcess().Id.ToString());started=DateTime.UtcNow;retry=started.AddSeconds(30);}
     File.WriteAllText(path+".interval",Interval.ToString());
     if(File.Exists(path)){
      var snapshot=new JavaScriptSerializer().Deserialize<SensorSnapshot>(Workers.ReadShared(path));
      if((DateTime.UtcNow-snapshot.Utc).TotalSeconds>30){Workers.KillTree(process);throw new TimeoutException("Sensor service stopped responding; restarting");}Snapshot=snapshot;
     }else if((DateTime.UtcNow-started).TotalSeconds>30){Workers.KillTree(process);throw new TimeoutException("Sensor initialization exceeded 30 seconds");}
    }catch(Exception e){Snapshot=new SensorSnapshot{Error=e.Message};}finally{busy=false;}
   });
  }
  public void Record(){var snapshot=Snapshot;if(snapshot.Utc==recorded)return;recorded=snapshot.Utc;foreach(var s in snapshot.Sensors)if(s.Value.HasValue){Queue<float> q;if(!History.TryGetValue(s.Id,out q)){q=new Queue<float>();History[s.Id]=q;}q.Enqueue(s.Value.Value);while(q.Count>300)q.Dequeue();}}
  public string Text(string category){var snapshot=Snapshot;var sb=new StringBuilder();if(!string.IsNullOrEmpty(snapshot.Error))sb.AppendLine(snapshot.Error);sb.AppendLine("Sensors: "+snapshot.Utc.ToLocalTime()+" | current / minimum / maximum");foreach(var group in snapshot.Sensors.Where(s=>category==null||s.Id.IndexOf(category,StringComparison.OrdinalIgnoreCase)>=0).GroupBy(s=>s.Hardware)){sb.AppendLine("\r\n"+group.Key);foreach(var s in group)sb.AppendLine("  "+s.Name+" ["+s.Type+"]  "+Fmt(s.Value)+" / "+Fmt(s.Min)+" / "+Fmt(s.Max));}return sb.ToString();}
  static string Fmt(float? v){return v.HasValue?v.Value.ToString("0.##",CultureInfo.CurrentCulture):"Unavailable";}
  public void Reset(){History.Clear();System.Threading.Tasks.Task.Run(()=>{try{File.WriteAllText(path+".reset","");}catch(Exception error){Workers.Log("Sensor reset: "+error.Message);}});}
  public void Dispose(){try{File.WriteAllText(path+".cancel","");if(process!=null){if(!process.WaitForExit(2000))Workers.KillTree(process);process.Dispose();}}catch{}foreach(var f in new[]{path,path+".cancel",path+".reset",path+".interval",path+".tmp"})try{File.Delete(f);}catch{}}
 }
 static class NetworkSample {
  [System.Runtime.InteropServices.DllImport("iphlpapi.dll")]static extern int GetBestInterface(uint destination,out uint index);
  public static string Read(){var physical=new HashSet<string>(StringComparer.OrdinalIgnoreCase);try{using(var query=new System.Management.ManagementObjectSearcher("SELECT GUID,PhysicalAdapter FROM Win32_NetworkAdapter"))using(var rows=query.Get())foreach(System.Management.ManagementObject row in rows)using(row){if(Convert.ToBoolean(row["PhysicalAdapter"]??false))physical.Add(Convert.ToString(row["GUID"]));}}catch{}var adapters=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).ToArray();uint route;bool found=GetBestInterface(0x08080808,out route)==0;Func<NetworkInterface,bool> routed=n=>{try{return found&&n.GetIPProperties().GetIPv4Properties().Index==route;}catch{return false;}};var ordered=adapters.OrderByDescending(routed).ThenBy(n=>n.Id).ToArray();var before=ordered.ToDictionary(n=>n.Id,n=>n.GetIPv4Statistics());var watch=Stopwatch.StartNew();Thread.Sleep(500);var sb=new StringBuilder();foreach(var n in ordered){if(n.OperationalStatus!=OperationalStatus.Up)continue;var a=before[n.Id];var b=n.GetIPv4Statistics();double seconds=watch.Elapsed.TotalSeconds;sb.AppendLine(n.Name+"  RX "+Math.Max(0,(b.BytesReceived-a.BytesReceived)/seconds/1024).ToString("0.0",CultureInfo.InvariantCulture)+" KiB/s  TX "+Math.Max(0,(b.BytesSent-a.BytesSent)/seconds/1024).ToString("0.0",CultureInfo.InvariantCulture)+" KiB/s  [Type="+n.NetworkInterfaceType+";Physical="+physical.Contains(n.Id)+";State="+n.OperationalStatus+";Route="+(routed(n)?"Default IPv4 route":"Active adapter")+"]");}return sb.Length>0?sb.ToString():"No active network adapters";}
 }
}

