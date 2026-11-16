using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Overclock {
 static class Tests {
  static void Assert(bool value,string message){if(!value)throw new Exception(message);}
  public static string Run(string folder){
   Directory.CreateDirectory(folder);var log=new StringBuilder();string original=ReportStore.Root;ReportStore.Root=Path.Combine(folder,"report-store-"+Guid.NewGuid().ToString("N"));
   try{
    Assert(DashboardArt.Cpu("AuthenticAMD")=="cpu-amd"&&DashboardArt.Cpu("genuineintel")=="cpu-intel"&&DashboardArt.Cpu("unknown")==null,"CPU vendor mapping failed");
    foreach(var entry in new[]{new[]{"NVIDIA GeForce RTX fixture","10DE","gpu-nvidia"},new[]{"AMD Radeon RX fixture","1002","gpu-amd"},new[]{"Intel Arc A770 Graphics","8086","gpu-intel"}}){var adapter=new AdapterIdentity{Name=entry[0],PnpId="PCI\\VEN_"+entry[1]};Assert(DashboardArt.Gpu(entry[0],new[]{adapter})==entry[2],"GPU PCI mapping failed");Assert(DashboardArt.Gpu("Other selected adapter",new[]{adapter})==null&&DashboardArt.Gpu(entry[0],new[]{adapter,adapter})==null,"Ambiguous adapter mapping failed");}
    foreach(string model in new[]{"Intel UHD Graphics","AMD Radeon(TM) Graphics","AMD Radeon 780M Graphics","Intel Arc 140V GPU"})Assert(DashboardArt.Gpu(model,new[]{new AdapterIdentity{Name=model,PnpId="PCI\\VEN_1002"}})==null,"Integrated adapter artwork was misleading");
    Assert(DashboardArt.Network("Ethernet","Up")=="network-ethernet"&&DashboardArt.Network("Wireless80211","Up")=="network-wifi"&&DashboardArt.Network("Ethernet","Down")==null&&DashboardArt.Network("Tunnel","Up")==null&&DashboardArt.Network("Ethernet","Up",false)==null,"Network switching/type mapping failed");
    Assert(DashboardArt.Storage(new StorageCapacity{Disks=new System.Collections.Generic.List<PhysicalCapacity>{new PhysicalCapacity{MediaType="HDD"}}})==null,"HDD was shown as SSD");
    DashboardArt.Load();Assert(DashboardArt.Get("cpu-amd")!=null&&DashboardArt.Get("gpu-intel")!=null&&DashboardArt.Get("network-wifi")!=null,"Embedded dashboard art missing");
    using(var bitmap=new Bitmap(540,540)){bitmap.SetResolution(144,144);using(var card=new MetricCard{Width=540,Height=540,ArtKey="gpu-nvidia"}){card.Set("GPU","42%","UTILIZATION","ISOLATED GPU FIXTURE","Fixture only — not live telemetry","Fixture history",50,"50°",new float[]{15,30,24,42});using(var g=Graphics.FromImage(bitmap)){typeof(MetricCard).GetMethod("OnPaint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(card,new object[]{new System.Windows.Forms.PaintEventArgs(g,new Rectangle(0,0,540,540))});}bitmap.Save(Path.Combine(folder,"dashboard-144dpi-fixture.png"));}}
    log.AppendLine("PASS CPU/GPU PCI selection, multiple/ambiguous/integrated fallback, Ethernet/Wi-Fi switching, disconnected/VPN fallback, HDD fallback, embedded artwork and 144-DPI card rendering (isolated fixtures)");
    foreach(int dpi in new[]{96,120,144}){float factor=dpi/96f;using(var image=new Bitmap((int)(414*factor),(int)(390*factor))){image.SetResolution(dpi,dpi);using(var card=new MetricCard{Width=image.Width,Height=image.Height,ArtKey="cpu-amd"}){card.Set("CPU","42%","UTILIZATION","ISOLATED CPU FIXTURE — NOT LIVE","Fixture values only","Fixture history",58,"58°",new float[]{20,42,30,42});using(var g=Graphics.FromImage(image))typeof(MetricCard).GetMethod("OnPaint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(card,new object[]{new System.Windows.Forms.PaintEventArgs(g,new Rectangle(Point.Empty,image.Size))});image.Save(Path.Combine(folder,"precision-card-"+dpi+"dpi-fixture.png"));}}}
    log.AppendLine("PASS custom card renderer at 96/120/144 DPI (isolated fixtures, not full-window monitor DPI transitions)");
    log.AppendLine(ScrollTests.Run(folder));
    Assert(Branding.Name=="BenchMark X"&&Branding.Icon.Width>=16&&Branding.LogoBounds.Width>0,"Brand assets failed to load");
    var fixture=ReportStore.Create("diskbench","fixture only","fixture only","Physical disk: Disk #9 | Unit test drive | PRIVATE-SERIAL | 128 GiB | NVMe | Healthy\r\nMounted volume: X:\\ | 128 MiB\r\nProfile: 0 | seconds: 1 | repetitions: 1\r\nSEQ1M Q8T1 READ   123.45 MB/s | 118 IOPS | 12.3 us\r\nCompleted.","Completed");
    var scoreData=DiskScoreData.FromRecord(fixture);Assert(scoreData.Rows.Count==1&&scoreData.Rows[0].Read==123.45&&!scoreData.Rows[0].Write.HasValue,"Scorecard parsing or missing-value handling failed");Assert(!scoreData.Drive.Contains("PRIVATE-SERIAL"),"Serial leaked into shareable title");Assert(scoreData.Utc==fixture.Utc&&scoreData.Settings.Contains("128 MiB"),"Share metadata not preserved");
    scoreData.Drive="ISOLATED UNIT FIXTURE — NOT A BENCHMARK";using(var board=new DiskScoreboard()){board.Data=scoreData;using(var image=board.CreateImage()){Assert(image.Width==1100&&image.Height>250,"Share image has invalid dimensions");image.Save(Path.Combine(folder,"scorecard-unit-fixture.png"));}}
    var copyData=DiskScoreData.FromReport("ISO (large files)   12.50 MB/s\r\nCompleted.","Completed");Assert(copyData.IsCopy&&copyData.Rows[0].Read==12.5,"Copy result mislabeled as separate read/write I/O");
    log.AppendLine("PASS embedded logo/icon, scorecard parsing, unavailable write handling, safe share metadata and PNG rendering (isolated fixtures)");
    Assert(GpuStress.DurationChoice("Until stopped")==0&&GpuStress.DurationChoice("10")==10,"Duration choice mapping failed");
    Assert(GpuStress.ValidSettings(0,1280,720,70)&&!GpuStress.ValidSettings(-1,1280,720,70)&&!GpuStress.ValidSettings(601,1280,720,70),"Unlimited settings validation failed");
    Assert(!GpuStress.DurationExpired(0,86400*365)&&!GpuStress.ParentDeadline(0,86400*365),"Unlimited duration unexpectedly expires");
    Assert(!GpuStress.DurationExpired(10,9.9)&&GpuStress.DurationExpired(10,10)&&GpuStress.ParentDeadline(10,36),"Finite duration protection changed");
    Assert(StressOverlay.Time(90061)=="25:01:01","Long-running timer wrapped at 24 hours");
    using(var bitmap=StressOverlay.Draw(new StressOverlayData{Duration=0,Width=1280,Height=720,Cutoff=70,Telemetry="GPU test fixture, 50, 60, 1024, 12288, 100, 1900, 40, 7500",Temperature=60,PeakTemperature=61,Fps=100,AverageFps=99,Elapsed=90061,TelemetryAge=.5,TemperatureHistory=new float[]{59,60,61,60}})){Assert(bitmap.Width==620&&bitmap.Height==420,"Overlay dimensions invalid");bitmap.Save(Path.Combine(folder,"overlay-unit-fixture.png"));}
    log.AppendLine("PASS unlimited duration mapping, year-long deadline checks, finite watchdog, long timer and overlay drawing (isolated fixtures)");
    Assert(Workers.NvidiaTemperature("GPU, 10, 55, 1, 2, 3, 4, 5")==55,"Temperature parsing failed");foreach(string raw in new[]{"GPU,10,N/A", "GPU,10,NaN", "GPU,10,121", "GPU,10,50\nGPU2,10,40"}){bool failed=false;try{Workers.NvidiaTemperature(raw);}catch{failed=true;}Assert(failed,"Unsafe temperature accepted: "+raw);}log.AppendLine("PASS unavailable, invalid and multi-adapter GPU telemetry rejection");
    var parse=typeof(Workers).GetMethod("Parse",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
    string xml=File.ReadAllText(Path.Combine(folder,"diskspd-readonly.xml"));var score=(double[])parse.Invoke(null,new object[]{xml,false});Assert(score[0]>0&&score[1]>0&&score[2]>0,"Real DiskSpd result has missing metrics");log.AppendLine("PASS real read-only DiskSpd XML: "+score[0]+" MB/s, "+score[1]+" IOPS, "+score[2]+" us");
    bool rejected=false;try{parse.Invoke(null,new object[]{"<Results><TimeSpan><TestTimeSeconds>1</TestTimeSeconds></TimeSpan></Results>",false});}catch{rejected=true;}Assert(rejected,"Malformed XML accepted");log.AppendLine("PASS malformed/incomplete result rejection");
    var r=ReportStore.Create("test","quote, comma \" and newline\r\n","fixture only","SEQ1M Q1T1 READ   12.50 MB/s | 1 IOPS | 2 us\r\nCompleted.","Completed");ReportStore.Save(r);var loaded=ReportStore.Load();Assert(loaded.Count==1&&loaded[0].Id==r.Id&&loaded[0].Metrics.Count==3,"Report round-trip failed");string csv=ReportStore.Export(loaded,".csv");Assert(csv.Contains("\"\" and newline")&&csv.Contains("12.5"),"CSV escaping failed");var exported=new JavaScriptSerializer().Deserialize<RunRecord[]>(ReportStore.Export(loaded,".json"));Assert(exported.Length==1&&exported[0].Id==r.Id,"JSON export failed");File.WriteAllText(Path.Combine(folder,"export-test.csv"),csv);File.WriteAllText(Path.Combine(folder,"export-test.json"),ReportStore.Export(loaded,".json"));log.AppendLine("PASS report persistence, JSON and CSV export/escaping (fixtures isolated from real history)");
    var settings=new AppSettings{SensorInterval=3000,TopMost=true};settings.Save();var settings2=AppSettings.Load();Assert(settings2.SensorInterval==3000&&settings2.TopMost,"Settings persistence failed");log.AppendLine("PASS settings persistence");
    var watch=Stopwatch.StartNew();rejected=false;try{Workers.Run("powershell.exe","-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 10\"",1);}catch(OperationCanceledException){rejected=true;}Assert(rejected&&watch.Elapsed.TotalSeconds<6,"Command timeout failed");log.AppendLine("PASS process timeout and termination: "+watch.Elapsed.TotalSeconds.ToString("0.00")+" s");
    string cancel=Path.Combine(folder,"command.cancel");File.WriteAllText(cancel,"");watch.Restart();rejected=false;try{Workers.Run("powershell.exe","-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 10\"",20,cancel);}catch(OperationCanceledException){rejected=true;}Assert(rejected&&watch.Elapsed.TotalSeconds<6,"Cancellation failed");File.Delete(cancel);log.AppendLine("PASS process cancellation and termination");
    string self=Path.Combine(folder,"unit-selftest.txt");Workers.Execute("selftest",self,new[]{"--worker","selftest",self});log.AppendLine(File.ReadAllText(self));
    return log.ToString();
   }finally{ReportStore.Root=original;}
  }
 }
}
