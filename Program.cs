using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Overclock {
 static class Program {
  [STAThread] static void Main(string[] args) {
   if(args.Length==1&&args[0]=="--studio-host"){NativeDisplayStudio.Host();return;}
   if(args.Length>=3 && args[0]=="--worker") {
    try { Workers.Execute(args[1],args[2],args); }
    catch(Exception e) { string previous="";try{if(File.Exists(args[2]))previous=File.ReadAllText(args[2])+"\r\n";}catch{}Workers.Write(args[2],previous+"ERROR: "+e.Message); Environment.ExitCode=1; }
    return;
   }
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   Task.Run(()=>DashboardArt.Load()).GetAwaiter().GetResult(); Application.ApplicationExit+=(s,e)=>DashboardArt.Dispose(); var window=new MainWindow();if(args.Length>=2&&args[0]=="--ui-test")window.BeginUiTest(args[1]);if(args.Length>=2&&args[0]=="--sidebar-test")window.BeginSidebarTest(args[1]);if(args.Length>=2&&args[0]=="--studio-ui-test")window.BeginStudioUiTest(args[1]);if(args.Length>=2&&args[0]=="--studio-connected-ui-test")window.BeginStudioUiTest(args[1],true);if(args.Length>=2&&args[0]=="--live-brightness-test")window.BeginLiveBrightnessTest(args[1]);if(args.Length>=2&&args[0]=="--hdr-compact-test")window.BeginHdrCompactTest(args[1]);Application.Run(window);
  }
 }
 sealed class Collector {
  public readonly string Key; public readonly int Interval, Timeout;
  public volatile string Value="Waiting for first reading", Error="";
  public volatile bool Busy; public DateTime Updated=DateTime.MinValue, Next=DateTime.MinValue;
  public Collector(string key,int interval,int timeout){Key=key;Interval=interval;Timeout=timeout;}
  public void Request(bool force) {
   if(Busy || (!force && DateTime.UtcNow<Next)) return;
   Busy=true;
   Task.Run(()=> {
    string file=Path.Combine(Path.GetTempPath(),"overclock-"+Guid.NewGuid().ToString("N")+".txt");
    try {
     using(var p=Workers.StartSelf(Key,file,"")) {
      if(!p.WaitForExit(Timeout*1000)){Workers.KillTree(p);throw new TimeoutException("Provider timed out");}
      string result=File.ReadAllText(file);
      if(p.ExitCode!=0 || result.StartsWith("ERROR:")) throw new Exception(result);
      Value=result;Error="";Updated=DateTime.UtcNow;
     }
    } catch(Exception e){Error=e.Message;Workers.Log(Key+": "+e.Message);}
    finally { try{File.Delete(file);}catch{} Next=DateTime.UtcNow.AddSeconds(Interval);Busy=false; }
   });
  }
  public string Display {get{return Value+(Busy?"\r\n\r\nRefreshing in background…":"")+(Error!=""?"\r\n\r\nLast scan: "+Error:"");}}
 }
 sealed partial class MainWindow:Form {
  readonly StudioWorkspace studios=new StudioWorkspace();
  readonly Color bg=Theme.Background, card=Theme.Surface, accent=Theme.Accent;
  readonly Dictionary<string,Collector> sources=new Dictionary<string,Collector>();
  readonly Dictionary<string,MetricCard> tiles=new Dictionary<string,MetricCard>();
  readonly FlowLayoutPanel tabs=new FlowLayoutPanel();readonly Label subtitle=new Label();readonly Dictionary<string,ModernButton> navButtons=new Dictionary<string,ModernButton>();readonly Panel body=new Panel(),pageHeader=new Panel(); readonly Label heading=new PageHeading(), status=new PageFooter();
  readonly System.Windows.Forms.Timer tick=new System.Windows.Forms.Timer();
  readonly DarkCombo disks=new DarkCombo(),profile=new DarkCombo(),size=new DarkCombo(),duration=new DarkCombo(),repeats=new DarkCombo(),qd=new DarkCombo(),threads=new DarkCombo(),block=new DarkCombo(),pattern=new DarkCombo(),resolution=new DarkCombo(),limit=new DarkCombo();
  readonly SensorClient sensorClient=new SensorClient(); readonly AppSettings settings=AppSettings.Load(); readonly DarkCombo test=new DarkCombo(),windowMode=new DarkCombo(),gpuSelect=new DarkCombo();readonly FlowLayoutPanel individualTests=new FlowLayoutPanel{Dock=DockStyle.Top,Height=44}; readonly ProgressBar progress=new ProgressBar{Dock=DockStyle.Top,Height=6,Style=ProgressBarStyle.Marquee}; readonly SensorGraph stressGraph=new SensorGraph{Dock=DockStyle.Bottom,Height=160};readonly Queue<float> stressTemperature=new Queue<float>();string lastStressSample=""; readonly SensorGraph compressionGraph=new SensorGraph{Dock=DockStyle.Bottom,Height=170};
  readonly Queue<float> cpuHistory=new Queue<float>(),gpuHistory=new Queue<float>(),networkHistory=new Queue<float>();
  readonly List<Trend> charts=new List<Trend>();
  DateTime lastSample; string reportCache="Loading reports…"; bool reportsBusy; DateTime nextReports;
  bool compact; Size normalSize; readonly SidebarPanel navBar=new SidebarPanel();
  readonly DarkRichTextBox detail=new DarkRichTextBox(); readonly Button start=new ModernButton{Primary=true},cancel=new ModernButton();
  string page="Overview", lastDiskData="", lastDetail=""; volatile Process active; volatile bool running, cancelRequested;
  volatile string benchmarkText="Ready";
  public MainWindow(){
   Text=Branding.Name+" "+Branding.Version;InitializeBranding(); AutoScaleMode=AutoScaleMode.Dpi; Size=new Size(1586,992); MinimumSize=new Size(780,600); BackColor=bg; ForeColor=Color.White;
   Font=new Font("Segoe UI",12); StartPosition=FormStartPosition.CenterScreen;
   sources.Add("Identity",new Collector("identity",120,16));sources.Add("CPU",new Collector("basic",3,8));sources.Add("GPU",new Collector("gpu",4,8));
   sources.Add("Storage",new Collector("disks",60,50));sources.Add("Capacity",new Collector("capacity",30,30));sources.Add("Drive Health",new Collector("health",120,45));
   sources.Add("Sensors",new Collector("sensors",20,15));sources.Add("Inventory",new Collector("inventory",120,25));sources.Add("Network",new Collector("network",3,12));sources.Add("Core load",new Collector("cores",3,12));sources.Add("Disk activity",new Collector("activity",3,12));
   sensorClient.Interval=settings.SensorInterval;TopMost=settings.TopMost;
   var nav=navBar;nav.Dock=DockStyle.Left;nav.Width=264;
   var brand=new BrandLogo{Size=new Size(232,58),BackColor=nav.BackColor};nav.Controls.Add(brand);var version=new SidebarLabel{LogicalHeight=48,Text="HARDWARE STUDIO   /   "+Branding.Version,Font=Theme.Face(10),ForeColor=Theme.Muted};version.Size=new Size(232,48);nav.Controls.Add(version);
   foreach(string group in new[]{"Overview","Hardware","Storage tests","GPU stress","Reports","RGB Studio","Display Studio"}){string destination=group=="Hardware"?"CPU":group=="Storage tests"?"Disk Benchmark":group=="GPU stress"?"GPU Stress Test":group;var b=(ModernButton)Theme.Button(group);b.Width=232;b.Height=48;b.Margin=new Padding(0,0,0,10);b.Font=Theme.Face(12);b.NavGlyph=group;b.Click+=(sender,args)=>Navigate(destination);nav.Controls.Add(b);navButtons[group]=b;}
   foreach(string name in new[]{"Settings","Logs"}){string destination=name;var b=ButtonFor(name);b.Width=232;b.Height=48;b.Margin=new Padding(0,0,0,10);((ModernButton)b).NavGlyph=name;b.Click+=(sender,args)=>Navigate(destination);nav.Controls.Add(b);}
   var mini=ButtonFor("Compact monitor");mini.Width=232;mini.Height=48;((ModernButton)mini).NavGlyph="Compact monitor";mini.Click+=(sender,args)=>ToggleMini();nav.Controls.Add(mini);
   var right=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16,8,16,0),BackColor=bg};
   var header=pageHeader;header.Dock=DockStyle.Top;header.Height=106;heading.Dock=DockStyle.Top;heading.Height=60;heading.Font=new Font("Segoe UI",27,FontStyle.Bold|FontStyle.Italic);heading.ForeColor=Theme.Text;heading.Click+=(sender,args)=>{if(compact)ToggleMini();};subtitle.Dock=DockStyle.Bottom;subtitle.Height=34;subtitle.ForeColor=Theme.Muted;subtitle.Font=new Font("Segoe UI",13,FontStyle.Italic);header.Controls.Add(heading);header.Controls.Add(subtitle);
   tabs.Dock=DockStyle.Top;tabs.Height=51;tabs.WrapContents=false;tabs.AutoScroll=true;tabs.Padding=new Padding(0,0,0,10);
   status.Dock=DockStyle.Bottom;status.Height=58;status.ForeColor=Theme.Muted;status.Font=new Font("Segoe UI",9);
   body.Dock=DockStyle.Fill;right.Controls.Add(body);right.Controls.Add(tabs);right.Controls.Add(header);right.Controls.Add(status);Controls.Add(right);Controls.Add(nav);
   tick.Interval=500;tick.Tick+=(s,e)=>UpdateView();tick.Start();
   Shown+=(s,e)=>{sources["CPU"].Request(true);sources["GPU"].Request(true);};
   FormClosing+=(s,e)=>{if(running){e.Cancel=true;CancelRun();status.Text="Stopping benchmark; close again once stopped."; } };
   FormClosed+=(s,e)=>{tick.Stop();sensorClient.Dispose();studios.Dispose();};
   Navigate("Overview");
  }
  Button ButtonFor(string title){return Theme.Button(title);}
  void Navigate(string next){
   page=next;heading.Text=next;BuildTabs();foreach(Control shared in new Control[]{detail,disks,profile,size,duration,repeats,qd,threads,block,pattern,resolution,limit,test,windowMode,start,cancel,progress,compressionGraph,stressGraph,gpuSelect,individualTests})if(shared.Parent!=null)DarkScrolling.Detach(shared);foreach(Control control in body.Controls.Cast<Control>().ToArray())control.Dispose();body.Controls.Clear();tiles.Clear();charts.Clear();lastDetail="";
   if(next=="Overview"){
    var grid=new OverviewGrid(compact);
    foreach(string k in compact?new[]{"CPU","GPU","Memory"}:new[]{"CPU","GPU","Memory","Storage","Network","Sensors"}){string key=k;var tile=new MetricCard{Compact=compact,Accent=k=="GPU"?Theme.Cyan:k=="Network"||k=="Memory"?Theme.Mint:Theme.Accent};tile.Click+=(sender,args)=>{if(compact)ToggleMini();Navigate(key);};grid.Controls.Add(tile);tiles[k]=tile;}body.Controls.Add(grid);if(compact)body.Controls.Add(new StudioView(false,studios,()=>running,true){Dock=DockStyle.Bottom,Height=245});UpdateCards();
   } else if(next=="Reports"){body.Controls.Add(new ReportView());}
   else if(next=="Sensors"){body.Controls.Add(new SensorView(sensorClient));}
   else if(next=="RGB Studio"||next=="Display Studio"){body.Controls.Add(new StudioView(next=="RGB Studio",studios,()=>running));}
   else if(next=="Settings"){BuildSettings();}
   else if(next=="Drive Health"){body.Controls.Add(new HealthView(sources["Storage"]));}
   else if(new[]{"CPU","GPU","Memory","Storage","Network","Inventory"}.Contains(next)){body.Controls.Add(new HardwareView(next,sensorClient,sources));}
   else if(next.EndsWith("Benchmark")||next=="GPU Stress Test")BuildBenchmark();
   else {
    var refresh=ButtonFor("Refresh");refresh.Dock=DockStyle.Top;refresh.Click+=(s,e)=>{if(sources.ContainsKey(page))sources[page].Request(true);else{nextReports=DateTime.MinValue;UpdateView();}};
    SetupText();var logPanel=new SectionPanel("Diagnostic log"){Dock=DockStyle.Fill};logPanel.Controls.Add(detail);body.Controls.Add(logPanel);
    if(next=="Reports"){var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48};refresh.Dock=DockStyle.None;buttons.Controls.Add(refresh);var export=ButtonFor("Export history");export.Click+=(s,e)=>{using(var dlg=new SaveFileDialog{Filter="Text report|*.txt",FileName="BenchMark-X-history.txt"})if(dlg.ShowDialog()==DialogResult.OK){string path=dlg.FileName,value=reportCache;Task.Run(()=>{try{File.WriteAllText(path,value);}catch(Exception ex){Workers.Log(ex.Message);}});}};buttons.Controls.Add(export);body.Controls.Add(buttons);}else body.Controls.Add(refresh);
    if(sources.ContainsKey(next))sources[next].Request(true);
   }
   DarkScrolling.Install(body);DarkScrolling.Install(this);foreach(var scrollFrame in Descendants(this).OfType<DarkScrollFrame>())scrollFrame.RefreshRange();UpdateView();
  }
  void SetupText(){detail.Multiline=true;detail.ReadOnly=true;detail.ScrollBars=RichTextBoxScrollBars.None;detail.WordWrap=false;detail.Dock=DockStyle.Fill;detail.BackColor=card;detail.ForeColor=Color.White;detail.BorderStyle=BorderStyle.None;detail.Font=new Font("Consolas",11);detail.Text="";}
  string OverviewText(string key){string value;if(key=="CPU"&&!compact){var temperature=sensorClient.Snapshot.Sensors.FirstOrDefault(v=>v.Id.Contains("cpu")&&v.Type=="Temperature"&&v.Value.HasValue);var clock=sensorClient.Snapshot.Sensors.FirstOrDefault(v=>v.Id.Contains("cpu")&&v.Type=="Clock"&&v.Value.HasValue&&v.Value.Value>0);value=string.Join("\r\n",sources["CPU"].Value.Split('\n').Take(2).ToArray())+"\r\nTemperature: "+(temperature==null?"Unavailable":temperature.Value.Value.ToString("0.#")+" C")+"\r\nClock: "+(clock==null?"Unavailable":clock.Value.Value.ToString("0")+" MHz");}else if(key=="GPU"&&compact){value=string.Join(" | ",sources["GPU"].Value.Replace("\r","").Split('\n').Where(l=>l.StartsWith("Load")||l.StartsWith("Temperature")).Select(l=>Regex.Replace(l," +"," ")).ToArray());}else if(key=="Storage"){value=string.Join("\r\n",sources["Storage"].Value.Split('\n').Where(l=>l.StartsWith("Disk #")).Select(l=>{var parts=l.Split('|');return parts.Length>=4?parts[0].Trim()+" "+parts[1].Trim()+" | "+parts[3].Trim():l;}).ToArray());if(value=="")value=sources["Storage"].Display;}else if(key=="Memory")value=string.Join("\r\n",sources["CPU"].Value.Split('\n').Where(l=>l.StartsWith("RAM")))+"\r\nModule information and frequency in Memory";else if(key=="Sensors")value="Live sensor tree: "+sensorClient.Snapshot.Sensors.Count+" sensors\r\n"+(sensorClient.Snapshot.Error??"Current, minimum, maximum and history");else value=sources[key].Value;var lines=value.Replace("\r","").Split('\n').Where(l=>!string.IsNullOrWhiteSpace(l)).Take(compact?1:4).Select(l=>l.Length>64?l.Substring(0,61)+"...":l);return string.Join("\r\n",lines.ToArray());}
  void Combo(FlowLayoutPanel p,string title,DarkCombo c,string[] values){var group=new Panel{Width=c==disks||c==gpuSelect?430:155,Height=66,Margin=new Padding(0,0,14,10)};var label=Theme.Label(title,9);label.Dock=DockStyle.Top;label.Height=23;c.AccessibleName=title;c.DropDownStyle=ComboBoxStyle.DropDownList;c.Dock=DockStyle.Bottom;c.Items.Clear();c.Items.AddRange(values);if(c.Items.Count>0)c.SelectedIndex=0;Theme.Combo(c);group.Controls.Add(c);group.Controls.Add(label);p.Controls.Add(group);}
  void BuildBenchmark(){
   bool gpu=page=="GPU Stress Test",copy=page=="Copy Benchmark";
   var basic=new FlowLayoutPanel{Dock=DockStyle.Top,Height=80,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,AutoScroll=false};
   var advanced=new FlowLayoutPanel{Dock=DockStyle.Top,Height=145,WrapContents=true,AutoScroll=true,Visible=false,Padding=new Padding(14),BackColor=Theme.Surface};
   if(!gpu){
    Combo(basic,"PHYSICAL DRIVE",disks,new string[0]);disks.DropDownWidth=1000;lastDiskData="";sources["Storage"].Request(true);
    Combo(basic,"PRESET",profile,new[]{"CDM Default","CDM NVMe","AS SSD inspired","Compression","Custom"});if(page=="AS SSD Benchmark")profile.SelectedIndex=2;if(page=="Compression Benchmark")profile.SelectedIndex=3;profile.Enabled=page=="Disk Benchmark";
    Combo(basic,"TEST SIZE · MiB",size,new[]{"128","256","512","1024"});Combo(basic,"WORKLOAD",test,new[]{"Run All"});
    Combo(advanced,"SECONDS / PHASE",duration,new[]{"1","5","10","30"});Combo(advanced,"REPETITIONS",repeats,new[]{"1","3","5"});Combo(advanced,"CUSTOM QUEUE DEPTH",qd,new[]{"1","2","4","8","16","32"});Combo(advanced,"CUSTOM THREADS",threads,new[]{"1","2","4","8","16","32","64"});Combo(advanced,"CUSTOM BLOCK · KiB",block,new[]{"4","128","1024"});Combo(advanced,"CUSTOM ACCESS",pattern,new[]{"Random","Sequential"});
    if(copy){advanced.Visible=false;}profile.SelectedIndexChanged-=ProfileChanged;profile.SelectedIndexChanged+=ProfileChanged;ProfileChanged(null,EventArgs.Empty);
   }else{Combo(basic,"GRAPHICS ADAPTER",gpuSelect,new[]{sources["GPU"].Value.Split('\n').FirstOrDefault(l=>l.Contains("NVIDIA"))??"NVIDIA telemetry unavailable"});Combo(basic,"RENDER RESOLUTION",resolution,new[]{"1280x720","1920x1080","2560x1440","3840x2160"});Combo(basic,"DURATION · SECONDS",duration,new[]{"10","30","60","120","300","600","Until stopped"});Combo(basic,"DISPLAY MODE",windowMode,new[]{"Windowed","Fullscreen"});Combo(basic,"THERMAL CUTOFF · °C",limit,new[]{"70","75","80"});}
   var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=56,Padding=new Padding(0,5,0,10)};
   start.Text=gpu?"Start stress test":"Run benchmark";start.Size=new Size(165,38);start.FlatStyle=FlatStyle.Flat;start.FlatAppearance.BorderSize=0;start.BackColor=Theme.Primary;start.ForeColor=Theme.Text;start.Click-=StartClick;start.Click+=StartClick;
   cancel.Text="Stop";cancel.Size=new Size(95,38);cancel.FlatStyle=FlatStyle.Flat;cancel.BackColor=Theme.Raised;cancel.ForeColor=Theme.AccentSoft;cancel.FlatAppearance.BorderSize=0;cancel.Click-=StopClick;cancel.Click+=StopClick;actions.Controls.Add(start);actions.Controls.Add(cancel);
   if(!gpu&&!copy){var toggle=ButtonFor("Advanced settings");toggle.Click+=(sender,args)=>{advanced.Visible=!advanced.Visible;toggle.Text=advanced.Visible?"Hide advanced":"Advanced settings";};actions.Controls.Add(toggle);}
   var export=ButtonFor("Save report");export.Click+=async (sender,args)=>{using(var dialog=new SaveFileDialog{Filter="Readable report|*.txt",FileName="BenchMark-X-report.txt"})if(dialog.ShowDialog()==DialogResult.OK)try{string path=dialog.FileName,content=benchmarkText;await Task.Run(()=>File.WriteAllText(path,content));}catch(Exception error){MessageBox.Show(error.Message);}};actions.Controls.Add(export);
   if(!gpu){var scan=ButtonFor("Rescan drives");scan.Click+=(sender,args)=>sources["Storage"].Request(true);actions.Controls.Add(scan);}
   results=new BenchmarkResults(gpu);UpdateResultContext(null,EventArgs.Empty);disks.SelectedIndexChanged-=UpdateResultContext;disks.SelectedIndexChanged+=UpdateResultContext;foreach(var combo in new[]{size,duration,repeats}){combo.SelectedIndexChanged-=UpdateResultContext;combo.SelectedIndexChanged+=UpdateResultContext;}
   var resultSection=new SectionPanel(gpu?"Live workload results":copy?"File copy results":page=="Compression Benchmark"?"Measured compression results":"Results — "+(page=="AS SSD Benchmark"?"AS SSD":"Disk")){Dock=DockStyle.Fill};resultSection.Controls.Add(results);
   if(page=="Compression Benchmark"){var chart=new SectionPanel("Measured throughput vs. zero content"){Dock=DockStyle.Bottom,Height=250};compressionGraph.Dock=DockStyle.Fill;compressionGraph.BackColor=Theme.Surface;chart.Controls.Add(compressionGraph);resultSection.Controls.Add(chart);}if(gpu){var chart=new SectionPanel("Temperature history"){Dock=DockStyle.Bottom,Height=245};stressGraph.Dock=DockStyle.Fill;stressGraph.BackColor=Theme.Surface;chart.Controls.Add(stressGraph);resultSection.Controls.Add(chart);}
   var configuration=new SectionPanel(gpu?"Workload configuration":copy?"Copy settings":page=="Compression Benchmark"?"Workload controls":"Disk test settings"){Dock=DockStyle.Left,Width=365};basic.AutoSize=false;basic.Height=gpu?365:300;basic.WrapContents=true;foreach(Control group in basic.Controls){group.Width=325;group.Margin=new Padding(0,0,0,8);}advanced.Height=235;advanced.Padding=new Padding(0,4,0,4);foreach(Control group in advanced.Controls){group.Width=305;group.Margin=new Padding(0,0,0,6);}actions.AutoSize=false;actions.Height=gpu?125:165;actions.WrapContents=true;foreach(Control button in actions.Controls){button.Width=150;button.Height=40;button.Margin=new Padding(0,0,9,8);}configuration.Controls.Add(advanced);configuration.Controls.Add(actions);configuration.Controls.Add(basic);if(gpu){var safety=Theme.Label("Temperature cutoff, telemetry-loss detection, Stop and Escape remain active in every duration mode. Rendering starts only after you confirm a test.",11);safety.Dock=DockStyle.Bottom;safety.Height=110;configuration.Controls.Add(safety);}
   body.Controls.Add(resultSection);body.Controls.Add(configuration);progress.Dock=DockStyle.Bottom;body.Controls.Add(progress);individualTests.Visible=false;
  }
  void ProfileChanged(object sender,EventArgs e){
   if(page=="GPU Stress Test")return;string[] names=page=="Copy Benchmark"?new[]{"ISO","Program","Game"}:profile.SelectedIndex==0?new[]{"SEQ1M Q8T1","SEQ1M Q1T1","RND4K Q32T1","RND4K Q1T1"}:profile.SelectedIndex==1?new[]{"SEQ1M Q8T1","SEQ128K Q32T1","RND4K Q32T16","RND4K Q1T1"}:profile.SelectedIndex==2?new[]{"Sequential","4K Q1T1","4K-64Thrd","Access time 512B"}:profile.SelectedIndex==3?new string[0]:new[]{"Custom"};
   foreach(Control old in individualTests.Controls.Cast<Control>().ToArray())old.Dispose();individualTests.Controls.Clear();test.Items.Clear();test.Items.Add(names.Length==0?"All compression points":"Run All");foreach(string name in names)test.Items.Add(name);test.SelectedIndex=0;for(int i=0;i<names.Length;i++){int index=i;var button=ButtonFor(names[i]);button.Width=180;button.Click+=(origin,eventArgs)=>{if(running)return;test.SelectedIndex=index+1;StartClick(origin,eventArgs);};individualTests.Controls.Add(button);}individualTests.Visible=names.Length>0;UpdateResultContext(null,EventArgs.Empty);
  }
  void UpdateView(){
   sensorClient.Poll();sensorClient.Record();sources["CPU"].Request(false);sources["GPU"].Request(false);
   if((DateTime.UtcNow-lastSample).TotalSeconds>=3){lastSample=DateTime.UtcNow;AddTrend(cpuHistory,ReadMetric(sources["CPU"].Value,"CPU load"));AddTrend(gpuHistory,ReadMetric(sources["GPU"].Value,"Load"));foreach(var c in charts)c.Invalidate();}
   if(sources.ContainsKey(page)&&page!="CPU"&&page!="GPU")sources[page].Request(false);
   if(page=="Overview"){sources["Identity"].Request(false);sources["Capacity"].Request(false);sources["Storage"].Request(false);sources["Network"].Request(false);sources["Inventory"].Request(false);sources["Sensors"].Value=sensorClient.Text(null);UpdateCards();}
   else if(new[]{"Sensors","Reports","Settings","Drive Health","CPU","GPU","Memory","Storage","Network","Inventory"}.Contains(page)){}
   else if(page=="Memory"){sources["Inventory"].Request(false);SetDetail(sources["CPU"].Display+"\r\n"+sensorClient.Text("ram")+"\r\n"+sources["Inventory"].Display);}
   else if(sources.ContainsKey(page)){string value=sources[page].Display;if(page=="CPU"){sources["Core load"].Request(false);value+="\r\n"+sources["Core load"].Display+"\r\n"+sensorClient.Text("cpu");}if(page=="GPU")value+="\r\n"+sensorClient.Text("gpu");if(page=="Storage"){sources["Disk activity"].Request(false);value+="\r\n"+sources["Disk activity"].Display+"\r\n"+sensorClient.Text("storage");}SetDetail(value);}
   else if(page=="Logs"){SetDetail(Workers.RecentLogs);}
   else if(page=="Reports"){if(!reportsBusy&&DateTime.UtcNow>=nextReports){reportsBusy=true;Task.Run(()=>{try{reportCache=Workers.ReportHistory();}catch(Exception e){reportCache=e.Message;}finally{nextReports=DateTime.UtcNow.AddSeconds(5);reportsBusy=false;}});}SetDetail(reportCache);}
   else {
    if(page!="GPU Stress Test"){
     sources["Storage"].Request(false);
     string raw=sources["Storage"].Value;
     if(raw!=lastDiskData){lastDiskData=raw;string old=disks.Text;disks.Items.Clear();foreach(string line in raw.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries))if(line.StartsWith("Disk #")&&line.Contains("|"))disks.Items.Add(line.Trim());if(disks.Items.Contains(old))disks.SelectedItem=old;else if(disks.Items.Count>0)disks.SelectedIndex=0;}
    }
    progress.Visible=running;if(page=="GPU Stress Test"&&benchmarkText!=lastStressSample){lastStressSample=benchmarkText;float temp=ReadMetric(benchmarkText.Replace(" C", ""),"GPU temperature:");if(temp>=0){stressTemperature.Enqueue(temp);while(stressTemperature.Count>300)stressTemperature.Dequeue();}stressGraph.Title="GPU temperature (C)";stressGraph.Values=stressTemperature.ToArray();stressGraph.Invalidate();}if(page=="Compression Benchmark"){var points=Regex.Matches(benchmarkText,@"([0-9]+)% zero data WRITE ([0-9.,]+) MB/s").Cast<Match>().Select(m=>float.Parse(m.Groups[2].Value)).ToArray();compressionGraph.Title="Write throughput (MB/s) across 0, 25, 50, 75, 100% zero data";compressionGraph.Values=points;compressionGraph.Invalidate();}
    foreach(Control button in individualTests.Controls)button.Enabled=!running&&disks.SelectedItem!=null;start.Enabled=!running&&(page=="GPU Stress Test"||disks.SelectedItem!=null);cancel.Enabled=running;SetDetail(page!="GPU Stress Test"&&disks.SelectedItem==null?benchmarkText+"\r\n\r\nPhysical disk discovery: "+sources["Storage"].Display:benchmarkText);
   }
   status.Text=running?"WORKLOAD RUNNING   /   Monitoring remains available":"LIVE MONITORING   /   "+DateTime.Now.ToString("HH:mm:ss")+"   /   "+Branding.Name+" "+Branding.Version;
  }
  void SetDetail(string s){if(s==lastDetail)return;lastDetail=s;if(results!=null&&!results.IsDisposed)results.UpdateReport(s,running);else detail.Text=s;}
  void StartClick(object sender,EventArgs e){
   if(running)return;
   string kind=page=="GPU Stress Test"?"stress":page=="Copy Benchmark"?"copy":"diskbench";
   string args;
   if(kind=="stress"){
    if(MessageBox.Show("Run "+(duration.Text=="Until stopped"?"until you stop the test":duration.Text+" seconds")+" at "+resolution.Text+" ("+windowMode.Text+")? Stay at your PC. Stops at "+limit.Text+"°C or loss of NVIDIA telemetry.","GPU Stress Test",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
    args=GpuStress.DurationChoice(duration.Text)+" "+resolution.Text.Replace("x"," ")+" "+limit.Text+" "+windowMode.SelectedIndex;
   }else{
    if(disks.SelectedItem==null)return;
    string disk=disks.Text.Split('|')[0].Replace("Disk #","").Trim();
    int num;if(!int.TryParse(disk,out num))return;
    if(MessageBox.Show("Run "+profile.Text+" / "+test.Text+" on "+disks.Text+"?\r\nTemporary file: "+size.Text+" MiB; "+duration.Text+" seconds per phase; "+repeats.Text+" repetitions plus warm-up. Both reads and writes run. Copy tests run until "+size.Text+" MiB per scenario completes. Storage temperature is checked between phases with a 70 C guard where telemetry is available. This consumes SSD write endurance.","Storage benchmark",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
    args=num+" "+size.Text+" "+(kind=="diskbench"?profile.SelectedIndex+" "+duration.Text+" "+repeats.Text+" "+qd.Text+" "+threads.Text+" "+block.Text+" "+(pattern.SelectedIndex==0?"1":"0"):"0 5 1 1 1 4 1")+" "+Convert.ToBase64String(Encoding.UTF8.GetBytes(disks.Text))+" "+(test.SelectedIndex-1);
   }
   string hardware=kind=="stress"?sources["GPU"].Value:disks.Text;hardware+="\r\n"+sources["CPU"].Value+"\r\n"+sources["GPU"].Value+"\r\n"+sources["Inventory"].Value;string configuration=page+" | "+args;
   if(results!=null&&!results.IsDisposed){results.ResetForRun();UpdateResultContext(null,EventArgs.Empty);}stressTemperature.Clear();running=true;cancelRequested=false;benchmarkText="Starting…";
   Task.Run(()=>{
    string completionStatus="Failed";string output=Path.Combine(Path.GetTempPath(),"overclock-job-"+Guid.NewGuid().ToString("N")+".txt");
    try{
     using(var p=Workers.StartSelf(kind,output,args)){
      active=p;
      var elapsed=Stopwatch.StartNew(); DateTime? cancelAt=null;
      while(!p.WaitForExit(400)){
       if(cancelRequested&&!cancelAt.HasValue){File.WriteAllText(output+".cancel","stop");cancelAt=DateTime.UtcNow;}
       bool stale=kind=="stress"&&elapsed.Elapsed.TotalSeconds>20&&(!File.Exists(output)||(DateTime.UtcNow-File.GetLastWriteTimeUtc(output)).TotalSeconds>15);
       if((kind!="stress"&&elapsed.Elapsed.TotalSeconds>1800)||stale||(cancelAt.HasValue&&(DateTime.UtcNow-cancelAt.Value).TotalSeconds>12)||(kind=="stress"&&GpuStress.ParentDeadline(int.Parse(args.Split(' ')[0]),elapsed.Elapsed.TotalSeconds))){Workers.KillTree(p);break;}
       ReadProgress(output);
      }
      ReadProgress(output);
      completionStatus=cancelRequested?"Cancelled":p.ExitCode!=0?"Failed":benchmarkText.TrimEnd().EndsWith("Completed.")||benchmarkText.TrimEnd().EndsWith("Completed")?"Completed":benchmarkText.TrimEnd().EndsWith("Stopped")?"Stopped":"Failed";
      if(cancelRequested)benchmarkText+="\r\nCancelled.";
      else if(p.ExitCode!=0)benchmarkText+="\r\nWorker failed.";
     }
    }catch(Exception ex){benchmarkText="Error: "+ex.Message;Workers.Log(benchmarkText);}
    finally{
     try{string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OverclockNative","Reports");Directory.CreateDirectory(folder);ReportStore.Save(ReportStore.Create(kind,configuration,hardware,benchmarkText,completionStatus));}catch(Exception saveError){benchmarkText+="\r\nReport could not be saved: "+saveError.Message;Workers.Log(saveError.Message);}
     active=null;running=false;try{File.Delete(output);File.Delete(output+".cancel");}catch{}
    }
   });
  }
  void BuildSettings(){var layout=new TableLayoutPanel{Dock=DockStyle.Top,Height=430,ColumnCount=2,RowCount=1};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));var monitoring=new SectionPanel("Monitoring"){Dock=DockStyle.Fill,Margin=new Padding(0,0,14,0)};var application=new SectionPanel("Application"){Dock=DockStyle.Fill,Margin=Padding.Empty};var left=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};var right=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};var label=Theme.Label("Sensor update interval",14,Theme.Text);label.Size=new Size(350,44);left.Controls.Add(label);var interval=new DarkCombo{Width=250,DropDownStyle=ComboBoxStyle.DropDownList};for(int i=1;i<=10;i++)interval.Items.Add(i+" second"+(i==1?"":"s"));interval.SelectedIndex=Math.Max(0,Math.Min(9,settings.SensorInterval/1000-1));Theme.Combo(interval);left.Controls.Add(interval);var intervalHelp=Theme.Label("Choose how often supported hardware sensors are sampled (1–10 seconds).",12);intervalHelp.Size=new Size(400,90);intervalHelp.Margin=new Padding(0,20,0,10);left.Controls.Add(intervalHelp);var top=new CheckBox{Text="Always on top",Checked=settings.TopMost,Width=330,Height=44,ForeColor=Theme.Text,FlatStyle=FlatStyle.Flat};right.Controls.Add(top);var note=Theme.Label("Windows administrator permission and the installed PawnIO driver provide access to supported low-level sensors. Unsupported readings remain unavailable. GPU stress supports one NVIDIA adapter.",12);note.Size=new Size(420,150);note.Margin=new Padding(0,20,0,0);right.Controls.Add(note);var save=Theme.Button("Save preferences",true);save.Width=220;save.Click+=async (s,e)=>{settings.SensorInterval=(interval.SelectedIndex+1)*1000;settings.TopMost=top.Checked;try{save.Enabled=false;await Task.Run(()=>settings.Save());if(IsDisposed)return;sensorClient.Interval=settings.SensorInterval;TopMost=settings.TopMost;if(!save.IsDisposed)save.Text="Saved";}catch(Exception ex){if(!IsDisposed)MessageBox.Show(ex.Message);}finally{if(!save.IsDisposed)save.Enabled=true;}};left.Controls.Add(save);monitoring.Controls.Add(left);application.Controls.Add(right);layout.Controls.Add(monitoring);layout.Controls.Add(application);body.Controls.Add(layout);body.Controls.Add(new IdentityBanner{Caption="Settings",Model="Monitoring and application preferences",Summary="Your existing settings and report history are retained.",Height=145});}
  static IEnumerable<Control> Descendants(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control child in Descendants(c))yield return child;}}
  public void BeginUiTest(string folder){
   Directory.CreateDirectory(folder);var log=new StringBuilder();if(tray==null||!tray.Visible||Icon==null||Text!=(Branding.Name+" "+Branding.Version))throw new Exception("Branding or tray icon unavailable");log.AppendLine("PASS BenchMark X branding, window icon and tray registration");string[] pages={"Overview","CPU","GPU","Memory","Storage","Drive Health","Sensors","Inventory","Network","Logs","Disk Benchmark","AS SSD Benchmark","Copy Benchmark","Compression Benchmark","GPU Stress Test","Reports","RGB Studio","Display Studio","Settings","Mini monitor","Expand monitor","Overview"};int index=0;double longest=0;var tester=new System.Windows.Forms.Timer{Interval=750};tester.Tick+=async (s,e)=>{tester.Stop();try{if(index<pages.Length){var watch=Stopwatch.StartNew();if(pages[index]=="Mini monitor"||pages[index]=="Expand monitor")ToggleMini();else Navigate(pages[index]);watch.Stop();longest=Math.Max(longest,watch.Elapsed.TotalMilliseconds);if(start.IsDisposed||detail.IsDisposed||disks.IsDisposed)throw new Exception("Shared control disposed");if(pages[index]=="CPU"||pages[index]=="GPU")await Task.Delay(1800);if(pages[index]=="Mini monitor")await Task.Delay(400);if(pages[index]=="Network")await Task.Delay(6000);if(pages[index]=="Drive Health")await Task.Delay(2500);if(pages[index]=="Reports")await Task.Delay(500);if(pages[index]=="Sensors"){var sensorTable=Descendants(body).OfType<DataGridView>().First();if(sensorTable.Rows.Count>5){var chosen=sensorTable.Rows[4].Tag as string;sensorTable.ClearSelection();sensorTable.Rows[4].Selected=true;await Task.Delay(2200);if(sensorTable.SelectedRows.Count==0||sensorTable.SelectedRows[0].Tag as string!=chosen)throw new Exception("Sensor selection changed during refresh");log.AppendLine("PASS sensor selection and history target preserved through live refresh");}}if(index==pages.Length-1)await Task.Delay(1200);DarkScrolling.Audit(this);log.AppendLine("PASS navigate and native-scrollbar audit "+pages[index]);Action<string> capture=name=>{TopMost=true;BringToFront();Activate();Refresh();Application.DoEvents();using(var bitmap=new Bitmap(Width,Height)){using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(Location,Point.Empty,Size);bitmap.Save(Path.Combine(folder,"ui-"+name+".png"));}};capture(pages[index].Replace(" ","-"));if(pages[index]=="GPU Stress Test"){duration.SelectedItem="Until stopped";if(GpuStress.DurationChoice(duration.Text)!=0)throw new Exception("Unlimited UI option unavailable");capture("GPU-Unlimited");duration.SelectedItem="10";log.AppendLine("PASS unlimited duration UI selection without starting workload");}if(pages[index]=="Reports"){var list=Descendants(body).OfType<ListBox>().First();var completed=Enumerable.Range(0,list.Items.Count).Where(i=>((RunRecord)list.Items[i]).Status=="Completed").Take(2).ToArray();if(completed.Length>0){list.ClearSelected();foreach(int i in completed)list.SetSelected(i,true);capture("Reports-Compare");log.AppendLine("PASS actual saved report selection and comparison");}}if(pages[index]=="Disk Benchmark"){var advanced=Descendants(body).OfType<Button>().FirstOrDefault(b=>b.Text=="Advanced settings");if(advanced==null)throw new Exception("Advanced settings control missing");advanced.PerformClick();if(!qd.Visible||!threads.Visible)throw new Exception("Advanced settings failed to open");capture("Disk-Advanced");var saved=ReportStore.Load().FirstOrDefault(r=>r.Kind=="diskbench"&&r.Status=="Completed");if(saved!=null){results.ShowRecord(saved);results.ExportPng(Path.Combine(folder,"shareable-ssd-results.png"));capture("Disk-Saved-Results");results.UpdateReport(benchmarkText,running);}profile.SelectedIndex=4;if(test.Items.Count!=2)throw new Exception("Custom workload selector failed");advanced.PerformClick();if(qd.Visible)throw new Exception("Advanced settings failed to close");log.AppendLine("PASS advanced settings expand/collapse and custom workload selection");}if(index==pages.Length-1){WindowState=FormWindowState.Normal;ClientSize=new Size(1586,992);PerformLayout();foreach(var frame in Descendants(this).OfType<DarkScrollFrame>())frame.RefreshRange();if(ClientSize.Width!=1586||ClientSize.Height!=992)throw new Exception("Reference layout size was not applied: "+Size);capture("Overview-Reference");Size=new Size(1280,720);PerformLayout();if(Width!=1280||Height!=720)throw new Exception("1280x720 layout size was not applied: "+Size);capture("Overview-1280x720");var dashboard=Descendants(body).OfType<OverviewGrid>().First();DarkScrolling.ScrollTo(dashboard,0,int.MaxValue);PerformLayout();capture("Overview-1280x720-Scrolled");DarkScrolling.ScrollTo(dashboard,0,0);Size=new Size(900,720);PerformLayout();capture("Overview-Narrow");Size=new Size(1100,760);PerformLayout();capture("Overview-Small");Navigate("Disk Benchmark");PerformLayout();capture("Disk-Small");}index++;tester.Start();}else{log.AppendLine("Longest synchronous navigation action: "+longest.ToString("0")+" ms");File.WriteAllText(Path.Combine(folder,"ui-results.txt"),log.ToString());tester.Dispose();Close();}}catch(Exception ex){log.AppendLine("FAIL "+ex);File.WriteAllText(Path.Combine(folder,"ui-results.txt"),log.ToString());tester.Dispose();Environment.ExitCode=1;Close();}};Shown+=async (s,e)=>{try{WindowState=FormWindowState.Minimized;await Task.Delay(200);if(Visible)throw new Exception("Minimize did not hide the window to tray");((ToolStripMenuItem)tray.ContextMenuStrip.Items[0]).PerformClick();await Task.Delay(100);if(!Visible||WindowState!=FormWindowState.Normal)throw new Exception("Tray restore failed");log.AppendLine("PASS minimize-to-tray and restore action");var stalled=new Collector("delay-test",1,1);var requestWatch=Stopwatch.StartNew();stalled.Request(true);requestWatch.Stop();if(requestWatch.ElapsedMilliseconds>250)throw new Exception("Collector request blocked UI");sources["Core load"].Request(true);sources["Identity"].Request(true);await Task.Delay(8000);if(stalled.Busy||string.IsNullOrEmpty(stalled.Error))throw new Exception("Stalled collector did not time out");log.AppendLine("PASS stalled provider timeout and nonblocking request");tester.Start();}catch(Exception error){log.AppendLine("FAIL "+error);File.WriteAllText(Path.Combine(folder,"ui-results.txt"),log.ToString());Environment.ExitCode=1;Close();}};
  }
  void ReadProgress(string path){try{benchmarkText=Workers.ReadShared(path);}catch{}}
  void StopClick(object sender,EventArgs e){CancelRun();}
  void CancelRun(){cancelRequested=true;status.Text="Stopping…";}
  static float ReadMetric(string text,string prefix){foreach(string l in text.Split('\n'))if(l.StartsWith(prefix)){float n;if(float.TryParse(l.Substring(prefix.Length).Trim().TrimEnd('%','\r'),out n))return n;}return -1;}
  static void AddTrend(Queue<float> q,float v){if(v<0)return;q.Enqueue(v);while(q.Count>60)q.Dequeue();}
  void ToggleMini(){if(!compact){normalSize=WindowState==FormWindowState.Normal?Size:RestoreBounds.Size;WindowState=FormWindowState.Normal;compact=true;pageHeader.Height=62;heading.Height=44;subtitle.Visible=false;status.Height=36;Navigate("Overview");navBar.Visible=false;MinimumSize=new Size(640,520);Size=new Size(860,610);TopMost=true;heading.Text="Monitor · expand";}else{compact=false;pageHeader.Height=106;heading.Height=60;subtitle.Visible=true;status.Height=58;navBar.Visible=true;MinimumSize=new Size(780,600);Size=normalSize;TopMost=settings.TopMost;Navigate("Overview");}}
  protected override void OnDoubleClick(EventArgs e){if(compact)ToggleMini();base.OnDoubleClick(e);}
 }
 sealed class Trend:Control{
  readonly Queue<float> values;public Trend(Queue<float> v){values=v;DoubleBuffered=true;BackColor=Theme.Surface;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);float[] v=values.ToArray();if(v.Length<2)return;using(var pen=new Pen(Theme.Accent,2)){for(int i=1;i<v.Length;i++)e.Graphics.DrawLine(pen,(i-1)*Width/(v.Length-1f),Height-2-Math.Min(100,v[i-1])*(Height-4)/100,i*Width/(v.Length-1f),Height-2-Math.Min(100,v[i])*(Height-4)/100);}}
 }
}













