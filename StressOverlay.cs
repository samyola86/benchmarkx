using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;

namespace Overclock {
 sealed class StressOverlayData {
  public string Telemetry="",Renderer="";public double Elapsed,Fps,MinFps=double.MaxValue,MaxFps,AverageFps,Temperature,PeakTemperature,TelemetryAge;public int Duration,Width,Height,Cutoff;public long Frames;public float[] TemperatureHistory=new float[0];
 }
 static class StressOverlay {
  public const int Width=620,Height=420;
  public static string Time(double seconds){var span=TimeSpan.FromSeconds(Math.Max(0,seconds));return ((long)span.TotalHours).ToString("00")+":"+span.Minutes.ToString("00")+":"+span.Seconds.ToString("00");}
  static string Field(string[] values,int index,string unit){return values.Length>index&&!string.IsNullOrWhiteSpace(values[index])&&values[index].IndexOf("N/A",StringComparison.OrdinalIgnoreCase)<0?values[index].Trim()+unit:"Unavailable";}
  public static Bitmap Draw(StressOverlayData data){
   var bitmap=new Bitmap(Width,Height,PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(bitmap))using(var title=new Font("Segoe UI",13,FontStyle.Bold))using(var text=new Font("Consolas",10))using(var big=new Font("Segoe UI",23,FontStyle.Bold)){
    g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    using(var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),12))using(var fill=new SolidBrush(Color.FromArgb(238,12,12,14)))g.FillPath(fill,path);
    Branding.DrawLogo(g,new Rectangle(18,6,235,32));
    var values=data.Telemetry.Split(',');string gpu=values.Length>0&&!string.IsNullOrWhiteSpace(values[0])?values[0].Trim():data.Renderer;
    g.DrawString(gpu,text,Brushes.White,18,43);
    using(var brush=new SolidBrush(Theme.Accent))g.DrawString(data.Fps.ToString("N1")+" FPS",big,brush,16,65);
    string extrema="MIN "+(data.MinFps==double.MaxValue?"â€”":data.MinFps.ToString("N1"))+"   MAX "+data.MaxFps.ToString("N1")+"   AVG "+data.AverageFps.ToString("N1");g.DrawString(extrema,text,Brushes.White,18,108);
    string duration=data.Duration==0?"UNTIL STOPPED":Time(data.Duration);g.DrawString("TIME "+Time(data.Elapsed)+" / "+duration+"   FRAMES "+data.Frames.ToString("N0"),text,Brushes.White,18,132);
    g.DrawString("RENDER "+data.Width+" Ã— "+data.Height+"   |   TEMP LIMIT "+data.Cutoff+" Â°C",text,Brushes.White,18,156);
    g.DrawString("GPU LOAD  "+Field(values,1," %")+"    TEMP  "+data.Temperature.ToString("0.0")+" Â°C    PEAK  "+data.PeakTemperature.ToString("0.0")+" Â°C",text,Brushes.White,18,184);
    g.DrawString("CORE "+Field(values,6," MHz")+"    MEMORY "+Field(values,8," MHz")+"    POWER "+Field(values,5," W"),text,Brushes.White,18,208);
    g.DrawString("VRAM "+Field(values,3," MiB")+" / "+Field(values,4," MiB")+"    FAN "+Field(values,7," %"),text,Brushes.White,18,232);
    using(var brush=new SolidBrush(data.TelemetryAge>3?Theme.Warning:Theme.Muted))g.DrawString("TELEMETRY AGE "+data.TelemetryAge.ToString("0.0")+" s   |   ESC / CLOSE / STOP TO END",text,brush,18,264);
    using(var brush=new SolidBrush(Theme.Muted))g.DrawString("Independent OpenGL workload Â· not a FurMark score",text,brush,18,296);
    using(var brush=new SolidBrush(Theme.Muted))g.DrawString("TEMPERATURE HISTORY  /  latest 600 samples",text,brush,18,326);
    using(var pen=new Pen(Theme.Line))for(int i=0;i<3;i++)g.DrawLine(pen,18,355+i*21,Width-18,355+i*21);
    if(data.TemperatureHistory.Length>1)using(var pen=new Pen(Theme.AccentSoft,2))for(int i=1;i<data.TemperatureHistory.Length;i++)g.DrawLine(pen,18+(i-1)*(Width-36f)/(data.TemperatureHistory.Length-1),397-Math.Max(0,Math.Min(data.Cutoff,data.TemperatureHistory[i-1]))/data.Cutoff*42,18+i*(Width-36f)/(data.TemperatureHistory.Length-1),397-Math.Max(0,Math.Min(data.Cutoff,data.TemperatureHistory[i]))/data.Cutoff*42);
   }return bitmap;
  }
 }
}

