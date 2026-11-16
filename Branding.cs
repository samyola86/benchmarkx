using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
namespace Overclock {
 static class Branding {
  public const string Name="BenchMark X",Version="1.7.0";
  public static readonly Bitmap Logo=ReadImage("BenchMarkX.Logo"),Symbol=ReadImage("BenchMarkX.Symbol");
  public static readonly Icon Icon=ReadIcon();public static readonly Rectangle LogoBounds=Content(Logo);
  static Bitmap ReadImage(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))using(var image=Image.FromStream(stream))return new Bitmap(image);}
  static Icon ReadIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BenchMarkX.Icon"))return new Icon(stream,32,32);}
  static Rectangle Content(Bitmap image){int left=image.Width,top=image.Height,right=0,bottom=0;for(int y=0;y<image.Height;y+=2)for(int x=0;x<image.Width;x+=2){var c=image.GetPixel(x,y);if(c.A>20&&Math.Max(c.R,Math.Max(c.G,c.B))>50){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}}return left<right?Rectangle.FromLTRB(Math.Max(0,left-4),Math.Max(0,top-4),Math.Min(image.Width,right+5),Math.Min(image.Height,bottom+5)):new Rectangle(0,0,image.Width,image.Height);}
  public static void DrawLogo(Graphics graphics,Rectangle area){float scale=Math.Min(area.Width/(float)LogoBounds.Width,area.Height/(float)LogoBounds.Height);var target=new RectangleF(area.X,area.Y+(area.Height-LogoBounds.Height*scale)/2,LogoBounds.Width*scale,LogoBounds.Height*scale);using(var attributes=new ImageAttributes()){attributes.SetColorKey(Color.FromArgb(0,0,0),Color.FromArgb(12,12,12));graphics.DrawImage(Logo,Rectangle.Round(target),LogoBounds.X,LogoBounds.Y,LogoBounds.Width,LogoBounds.Height,GraphicsUnit.Pixel,attributes);}}
 }
 sealed class BrandLogo:Control {public BrandLogo(){DoubleBuffered=true;AccessibleName=Branding.Name;BackColor=Theme.Background;}protected override void OnPaint(PaintEventArgs e){Branding.DrawLogo(e.Graphics,ClientRectangle);}}
 sealed partial class MainWindow {
  NotifyIcon tray;
  void InitializeBranding(){Icon=Branding.Icon;tray=new NotifyIcon{Icon=Branding.Icon,Text=Branding.Name,Visible=true};var menu=new ContextMenuStrip{Renderer=new DarkMenuRenderer(),ShowImageMargin=false,BackColor=Theme.Surface,ForeColor=Theme.Text};menu.Items.Add("Open BenchMark X",null,(s,e)=>RestoreFromTray());menu.Items.Add("Compact monitor",null,(s,e)=>{RestoreFromTray();if(!compact)ToggleMini();});menu.Items.Add("Exit",null,(s,e)=>{RestoreFromTray();Close();});tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>RestoreFromTray();Resize+=(s,e)=>{if(WindowState==FormWindowState.Minimized)Hide();};FormClosed+=(s,e)=>{tray.Visible=false;tray.Dispose();menu.Dispose();};}
  void RestoreFromTray(){Show();WindowState=FormWindowState.Normal;Activate();}
 }
 sealed class DarkMenuRenderer:ToolStripProfessionalRenderer {
  protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){using(var brush=new SolidBrush(Theme.Surface))e.Graphics.FillRectangle(brush,e.AffectedBounds);}
  protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){using(var brush=new SolidBrush(e.Item.Selected?Theme.SelectedSurface:Theme.Surface))e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));}
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=!e.Item.Enabled?Theme.Muted:e.Item.Selected?Theme.Accent:Theme.Text;base.OnRenderItemText(e);}
  protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using(var pen=new Pen(Theme.Line))e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}
 }
}
