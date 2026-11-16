using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Overclock {
 static class Workers {
  static readonly object logLock=new object();
  static readonly Queue<string> log=new Queue<string>();
  public static string RecentLogs{get{lock(logLock)return string.Join("\r\n",log.ToArray());}}
  public static void Log(string value){lock(logLock){log.Enqueue(DateTime.Now.ToString("HH:mm:ss")+" "+value);while(log.Count>100)log.Dequeue();}}
  public static string Quote(string value){return "\""+value.Replace("\"","\\\"")+"\"";}
  public static Process StartSelf(string kind,string output,string extra){
   var p=Process.Start(new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,"--worker "+kind+" "+Quote(output)+" "+extra){UseShellExecute=false,CreateNoWindow=true});
   return p;
  }
  public static void KillTree(Process p){
   try{if(p.HasExited)return;using(var killer=Process.Start(new ProcessStartInfo("taskkill.exe","/PID "+p.Id+" /T /F"){UseShellExecute=false,CreateNoWindow=true})){killer.WaitForExit(3000);}}
   catch{}try{if(!p.HasExited)p.Kill();p.WaitForExit(3000);}catch{}
  }
  public static void Write(string file,string value){MonitorService.Atomic(file,value);}
  public static string ReadShared(string file){using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream,Encoding.UTF8))return reader.ReadToEnd();}
  public static string Run(string exe,string args,int timeout,string cancel=null,bool smartStatus=false){
   using(var p=new Process{StartInfo=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}}){
    p.Start();Task<string> stdout=p.StandardOutput.ReadToEndAsync(),stderr=p.StandardError.ReadToEndAsync();
    Stopwatch sw=Stopwatch.StartNew();
    while(!p.WaitForExit(100)){
     if((cancel!=null&&File.Exists(cancel))||sw.Elapsed.TotalSeconds>timeout){KillTree(p);throw new OperationCanceledException("Command cancelled or timed out");}
    }
    if(!Task.WaitAll(new Task[]{stdout,stderr},3000))throw new TimeoutException("Output timed out");
    if(smartStatus)return stdout.Result.Trim()+"\r\n\r\nsmartctl exit bitmask: "+p.ExitCode+((p.ExitCode&7)!=0?" (command/device/partial-data error; available attributes retained)":"")+((p.ExitCode&248)!=0?" (health/error-log warning bits set; inspect report)":"")+"\r\n"+stderr.Result;
    if(p.ExitCode!=0)throw new Exception(exe+" exit "+p.ExitCode+": "+stderr.Result+"\r\n"+stdout.Result);
    return stdout.Result.Trim();
   }
  }
  static string PS(string command,int timeout){return Run("powershell.exe","-NoProfile -NonInteractive -Command "+Quote(command),timeout);}
  public static string Resolve(string name){
   string root=Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
   foreach(string p in new[]{Path.Combine(root,name+".exe"),Path.Combine(root,"tools",name+".exe"),Path.Combine(Path.GetDirectoryName(root),"tools",name+".exe")})if(File.Exists(p))return p;
   foreach(string p in (Environment.GetEnvironmentVariable("PATH")??"").Split(';'))try{string f=Path.Combine(p.Trim('"'),name+".exe");if(File.Exists(f))return f;}catch{}
   return name+".exe";
  }
  public static string Nvidia(){return Run(Resolve("nvidia-smi"),"--query-gpu=name,utilization.gpu,temperature.gpu,memory.used,memory.total,power.draw,clocks.gr,fan.speed,clocks.mem --format=csv,noheader,nounits",5);}
  public static double NvidiaTemperature(){return NvidiaTemperature(Nvidia());}
  public static double NvidiaTemperature(string raw){
   string[] rows=raw.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
   if(rows.Length!=1)throw new Exception("Stress requires exactly one NVIDIA GPU with available telemetry.");
   double t;if(!double.TryParse(rows[0].Split(',')[2],NumberStyles.Float,CultureInfo.InvariantCulture,out t)||double.IsNaN(t)||double.IsInfinity(t)||t<0||t>120)throw new Exception("Temperature unavailable");return t;
  }
  [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint Length,Load;public ulong Total,Available,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended;}
  [DllImport("kernel32.dll")]static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
  [DllImport("kernel32.dll")]static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
  static string Basic(){
   var sb=new StringBuilder();
   long i1,k1,u1,i2,k2,u2;if(!GetSystemTimes(out i1,out k1,out u1))throw new Exception("CPU counter unavailable");Thread.Sleep(250);
   if(!GetSystemTimes(out i2,out k2,out u2))throw new Exception("CPU counter unavailable");
   double totalTicks=(k2-k1)+(u2-u1);double load=totalTicks>0?100*(1-(i2-i1)/totalTicks):0;sb.AppendLine("CPU load     "+Math.Max(0,Math.Min(100,load)).ToString("0.0")+"%");
   using(var key=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))if(key!=null)sb.AppendLine(Convert.ToString(key.GetValue("ProcessorNameString")));
   sb.AppendLine("Logical CPUs "+Environment.ProcessorCount);
   var mem=new MemoryStatus{Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};if(!GlobalMemoryStatusEx(ref mem))throw new Exception("Memory counter unavailable");
   sb.AppendLine("RAM          "+((mem.Total-mem.Available)/1073741824.0).ToString("0.0")+" / "+(mem.Total/1073741824.0).ToString("0.0")+" GiB");
   return sb.ToString();
  }
  static string Gpu(){
   try{var sb=new StringBuilder();foreach(string line in Nvidia().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){var a=line.Split(',');if(a.Length<7)continue;sb.AppendLine(a[0].Trim());sb.AppendLine("Load         "+a[1].Trim()+"%");sb.AppendLine("Temperature  "+a[2].Trim()+" °C");sb.AppendLine("VRAM         "+a[3].Trim()+" / "+a[4].Trim()+" MiB");sb.AppendLine("Power        "+a[5].Trim()+" W");sb.AppendLine("Clock        "+a[6].Trim()+" MHz");if(a.Length>7)sb.AppendLine("Fan          "+a[7].Trim()+" %");}return sb.ToString();}
   catch{return PS("Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,AdapterRAM | Format-List | Out-String",8);}
  }
  static string NativeSerial(int disk){string value=Run(Resolve("smartctl"),"-i /dev/pd"+disk,8,null,true);var match=System.Text.RegularExpressions.Regex.Match(value,@"Serial Number:\s*(.+)");return match.Success?match.Groups[1].Value.Trim():"Unavailable";}
  static string Disks(){string raw=PS("$ErrorActionPreference='Stop'; Get-Disk | ForEach-Object { 'Disk #'+$_.Number+' | '+$_.FriendlyName+' | '+$_.SerialNumber.Trim()+' | '+[math]::Round($_.Size/1GB,1)+' GiB | '+$_.BusType+' | '+$_.HealthStatus }",20);var sb=new StringBuilder();foreach(string line in raw.Split('\n')){int disk;if(!int.TryParse(line.Split('|')[0].Replace("Disk #","").Trim(),out disk))continue;string serial;try{serial=NativeSerial(disk);}catch{serial="Unavailable";}sb.AppendLine(line.Trim()+" | Native serial: "+serial);}return sb.ToString();}
  static string Capacity(){return PS(@"$ErrorActionPreference='Stop'; $disks=@(Get-Disk | Where-Object { [string]$_.BusType -in @('SATA','NVMe','ATA','SAS','SCSI') }); $physical=@(); try{$physical=@(Get-PhysicalDisk)}catch{}; $records=@($disks | ForEach-Object { $disk=$_; $media=$physical | Where-Object { [string]$_.DeviceId -eq [string]$disk.Number } | Select-Object -First 1; @{Number=[int]$disk.Number;Name=[string]$disk.FriendlyName;MediaType=if($media){[string]$media.MediaType}else{'Unknown'};Bytes=[long]$disk.Size} }); $volumes=@($disks | ForEach-Object { Get-Partition -DiskNumber $_.Number | Where-Object DriveLetter | ForEach-Object { try { Get-Volume -DriveLetter $_.DriveLetter } catch{} } } | Where-Object { $_.DriveType -eq 'Fixed' -and $_.Size -gt 0 } | Group-Object UniqueId | ForEach-Object { $_.Group[0] }); @{PhysicalBytes=[long](($records | ForEach-Object {$_.Bytes} | Measure-Object -Sum).Sum);VolumeBytes=[long](($volumes | Measure-Object Size -Sum).Sum);FreeBytes=[long](($volumes | Measure-Object SizeRemaining -Sum).Sum);VolumeCount=$volumes.Count;Disks=$records} | ConvertTo-Json -Depth 6 -Compress",22);}
  static string Inventory(){return PS("Get-CimInstance Win32_Processor | Select Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed | Format-List | Out-String; Get-CimInstance Win32_BaseBoard | Select Manufacturer,Product | Format-List | Out-String; Get-CimInstance Win32_PhysicalMemory | Select Manufacturer,PartNumber,Capacity,Speed | Format-List | Out-String; Get-CimInstance Win32_VideoController | Select Name,DriverVersion | Format-List | Out-String; Get-Disk | Select Number,FriendlyName,SerialNumber,Size,BusType | Format-Table -AutoSize | Out-String",22);}
  public static string ReportHistory(){string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OverclockNative","Reports");if(!Directory.Exists(dir))return "No saved benchmarks yet.";var sb=new StringBuilder();var files=Directory.GetFiles(dir,"*.txt").OrderByDescending(x=>x).Take(20).ToArray();if(files.Length>=2){sb.AppendLine("LATEST TWO RUNS · compare matching workload names and configurations");foreach(string p in files.Take(2)){sb.AppendLine(Path.GetFileNameWithoutExtension(p));foreach(string line in File.ReadLines(p))if(line.Contains("MB/s")||line.Contains("FPS:")||line.StartsWith("Physical disk"))sb.AppendLine(line);}sb.AppendLine("────────────────────────────────────\r\nFULL REPORTS");}foreach(string p in files){sb.AppendLine(Path.GetFileNameWithoutExtension(p));sb.AppendLine(File.ReadAllText(p));sb.AppendLine("────────────────────────────────────");}return sb.ToString();}
  static string Health(){
   var sb=new StringBuilder();
   try{sb.AppendLine(PS("Get-PhysicalDisk | ForEach-Object { $_ | Select FriendlyName,SerialNumber,MediaType,HealthStatus,OperationalStatus,Size | Format-List | Out-String; $_ | Get-StorageReliabilityCounter -ErrorAction SilentlyContinue | Format-List | Out-String }",20));}catch(Exception e){sb.AppendLine(e.Message);}
   try{string exe=Resolve("smartctl"),scan=Run(exe,"--scan-open",10);foreach(string line in scan.Split('\n')){string device=line.Split('#')[0].Trim();if(device.StartsWith("/dev/")){try{sb.AppendLine(Run(exe,"-a "+device,8,null,true));}catch(Exception e){sb.AppendLine(e.Message);}}}}catch{sb.AppendLine("Install smartmontools for raw SMART attributes.");}
   return sb.ToString();
  }
  static string Sensors(){
   var sb=new StringBuilder();
   foreach(string ns in new[]{"root\\LibreHardwareMonitor","root\\OpenHardwareMonitor"})try{
    using(var q=new ManagementObjectSearcher(ns,"SELECT Name,Identifier,SensorType,Value,Min,Max FROM Sensor"))
    foreach(ManagementObject o in q.Get())sb.AppendLine(o["Identifier"]+" | "+o["Name"]+" | "+o["SensorType"]+" | "+o["Value"]+" (min "+o["Min"]+", max "+o["Max"]+")");
   }catch{}
   return sb.Length>0?sb.ToString():"No sensor provider found. Run LibreHardwareMonitor with WMI enabled.";
  }
  static string Volume(int disk,int mb,string identity){
   string cmd="$ErrorActionPreference='Stop'; $d=Get-Disk -Number "+disk+"; $actual='Disk #'+$d.Number+' | '+$d.FriendlyName+' | '+$d.SerialNumber.Trim()+' | '+[math]::Round($d.Size/1GB,1)+' GiB | '+$d.BusType+' | '+$d.HealthStatus; $expected=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('identity-value')); if($actual -ne $expected){throw 'Physical disk identity changed; rescan before testing'}; if($d.IsOffline -or $d.IsReadOnly -or @('RAID','Spaces','Virtual','File Backed Virtual','iSCSI','Unknown') -contains [string]$d.BusType){throw 'Unsafe or unmappable disk'}; $v=Get-Partition -DiskNumber "+disk+" | Where-Object DriveLetter | ForEach-Object { Get-Volume -DriveLetter $_.DriveLetter } | Where-Object { $_.SizeRemaining -gt "+((long)(mb+512)*1048576)+" -and @('NTFS','ReFS','exFAT') -contains $_.FileSystem -and $_.DriveType -eq 'Fixed' } | Sort-Object SizeRemaining -Descending | Select-Object -First 1; if(!$v){throw 'No suitable mounted volume with sufficient space'}; $v.DriveLetter";
   string decoded=Encoding.UTF8.GetString(Convert.FromBase64String(identity));string baseIdentity=string.Join(" | ",decoded.Split('|').Take(6).Select(x=>x.Trim()).ToArray());cmd=cmd.Replace("identity-value",Convert.ToBase64String(Encoding.UTF8.GetBytes(baseIdentity)));string drive=PS(cmd,22).Trim();if(drive.Length!=1||!char.IsLetter(drive[0]))throw new Exception("Unable to map physical disk to volume");return drive+":\\";
  }
  sealed class Row{public string Name;public int Block,Q,T;public bool Random;public Row(string n,int b,int q,int t,bool r){Name=n;Block=b;Q=q;T=t;Random=r;}}
  static Row[] Profile(int profile){
   if(profile==0)return new[]{new Row("SEQ1M Q8T1",1024,8,1,false),new Row("SEQ1M Q1T1",1024,1,1,false),new Row("RND4K Q32T1",4,32,1,true),new Row("RND4K Q1T1",4,1,1,true)};
   if(profile==1)return new[]{new Row("SEQ1M Q8T1",1024,8,1,false),new Row("SEQ128K Q32T1",128,32,1,false),new Row("RND4K Q32T16",4,32,16,true),new Row("RND4K Q1T1",4,1,1,true)};
   return new[]{new Row("Sequential",1024,1,1,false),new Row("4K Q1T1",4,1,1,true),new Row("4K-64Thrd",4,1,64,true),new Row("Access time 512B Q1T1",0,1,1,true)};
  }
  static double Number(XmlNode node,string name){XmlNode n=node.SelectSingleNode(name);if(n==null)throw new Exception("Missing DiskSpd result field: "+name);return double.Parse(n.InnerText,CultureInfo.InvariantCulture);}
  static double[] Parse(string xml,bool write){
   var doc=new XmlDocument();doc.XmlResolver=null;doc.LoadXml(xml);
   var span=doc.SelectSingleNode("/Results/TimeSpan");if(span==null)throw new Exception("Invalid DiskSpd output");
   double seconds=Number(span,"TestTimeSeconds"),bytes=0,count=0;string prefix=write?"Write":"Read";
   foreach(XmlNode n in span.SelectNodes("Thread/Target")){bytes+=Number(n,prefix+"Bytes");count+=Number(n,prefix+"Count");}
   if(seconds<=0||bytes<=0||count<=0)throw new Exception("No operations completed");return new[]{bytes/seconds/1000000,count/seconds,Number(span,"Latency/Average"+prefix+"Milliseconds")*1000};
  }
  static void Check(string output){if(File.Exists(output+".cancel"))throw new OperationCanceledException("Cancelled");}
  static void DriveTemperature(StringBuilder report,string output,int disk){
   double? temperature=null;try{string raw=Run(Resolve("smartctl"),"-A /dev/pd"+disk,5,output+".cancel",true);var match=System.Text.RegularExpressions.Regex.Match(raw,@"(?m)^Temperature:\s+([0-9]+) Celsius");double value;if(match.Success&&double.TryParse(match.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value))temperature=value;}catch(OperationCanceledException){throw;}catch(Exception e){Log("Drive temperature: "+e.Message);}
   report.AppendLine("Drive temperature: "+(temperature.HasValue?temperature.Value.ToString("0")+" C":"Unavailable")+" | thermal guard 70 C (between phases)");Write(output,report.ToString());if(temperature.HasValue&&temperature.Value>=70)throw new Exception("Storage thermal guard reached 70 C; test stopped");
  }
  static void Fill(string path,int mb,string output){byte[] buf=new byte[1048576];new Random().NextBytes(buf);using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,1048576)){for(int i=0;i<mb;i++){Check(output);f.Write(buf,0,buf.Length);}f.Flush(true);}}
  static void Bench(string output,string[] args,bool copy){
   int disk=int.Parse(args[3]),mb=int.Parse(args[4]),profile=int.Parse(args[5]),seconds=int.Parse(args[6]),repeats=int.Parse(args[7]);
   if(disk<0||mb<128||mb>1024||profile<0||profile>4||seconds<1||seconds>30||repeats<1||repeats>5)throw new Exception("Invalid test settings");
   Row[] selected=Profile(profile);if(profile==4){int q=int.Parse(args[8]),t=int.Parse(args[9]),b=int.Parse(args[10]);if(q<1||q>32||t<1||t>64||!new[]{4,128,1024}.Contains(b))throw new Exception("Invalid custom workload");selected=new[]{new Row("Custom "+b+"KiB Q"+q+"T"+t,b,q,t,args[11]=="1")};}
   if(args.Length<14)throw new Exception("Missing physical device identity");string identity=args[12];Convert.FromBase64String(identity);int only=int.Parse(args[13]);if(only < -1||(copy&&only>=3)||(profile==3&&only>=0))throw new Exception("Invalid individual workload");if(only>=0){if(only>=selected.Length)throw new Exception("Invalid individual test");selected=new[]{selected[only]};}
   string nativeIdentity=Encoding.UTF8.GetString(Convert.FromBase64String(identity));int serialAt=nativeIdentity.IndexOf(" | Native serial: ");if(serialAt>=0){string expected=nativeIdentity.Substring(serialAt+18);if(expected!="Unavailable"&&NativeSerial(disk)!=expected)throw new Exception("Native disk serial changed; rescan before testing");}
   if(!copy)Run(Resolve("diskspd"),"-?",5);
   string vol=Volume(disk,copy?2*mb:mb,identity),folder=Path.Combine(vol,"Overclock-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   var sb=new StringBuilder("Physical disk: "+Encoding.UTF8.GetString(Convert.FromBase64String(identity))+"\r\nMounted volume: "+vol+" | "+mb+" MiB\r\nProfile: "+profile+" | seconds: "+seconds+" | repetitions: "+repeats+"\r\n\r\n");
   try{
    DriveTemperature(sb,output,disk);string target=Path.Combine(folder,"test.dat");Write(output,sb+"Preparing test file…");if(!copy)Fill(target,mb,output);
    if(copy){
     File.Delete(target);
     foreach(string scenario in new[]{"ISO (large files)","Program (small files)","Game (mixed files)"}.Where((value,index)=>only<0||only==index)){
      var source=Path.Combine(folder,"source");var dest=Path.Combine(folder,"dest");Directory.CreateDirectory(source);Directory.CreateDirectory(dest);
      try{
       int remaining=mb*1024,index=0;byte[] buf=new byte[1048576];new Random().NextBytes(buf);
       while(remaining>0){Check(output);int kb=Math.Min(remaining,scenario.StartsWith("ISO")?mb*1024:scenario.StartsWith("Program")?64:(index%3==0?8192:128));string f=Path.Combine(source,index.ToString("D6")+".dat");
        using(var stream=new FileStream(f,FileMode.CreateNew)){int left=kb*1024;while(left>0){int n=Math.Min(left,buf.Length);stream.Write(buf,0,n);left-=n;}}remaining-=kb;index++;
       }
       DriveTemperature(sb,output,disk);Stopwatch sw=Stopwatch.StartNew();
       foreach(string f in Directory.GetFiles(source)){Check(output);using(var a=File.OpenRead(f))using(var b=new FileStream(Path.Combine(dest,Path.GetFileName(f)),FileMode.CreateNew)){int n;while((n=a.Read(buf,0,buf.Length))>0){Check(output);b.Write(buf,0,n);}b.Flush(true);}}
       sw.Stop();DriveTemperature(sb,output,disk);sb.AppendLine(scenario+"   "+(mb*1048576.0/sw.Elapsed.TotalSeconds/1000000).ToString("0.00")+" MB/s");Write(output,sb.ToString());
      }finally{Directory.Delete(source,true);Directory.Delete(dest,true);}
     }
    }else if(profile==3){
     foreach(int percent in new[]{0,25,50,75,100}){
      Check(output);DriveTemperature(sb,output,disk);byte[] buffer=new byte[1048576];new Random().NextBytes(buffer);Array.Clear(buffer,0,buffer.Length*percent/100);
      string pattern=Path.Combine(folder,"pattern.dat");File.WriteAllBytes(pattern,buffer);
      using(var f=new FileStream(target,FileMode.Truncate,FileAccess.Write)) {for(int i=0;i<mb;i++){Check(output);f.Write(buffer,0,buffer.Length);}f.Flush(true);}
      foreach(bool write in new[]{false,true}){
       string options="-Rxml -Su -L -W0 -C0 -b1024K -o1 -t1 -d"+seconds+" -w"+(write?"100":"0")+(write?" -Z1M,"+Quote(pattern):"")+" "+Quote(target);
       var score=Parse(Run(Resolve("diskspd"),options,seconds+20,output+".cancel"),write);
       DriveTemperature(sb,output,disk);sb.AppendLine(percent+"% zero data "+(write?"WRITE":"READ")+" "+score[0].ToString("0.00")+" MB/s");Write(output,sb.ToString());
      }
     }
    }else{
     sb.AppendLine("DiskSpd · unbuffered I/O · best measured throughput");
     foreach(Row row in selected)foreach(bool write in new[]{false,true}){
      double[] best=null;int warm=profile==2?0:1;
      for(int i=0;i<repeats+warm;i++){
       Check(output);Write(output,sb+row.Name+" "+(write?"write":"read")+" "+(i<warm?"warm-up":"run "+(i-warm+1)+"/"+repeats));
       string options="-Rxml -Su -L -W0 -C0 -b"+(row.Block==0?"512":row.Block+"K")+" -o"+row.Q+" -t"+row.T+" -d"+seconds+" -w"+(write?"100":"0")+(row.Random?" -r":"")+(write?" -Z1M":"")+" "+Quote(target);
       var score=Parse(Run(Resolve("diskspd"),options,seconds+20,output+".cancel"),write);
       DriveTemperature(sb,output,disk);if(i>=warm&&(best==null||score[0]>best[0]))best=score;
      }
      sb.AppendLine(row.Name+" "+(write?"WRITE":"READ")+"   "+best[0].ToString("0.00")+" MB/s | "+best[1].ToString("0")+" IOPS | "+best[2].ToString("0.0")+" us");Write(output,sb.ToString());
     }
    }
    Write(output,sb+"\r\nCompleted.");
   }finally{try{Directory.Delete(folder,true);}catch{}}
  }
  public static void Execute(string kind,string output,string[] args){
   switch(kind){
    case "test-suite":Write(output,Tests.Run(args[3]));break;
    case "selftest":
     var rows=Profile(0);if(rows.Length!=4||rows[2].Q!=32||!rows[2].Random)throw new Exception("Profile test failed");
     var score=Parse("<Results><TimeSpan><TestTimeSeconds>2</TestTimeSeconds><Latency><AverageReadMilliseconds>0.2</AverageReadMilliseconds></Latency><Thread><Target><ReadBytes>2000000</ReadBytes><ReadCount>200</ReadCount></Target></Thread></TimeSpan></Results>",false);
     if(score[0]!=1||score[1]!=100||Math.Abs(score[2]-200)>.01)throw new Exception("Result parser test failed");
     Write(output,"PASS: workload profiles and XML result parser");break;
    case "delay-test":Thread.Sleep(15000);Write(output,"Delayed worker done");break;
    case "sensor-service":MonitorService.Run(output,int.Parse(args[3]));break;
    case "network":Write(output,NetworkSample.Read());break;
    case "capacity":Write(output,Capacity());break;
    case "mapping":Write(output,Volume(int.Parse(args[3]),128,args[4]));break;
    case "health-disk":Write(output,Run(Resolve("smartctl"),"-x /dev/pd"+int.Parse(args[3]),20,null,true));break;
    case "activity":Write(output,PS("Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk | Select Name,DiskReadBytesPersec,DiskWriteBytesPersec,PercentDiskTime | Format-Table -AutoSize | Out-String",8));break;
    case "cores":Write(output,PS("Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor | Select Name,PercentProcessorTime | Format-Table -AutoSize | Out-String",8));break;
    case "basic":Write(output,Basic());break;case "gpu":Write(output,Gpu());break;case "disks":Write(output,Disks());break;case "health":Write(output,Health());break;case "sensors":Write(output,Sensors());break;
    case "diskbench":Bench(output,args,false);break;case "copy":Bench(output,args,true);break;
    case "inventory":Write(output,Inventory());break;
    case "stress":GpuStress.Run(output,int.Parse(args[3]),int.Parse(args[4]),int.Parse(args[5]),int.Parse(args[6]),args.Length>7&&args[7]=="1");break;
    default:throw new Exception("Unknown worker");
   }
  }
 }
}



