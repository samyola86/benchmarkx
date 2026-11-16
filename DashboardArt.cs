using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace Overclock {
 public sealed class AdapterIdentity {public string Name,PnpId;}
 public sealed class HardwareIdentity {public string CpuVendor;public List<AdapterIdentity> Gpus=new List<AdapterIdentity>();}
 static class DashboardArt {
  static readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();
  public static void Load(){if(images.Count>0)return;foreach(string name in new[]{"cpu-amd","cpu-intel","gpu-nvidia","gpu-amd","gpu-intel","memory","storage","network-ethernet","network-wifi","sensors"}){
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BenchMarkX.Art."+name))using(var source=Image.FromStream(stream)){
    var image=new Bitmap(512,Math.Max(1,(int)(512f*source.Height/source.Width)),PixelFormat.Format32bppArgb);
    using(var g=Graphics.FromImage(image)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,0,0,image.Width,image.Height);}
    var pixels=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
    try{byte[] bytes=new byte[pixels.Stride*image.Height];Marshal.Copy(pixels.Scan0,bytes,0,bytes.Length);for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){int p=y*pixels.Stride+x*4;float light=Math.Max(bytes[p],Math.Max(bytes[p+1],bytes[p+2]));float edge=Math.Min(1,Math.Min(x/80f,(image.Height-y)/45f));bytes[p+3]=(byte)(bytes[p+3]*Math.Min(1,light/36)*edge);}Marshal.Copy(bytes,0,pixels.Scan0,bytes.Length);}finally{image.UnlockBits(pixels);}images[name]=image;Bounds(name);
   }
  }}
  static readonly Dictionary<string,Rectangle> bounds=new Dictionary<string,Rectangle>();
  public static Rectangle Bounds(string key){Rectangle value;if(bounds.TryGetValue(key,out value))return value;Bitmap image=Get(key);if(image==null)return new Rectangle(0,0,1,1);int left=image.Width,top=image.Height,right=0,bottom=0;for(int y=0;y<image.Height;y+=2)for(int x=0;x<image.Width;x+=2){var c=image.GetPixel(x,y);if(c.A>48&&Math.Max(c.R,Math.Max(c.G,c.B))>55){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}}value=left<right?Rectangle.FromLTRB(Math.Max(0,left-5),Math.Max(0,top-5),Math.Min(image.Width,right+6),Math.Min(image.Height,bottom+6)):new Rectangle(0,0,image.Width,image.Height);bounds[key]=value;return value;}
  public static Bitmap Get(string key){Bitmap value;return key!=null&&images.TryGetValue(key,out value)?value:null;}
  public static void Dispose(){foreach(var b in images.Values)b.Dispose();images.Clear();}
  public static string Cpu(string vendor){string v=(vendor??"").Trim().ToUpperInvariant();if(v=="AUTHENTICAMD"||v=="AMD"||v=="ADVANCED MICRO DEVICES, INC.")return "cpu-amd";if(v=="GENUINEINTEL"||v=="INTEL"||v=="GENUINEINTEL CORPORATION"||v=="INTEL CORPORATION")return "cpu-intel";return null;}
  public static string Gpu(string model,IEnumerable<AdapterIdentity> adapters){var matches=(adapters??new AdapterIdentity[0]).Where(a=>string.Equals((a.Name??"").Trim(),(model??"").Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length!=1)return null;string name=(model??"").ToUpperInvariant();if(Regex.IsMatch(name,@"UHD|IRIS|VANGOGH|VEGA|RADEON\s*(\(TM\))?\s*(GRAPHICS|[0-9]{3}M)|INTEL.*(HD GRAPHICS|ARC.*(130|140|[0-9]+V))"))return null;var m=Regex.Match((matches[0].PnpId??"").ToUpperInvariant(),@"VEN_(10DE|1002|8086)");if(!m.Success)return null;switch(m.Groups[1].Value){case "10DE":return "gpu-nvidia";case "1002":return name.Contains("RADEON RX")||name.Contains("RADEON PRO")?"gpu-amd":null;case "8086":return Regex.IsMatch(name,@"ARC.*[AB][0-9]{3}")?"gpu-intel":null;default:return null;}}
  public static string Network(string type,string state,bool physical=true){if(!physical)return null;if(!string.Equals(state,"Up",StringComparison.OrdinalIgnoreCase))return null;if(string.Equals(type,"Ethernet",StringComparison.OrdinalIgnoreCase))return "network-ethernet";if(string.Equals(type,"Wireless80211",StringComparison.OrdinalIgnoreCase))return "network-wifi";return null;}
  public static string Storage(StorageCapacity c){return c!=null&&c.Disks.Any(d=>string.Equals(d.MediaType,"SSD",StringComparison.OrdinalIgnoreCase))?"storage":null;}
 }
 sealed class OverviewGrid:FlowLayoutPanel {
  public bool Compact;bool layingOut;
  public OverviewGrid(bool compact){Compact=compact;Dock=DockStyle.Fill;AutoScroll=true;WrapContents=true;FlowDirection=FlowDirection.LeftToRight;DoubleBuffered=true;}
  protected override void OnLayout(LayoutEventArgs e){if(layingOut)return;layingOut=true;try{float s;using(var g=CreateGraphics())s=g.DpiX/96;int gap=(int)(16*s),available=Math.Max(1,(Parent==null?ClientSize.Width:Parent.ClientSize.Width)-4);int columns=Compact?(available>=600*s?3:1):available>=960*s?3:available>=620*s?2:1;int rows=Math.Max(1,(Controls.Count+columns-1)/columns);int h=Compact?Math.Max((int)(128*s),((Parent==null?ClientSize.Height:Parent.ClientSize.Height)-gap*(rows-1)-8)/rows):Math.Max((int)(360*s),((Parent==null?ClientSize.Height:Parent.ClientSize.Height)-gap*(rows-1)-8)/rows);int cell=(available-gap*(columns-1))/columns;for(int i=0;i<Controls.Count;i++){Controls[i].Margin=new Padding(0,0,i%columns==columns-1?0:gap,i/columns==rows-1?0:gap);Controls[i].Size=new Size(cell,h);}AutoScrollMinSize=Size.Empty;base.OnLayout(e);}finally{layingOut=false;}}
 }
 sealed class ChromeFlow:FlowLayoutPanel {
  public ChromeFlow(){DoubleBuffered=true;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;using(var p=new Pen(Theme.Line))g.DrawLine(p,Width-1,0,Width-1,Height);using(var b=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,Math.Max(0,Height-380),Math.Max(1,Width),380),Color.FromArgb(30,3,8),Color.FromArgb(130,5,20),45))g.FillPolygon(b,new[]{new Point(0,Height-30),new Point(0,Height-98),new Point(Width,Height-390),new Point(Width,Height-312)});using(var b=new SolidBrush(Theme.PrimaryHover))g.FillPolygon(b,new[]{new Point(0,Height),new Point(28,Height),new Point(Width,Height-260),new Point(Width,Height-292)});using(var p=new Pen(Theme.Accent,1))g.DrawLine(p,0,Height-98,Width,Height-390);}
 }
 sealed class ChromePanel:Panel {
  public ChromePanel(){DoubleBuffered=true;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;using(var p=new Pen(Theme.Line))g.DrawLine(p,0,Height-1,Width-1,Height-1);using(var p=new Pen(Theme.Accent,2))g.DrawLine(p,Math.Max(0,Width-240),Height-1,Width-12,Height-1);for(int i=0;i<2;i++)using(var b=new SolidBrush(i==0?Theme.Accent:Theme.Primary))g.FillPolygon(b,new[]{new Point(Width-50+i*20,0),new Point(Width-35+i*20,0),new Point(Width-60+i*20,26),new Point(Width-75+i*20,26)});}
 }
}