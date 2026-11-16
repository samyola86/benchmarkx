using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Overclock {
 public sealed class Metric {public string Workload,Unit;public double Value;}
 public sealed class RunRecord {public string Id,Kind,Settings,Hardware,Status,Report;public DateTime Utc;public List<Metric> Metrics=new List<Metric>();public override string ToString(){return Utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")+" | "+Kind+" | "+Status;}}
 static class ReportStore {
  public static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OverclockNative");
  public static RunRecord Create(string kind,string settings,string hardware,string text,string status){
   var r=new RunRecord{Id=Guid.NewGuid().ToString("N"),Utc=DateTime.UtcNow,Kind=kind,Settings=settings,Hardware=hardware,Report=text,Status=status};
   foreach(string line in text.Split('\n')){
    var m=Regex.Match(line,@"^(.*?)\s+([0-9]+(?:[.,][0-9]+)?) MB/s");double value;
    if(m.Success&&double.TryParse(m.Groups[2].Value,NumberStyles.Float,CultureInfo.CurrentCulture,out value)){r.Metrics.Add(new Metric{Workload=m.Groups[1].Value.Trim(),Value=value,Unit="MB/s"});string workload=m.Groups[1].Value.Trim();foreach(string unit in new[]{"IOPS","us"}){var extra=Regex.Match(line,@"([0-9]+(?:[.,][0-9]+)?) "+unit);double v;if(extra.Success&&double.TryParse(extra.Groups[1].Value,NumberStyles.Float,CultureInfo.CurrentCulture,out v))r.Metrics.Add(new Metric{Workload=workload,Value=v,Unit=unit});}continue;}
    m=Regex.Match(line,@"^(.*?FPS):\s+([0-9]+(?:[.,][0-9]+)?)");if(m.Success&&double.TryParse(m.Groups[2].Value,NumberStyles.Float,CultureInfo.CurrentCulture,out value))r.Metrics.Add(new Metric{Workload=m.Groups[1].Value.Trim(),Value=value,Unit="FPS"});
   }return r;
  }
  public static void Save(RunRecord r){string dir=Path.Combine(Root,"Reports");Directory.CreateDirectory(dir);MonitorService.Atomic(Path.Combine(dir,r.Utc.ToString("yyyyMMdd-HHmmss-fff")+"-"+r.Id+".json"),new JavaScriptSerializer().Serialize(r));}
  public static List<RunRecord> Load(){string dir=Path.Combine(Root,"Reports");var list=new List<RunRecord>();if(!Directory.Exists(dir))return list;foreach(string f in Directory.GetFiles(dir,"*.json")){try{list.Add(new JavaScriptSerializer().Deserialize<RunRecord>(File.ReadAllText(f)));}catch(Exception e){Workers.Log("Report "+Path.GetFileName(f)+": "+e.Message);}}return list.OrderByDescending(r=>r.Utc).ToList();}
  static string Csv(string s){return "\""+(s??"").Replace("\"","\"\"")+"\"";}
  public static string Export(IEnumerable<RunRecord> records,string format){var rows=records.ToList();if(format==".json")return new JavaScriptSerializer().Serialize(rows);var sb=new StringBuilder();if(format==".csv"){sb.AppendLine("UTC,Kind,Status,Hardware,Settings,Workload,Value,Unit");foreach(var r in rows)foreach(var m in r.Metrics)sb.AppendLine(string.Join(",",new[]{Csv(r.Utc.ToString("o")),Csv(r.Kind),Csv(r.Status),Csv(r.Hardware),Csv(r.Settings),Csv(m.Workload),m.Value.ToString("R",CultureInfo.InvariantCulture),Csv(m.Unit)}));}else foreach(var r in rows)sb.AppendLine(r+"\r\n"+r.Hardware+"\r\n"+r.Settings+"\r\n"+r.Report+"\r\n");return sb.ToString();}
 }
 sealed class ReportView:UserControl {
  readonly ListBox list=new ListBox{Dock=DockStyle.Left,Width=290,SelectionMode=SelectionMode.MultiExtended,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=76,BorderStyle=BorderStyle.None,BackColor=Theme.Surface,ForeColor=Theme.Text};readonly TextBox text=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Bottom,Height=160,Visible=false,BackColor=Theme.Surface,ForeColor=Theme.Muted,BorderStyle=BorderStyle.None,Font=new Font("Consolas",9)};readonly ComparisonGraph graph=new ComparisonGraph{Dock=DockStyle.Bottom,Height=220,BackColor=Theme.Surface};readonly DataGridView metrics=Theme.Grid("Workload","Measured value","Unit");
  public ReportView(){Dock=DockStyle.Fill;BackColor=Theme.Background;var right=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16,0,0,0)};right.Controls.Add(metrics);right.Controls.Add(graph);right.Controls.Add(text);var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(0,0,0,12)};var refresh=Theme.Button("Refresh");refresh.Click+=(s,e)=>Reload();buttons.Controls.Add(refresh);var export=Theme.Button("Export selected",true);export.Click+=(s,e)=>Export();buttons.Controls.Add(export);var share=Theme.Button("Share PNG");share.Click+=(s,e)=>{if(list.SelectedItems.Count==1){var record=(RunRecord)list.SelectedItem;if(record.Kind!="stress")DiskScoreboard.SaveRecord(record);else MessageBox.Show("Select a disk benchmark to share its scorecard.");}};buttons.Controls.Add(share);var technical=Theme.Button("Technical report");technical.Click+=(s,e)=>text.Visible=!text.Visible;buttons.Controls.Add(technical);var hint=Theme.Label("Ctrl + click to compare runs",9);hint.Size=new Size(230,38);hint.TextAlign=ContentAlignment.MiddleLeft;buttons.Controls.Add(hint);Controls.Add(right);Controls.Add(list);Controls.Add(buttons);list.DrawItem+=(s,e)=>{if(e.Index<0)return;var r=(RunRecord)list.Items[e.Index];using(var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Theme.Raised:Theme.Surface))e.Graphics.FillRectangle(brush,e.Bounds);string title=r.Kind=="stress"?"GPU stress test":r.Kind=="copy"?"Copy benchmark":"Disk benchmark";TextRenderer.DrawText(e.Graphics,title,Font,new Rectangle(e.Bounds.X+14,e.Bounds.Y+9,e.Bounds.Width-28,23),Theme.Text,TextFormatFlags.EndEllipsis);TextRenderer.DrawText(e.Graphics,r.Utc.ToLocalTime().ToString("dd MMM · HH:mm")+"   /   "+r.Status,Font,new Rectangle(e.Bounds.X+14,e.Bounds.Y+37,e.Bounds.Width-28,22),r.Status=="Completed"?Theme.Muted:Theme.Warning,TextFormatFlags.EndEllipsis);};list.SelectedIndexChanged+=(s,e)=>{var records=list.SelectedItems.Cast<RunRecord>().ToList();share.Enabled=records.Count==1&&records[0].Kind!="stress";text.Text=ReportStore.Export(records,".txt");metrics.Rows.Clear();foreach(var r in records)foreach(var m in r.Metrics)metrics.Rows.Add((records.Count>1?r.Utc.ToLocalTime().ToString("HH:mm")+" · ":"")+m.Workload,m.Value.ToString("N2"),m.Unit);metrics.ClearSelection();graph.Records=records;graph.Invalidate();};Reload();}
  async void Reload(){try{var records=await System.Threading.Tasks.Task.Run(()=>ReportStore.Load());if(IsDisposed)return;list.Items.Clear();list.Items.AddRange(records.Cast<object>().ToArray());if(list.Items.Count>0)list.SelectedIndex=0;else metrics.Rows.Add("No saved runs yet","Run a benchmark to start your history","");}catch(Exception e){if(!IsDisposed)metrics.Rows.Add("History unavailable",e.Message,"");}}
  void Export(){if(list.SelectedItems.Count==0)return;using(var d=new SaveFileDialog{Filter="JSON|*.json|CSV|*.csv|Readable report|*.txt",FileName="BenchMark-X-results.json"})if(d.ShowDialog()==DialogResult.OK)try{File.WriteAllText(d.FileName,ReportStore.Export(list.SelectedItems.Cast<RunRecord>(),Path.GetExtension(d.FileName).ToLowerInvariant()),Encoding.UTF8);}catch(Exception e){MessageBox.Show(e.Message,"Export failed");}}
 }
 sealed class ComparisonGraph:Control {
  public List<RunRecord> Records=new List<RunRecord>();public ComparisonGraph(){DoubleBuffered=true;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var rows=Records.Where(r=>r.Status=="Completed").ToList();if(rows.Count==0){e.Graphics.DrawString("Select completed runs to compare matching workloads (MB/s or FPS).",Font,Brushes.White,8,8);return;}string unit=rows.SelectMany(r=>r.Metrics).Any(m=>m.Unit=="MB/s")?"MB/s":"FPS";var keys=rows.SelectMany(r=>r.Metrics).Where(m=>m.Unit==unit).Select(m=>m.Workload).Distinct().Take(8).ToList();double max=rows.SelectMany(r=>r.Metrics).Where(m=>m.Unit==unit).Select(m=>m.Value).DefaultIfEmpty(1).Max();if(max<=0)max=1;int y=8;Color[] colors={Theme.Accent,Theme.Secondary,Theme.AccentSoft,Theme.Text};for(int i=0;i<rows.Count&&i<4;i++){using(var legendBrush=new SolidBrush(colors[i]))e.Graphics.DrawString(rows[i].Utc.ToLocalTime().ToString("MM-dd HH:mm:ss"),Font,legendBrush,8+i*150,y);}y+=25;foreach(string key in keys){e.Graphics.DrawString(key,Font,Brushes.White,8,y);for(int i=0;i<rows.Count&&i<4;i++){var m=rows[i].Metrics.FirstOrDefault(v=>v.Workload==key&&v.Unit==unit);if(m==null)continue;using(var b=new SolidBrush(colors[i]))e.Graphics.FillRectangle(b,190,y+i*7,(float)(m.Value/max*Math.Max(1,Width-290)),5);}y+=Math.Max(23,rows.Count*7+4);}}
 }
 sealed class AppSettings {
  public int SensorInterval=2000;public bool TopMost;public static AppSettings Load(){try{return new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(Path.Combine(ReportStore.Root,"settings.json")));}catch{return new AppSettings();}}
  public void Save(){Directory.CreateDirectory(ReportStore.Root);MonitorService.Atomic(Path.Combine(ReportStore.Root,"settings.json"),new JavaScriptSerializer().Serialize(this));}
 }
}





