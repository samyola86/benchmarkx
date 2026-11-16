using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;

namespace Overclock {
 static class GpuStress {
  public static int DurationChoice(string value){return value=="Until stopped"?0:int.Parse(value,CultureInfo.InvariantCulture);}
  public static bool DurationExpired(int seconds,double elapsed){return seconds>0&&elapsed>=seconds;}
  public static bool ParentDeadline(int seconds,double elapsed){return seconds>0&&elapsed>seconds+25;}
  public static bool ValidSettings(int duration,int width,int height,int limit){return (duration==0||(duration>=10&&duration<=600))&&width>=1280&&width<=3840&&height>=720&&height<=2160&&limit>=70&&limit<=80;}
  public static void Run(string output,int duration,int width,int height,int limit,bool fullscreen){
   if(!ValidSettings(duration,width,height,limit))throw new Exception("Invalid stress settings");
   double temp=Workers.NvidiaTemperature();if(temp>=limit-5)throw new Exception("GPU is too hot to start");
   using(var view=new StressWindow(output,duration,temp,width,height,limit,fullscreen))Application.Run(view);
  }
  sealed class StressWindow:Form {
   readonly string output;readonly int seconds,thermalLimit,renderWidth,renderHeight;uint framebuffer,texture; double currentFps,minFps=double.MaxValue,maxFps,lastFpsAt,currentTemp;long lastFrames;uint overlayTexture;double lastOverlay=-1,lastCaptureCheck=-1;readonly string historyPath;StreamWriter history;volatile string gpuTelemetry="";readonly Queue<string> samples=new Queue<string>();readonly Stopwatch clock=new Stopwatch();
   readonly Timer timer=new Timer();IntPtr dc,rc;uint program;int timeLocation,resLocation;long frames;
   volatile bool stop;volatile string fault="";double peak;long lastTelemetry;
   bool initialized;string renderer="";double lastReport;
   public StressWindow(string path,int duration,double initial,int width,int height,int limit,bool fullscreen){
    historyPath=Path.Combine(ReportStore.Root,"Telemetry","GPU-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".csv");output=path;renderWidth=width;renderHeight=height;currentTemp=initial;seconds=duration;thermalLimit=limit;peak=initial;Icon=Branding.Icon;Text="BenchMark X GPU stress — Esc to stop";ClientSize=new Size(width,height);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;if(fullscreen){FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;}
    SetStyle(ControlStyles.Opaque|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint,true);
    KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape)Close();};
    Shown+=(s,e)=>{try{Initialize();}catch(Exception ex){fault=ex.Message;Close();}};
    timer.Interval=1;timer.Tick+=(s,e)=>SafeRender();
   }
   void Initialize(){
    dc=GetDC(Handle);var pfd=new PFD{Size=(ushort)Marshal.SizeOf(typeof(PFD)),Version=1,Flags=0x25,PixelType=0,ColorBits=32,DepthBits=24,LayerType=0};
    int format=ChoosePixelFormat(dc,ref pfd);if(format==0||!SetPixelFormat(dc,format,ref pfd))throw new Exception("OpenGL pixel format failed");
    rc=wglCreateContext(dc);if(rc==IntPtr.Zero||!wglMakeCurrent(dc,rc))throw new Exception("OpenGL context failed");
    renderer=Marshal.PtrToStringAnsi(glGetString(0x1F01))??"";
    string vendor=Marshal.PtrToStringAnsi(glGetString(0x1F00))??"";
    if(vendor.IndexOf("NVIDIA",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Stress renderer is not the monitored NVIDIA adapter: "+renderer);
    uint shader=Load<CreateShader>("glCreateShader")(0x8B30);
    string source=@"#version 120
uniform float uTime;
uniform vec2 uRes;
float torus(vec3 p) {
 float a=uTime*0.65; p.xz=mat2(cos(a),-sin(a),sin(a),cos(a))*p.xz;
 p.yz=mat2(0.82,-0.57,0.57,0.82)*p.yz;
 return length(vec2(length(p.xz)-1.0,p.y))-0.36;
}
void main(){
 vec2 uv=(gl_FragCoord.xy*2.0-uRes)/uRes.y;
 vec3 ro=vec3(0.,0.,3.7),rd=normalize(vec3(uv,-2.0));
 float t=0.;vec3 p=ro;
 for(int i=0;i<128;i++){p=ro+rd*t;float d=torus(p);if(abs(d)<0.001||t>8.)break;t+=max(d*0.65,0.001);}
 vec3 col=vec3(0.012,0.010,0.012);
 if(t<8.){
 vec3 n=normalize(vec3(torus(p+vec3(.003,0.,0.))-torus(p-vec3(.003,0.,0.)),torus(p+vec3(0.,.003,0.))-torus(p-vec3(0.,.003,0.)),torus(p+vec3(0.,0.,.003))-torus(p-vec3(0.,0.,.003))));
 float fur=0.;for(int j=0;j<48;j++){float k=float(j)+1.;fur+=sin(k*19.3*p.x+cos(k*21.7*p.y)+p.z*13.1*k+uTime)*0.012;}
 float l=max(0.,dot(n,normalize(vec3(2.,3.,4.))));
 col=(vec3(.72,.12,.16)+fur)*(.25+.75*l)+pow(l,16.)*.3;
 }gl_FragColor=vec4(col,1.);
}";
    IntPtr str=Marshal.StringToHGlobalAnsi(source),ptr=Marshal.AllocHGlobal(IntPtr.Size);
    try{Marshal.WriteIntPtr(ptr,str);Load<ShaderSource>("glShaderSource")(shader,1,ptr,IntPtr.Zero);}finally{Marshal.FreeHGlobal(str);Marshal.FreeHGlobal(ptr);}
    Load<ShaderCommand>("glCompileShader")(shader);int ok;Load<GetStatus>("glGetShaderiv")(shader,0x8B81,out ok);if(ok==0)throw new Exception("GPU shader compilation failed");
    program=Load<CreateProgram>("glCreateProgram")();Load<AttachShader>("glAttachShader")(program,shader);Load<ShaderCommand>("glLinkProgram")(program);
    Load<GetStatus>("glGetProgramiv")(program,0x8B82,out ok);if(ok==0)throw new Exception("GPU shader link failed");
    Load<ShaderCommand>("glUseProgram")(program);timeLocation=Load<GetUniform>("glGetUniformLocation")(program,"uTime");resLocation=Load<GetUniform>("glGetUniformLocation")(program,"uRes");
    try{Load<SwapInterval>("wglSwapIntervalEXT")(0);}catch{}
    glGenTextures(1,out texture);glBindTexture(0x0DE1,texture);glTexImage2D(0x0DE1,0,0x8058,renderWidth,renderHeight,0,0x1908,0x1401,IntPtr.Zero);
    Load<GenObjects>("glGenFramebuffers")(1,out framebuffer);Load<BindObject>("glBindFramebuffer")(0x8D40,framebuffer);Load<AttachTexture>("glFramebufferTexture2D")(0x8D40,0x8CE0,0x0DE1,texture,0);if(Load<CheckFramebuffer>("glCheckFramebufferStatus")(0x8D40)!=0x8CD5)throw new Exception("Offscreen framebuffer incomplete");
    Directory.CreateDirectory(Path.GetDirectoryName(historyPath));history=new StreamWriter(historyPath,false);history.AutoFlush=true;history.WriteLine("elapsed_seconds,temperature_C,current_FPS");glGenTextures(1,out overlayTexture);
    initialized=true;clock.Start();lastTelemetry=Stopwatch.GetTimestamp();
    Task.Run(()=>{
     while(!stop){try{string raw=Workers.Nvidia();double t=Workers.NvidiaTemperature(raw);gpuTelemetry=raw;lock(this){currentTemp=t;peak=Math.Max(peak,t);lastTelemetry=Stopwatch.GetTimestamp();}if(t>=thermalLimit){fault="Thermal limit reached";stop=true;}}
      catch(Exception e){fault="Telemetry lost: "+e.Message;stop=true;}System.Threading.Thread.Sleep(500);}
    });
    Application.Idle+=RenderIdle;
    timer.Start();
   }
   void RenderIdle(object sender,EventArgs args){NativeMessage message;while(initialized&&!IsDisposed&&!PeekMessage(out message,IntPtr.Zero,0,0,0))SafeRender();}
   void SafeRender(){try{Render();}catch(Exception error){fault="Rendering failed: "+error.Message;Close();}}
   void Render(){
    if(!initialized)return;
    if(File.Exists(output+".cancel")){fault="Cancelled";Close();return;}
    double elapsed=clock.Elapsed.TotalSeconds;
    double age;lock(this)age=(Stopwatch.GetTimestamp()-lastTelemetry)/(double)Stopwatch.Frequency;
    if(stop||age>8||DurationExpired(seconds,elapsed)){if(age>8)fault="Telemetry timed out";Close();return;}
    Load<ShaderCommand>("glUseProgram")(program);Load<BindObject>("glBindFramebuffer")(0x8D40,framebuffer);glViewport(0,0,renderWidth,renderHeight);
    Load<Uniform1>("glUniform1f")(timeLocation,(float)elapsed);Load<Uniform2>("glUniform2f")(resLocation,renderWidth,renderHeight);
    glBegin(7);glVertex2f(-1,-1);glVertex2f(1,-1);glVertex2f(1,1);glVertex2f(-1,1);glEnd();Load<BindObject>("glBindFramebuffer")(0x8CA8,framebuffer);Load<BindObject>("glBindFramebuffer")(0x8CA9,0);Load<Blit>("glBlitFramebuffer")(0,0,renderWidth,renderHeight,0,0,ClientSize.Width,ClientSize.Height,0x4000,0x2600);DrawOverlay(elapsed,age);if(elapsed-lastCaptureCheck>=.5){lastCaptureCheck=elapsed;CaptureFrame();}glFinish();SwapBuffers(dc);frames++;
    if(elapsed-lastFpsAt>=1){currentFps=(frames-lastFrames)/(elapsed-lastFpsAt);minFps=Math.Min(minFps,currentFps);maxFps=Math.Max(maxFps,currentFps);lastFrames=frames;lastFpsAt=elapsed;}
    if(frames==1||elapsed-lastReport>=1){lastReport=elapsed;string sample=elapsed.ToString("0.0",CultureInfo.InvariantCulture)+","+currentTemp.ToString("0.0",CultureInfo.InvariantCulture)+","+currentFps.ToString("0.0",CultureInfo.InvariantCulture);samples.Enqueue(sample);while(samples.Count>600)samples.Dequeue();history.WriteLine(sample);double p;lock(this)p=peak;Workers.Write(output,"Native OpenGL stress | "+renderer+"\r\n"+elapsed.ToString("0.0")+" / "+(seconds==0?"until stopped":seconds+" seconds")+"\r\nAverage FPS: "+(frames/Math.Max(.01,elapsed)).ToString("0.0")+"\r\n"+Stats()+"\r\nPeak temperature: "+p.ToString("0.0")+" C");}
   }
   void DrawOverlay(double elapsed,double age){
    Load<BindObject>("glBindFramebuffer")(0x8D40,0);glViewport(0,0,ClientSize.Width,ClientSize.Height);
    glBindTexture(0x0DE1,overlayTexture);
    if(elapsed-lastOverlay>=.5||lastOverlay<0){
     lastOverlay=elapsed;double temperature,maximum;lock(this){temperature=currentTemp;maximum=peak;}
     using(var bitmap=StressOverlay.Draw(new StressOverlayData{Telemetry=gpuTelemetry,Renderer=renderer,Elapsed=elapsed,Fps=currentFps,MinFps=minFps,MaxFps=maxFps,AverageFps=frames/Math.Max(.01,elapsed),Temperature=temperature,PeakTemperature=maximum,TelemetryAge=age,Duration=seconds,Width=renderWidth,Height=renderHeight,Cutoff=thermalLimit,Frames=frames,TemperatureHistory=samples.Select(v=>float.Parse(v.Split(',')[1],CultureInfo.InvariantCulture)).ToArray()})){
      var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
      try{glTexImage2D(0x0DE1,0,0x8058,bitmap.Width,bitmap.Height,0,0x80E1,0x1401,data.Scan0);}finally{bitmap.UnlockBits(data);}
     }
     glTexParameteri(0x0DE1,0x2801,0x2601);glTexParameteri(0x0DE1,0x2800,0x2601);
    }
    Load<ShaderCommand>("glUseProgram")(0);glMatrixMode(0x1701);glPushMatrix();glLoadIdentity();glOrtho(0,ClientSize.Width,ClientSize.Height,0,-1,1);glMatrixMode(0x1700);glPushMatrix();glLoadIdentity();glEnable(0x0DE1);glEnable(0x0BE2);glBlendFunc(0x0302,0x0303);glColor4f(1,1,1,1);
    float scale=Math.Min(1,Math.Min((ClientSize.Width-24f)/StressOverlay.Width,(ClientSize.Height-24f)/StressOverlay.Height));float w=StressOverlay.Width*scale,h=StressOverlay.Height*scale;
    glBegin(7);glTexCoord2f(0,0);glVertex2f(12,12);glTexCoord2f(1,0);glVertex2f(12+w,12);glTexCoord2f(1,1);glVertex2f(12+w,12+h);glTexCoord2f(0,1);glVertex2f(12,12+h);glEnd();glDisable(0x0BE2);glDisable(0x0DE1);glPopMatrix();glMatrixMode(0x1701);glPopMatrix();glMatrixMode(0x1700);
   }
   void CaptureFrame(){if(!File.Exists(output+".capture"))return;using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height,PixelFormat.Format32bppArgb)){var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{glReadPixels(0,0,bitmap.Width,bitmap.Height,0x80E1,0x1401,data.Scan0);}finally{bitmap.UnlockBits(data);}bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);bitmap.Save(output+".png",ImageFormat.Png);}File.Delete(output+".capture");}
   string Stats(){double t;lock(this)t=currentTemp;return "Current FPS: "+currentFps.ToString("0.0")+"\r\nMinimum FPS: "+(minFps==double.MaxValue?0:minFps).ToString("0.0")+"\r\nMaximum FPS: "+maxFps.ToString("0.0")+"\r\nGPU temperature: "+t.ToString("0.0")+" C\r\nRender resolution: "+renderWidth+"x"+renderHeight+"\r\nDuration: "+(seconds==0?"Until stopped":seconds+" seconds")+"\r\nIndependent OpenGL workload; not a FurMark score.\r\nGPU telemetry (name, load %, temperature C, VRAM used/total MiB, power W, core MHz, fan %, memory MHz):\r\n"+gpuTelemetry;}
   protected override void OnFormClosed(FormClosedEventArgs e){
    initialized=false;Application.Idle-=RenderIdle;timer.Stop();stop=true;if(rc!=IntPtr.Zero){if(framebuffer!=0)Load<DeleteObjects>("glDeleteFramebuffers")(1,ref framebuffer);if(texture!=0)glDeleteTextures(1,ref texture);if(overlayTexture!=0)glDeleteTextures(1,ref overlayTexture);wglMakeCurrent(IntPtr.Zero,IntPtr.Zero);wglDeleteContext(rc);}if(dc!=IntPtr.Zero)ReleaseDC(Handle,dc);
    try{if(history!=null)history.Dispose();}catch(Exception error){fault="Telemetry history could not be saved: "+error.Message;}double elapsed=clock.Elapsed.TotalSeconds, p;lock(this)p=peak;
    Workers.Write(output,"Native OpenGL stress | "+renderer+"\r\nElapsed: "+elapsed.ToString("0.0")+" s\r\nFrames: "+frames+"\r\nAverage FPS: "+(frames/Math.Max(.01,elapsed)).ToString("0.0")+"\r\n"+Stats()+"\r\nPeak temperature: "+p.ToString("0.0")+" C\r\n"+(fault!=""?fault:DurationExpired(seconds,elapsed)?"Completed":"Stopped")+"\r\nFull telemetry CSV: "+historyPath+"\r\n\r\nRecent history (up to 600 samples): elapsed_seconds,temperature_C,current_FPS\r\n"+string.Join("\r\n",samples.ToArray())+"\r\n"+(fault==""&&DurationExpired(seconds,elapsed)?"Completed":"Stopped"));
    if(fault!=""&&fault!="Cancelled")Environment.ExitCode=1;timer.Dispose();base.OnFormClosed(e);
   }
  }
  static T Load<T>(string name) where T:class {IntPtr p=wglGetProcAddress(name);if(p==IntPtr.Zero||p.ToInt64()<=3||p.ToInt64()==-1)throw new Exception("Missing OpenGL function: "+name);return Marshal.GetDelegateForFunctionPointer(p,typeof(T)) as T;}
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void GenObjects(int count,out uint value);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void DeleteObjects(int count,ref uint value);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void BindObject(uint target,uint value);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void AttachTexture(uint target,uint attachment,uint textureTarget,uint texture,int level);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate uint CheckFramebuffer(uint target);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Blit(int x0,int y0,int x1,int y1,int dx0,int dy0,int dx1,int dy1,uint mask,uint filter);
  [DllImport("opengl32.dll")]static extern void glGenTextures(int count,out uint texture);
  [DllImport("opengl32.dll")]static extern void glDeleteTextures(int count,ref uint texture);
  [DllImport("opengl32.dll")]static extern void glBindTexture(uint target,uint texture);
  [DllImport("opengl32.dll")]static extern void glTexImage2D(uint target,int level,int internalFormat,int width,int height,int border,uint format,uint type,IntPtr data);
  [DllImport("opengl32.dll")]static extern void glFinish();
  [DllImport("opengl32.dll")]static extern void glTexParameteri(uint target,uint parameter,int value);
  [DllImport("opengl32.dll")]static extern void glMatrixMode(uint mode);
  [DllImport("opengl32.dll")]static extern void glPushMatrix();
  [DllImport("opengl32.dll")]static extern void glPopMatrix();
  [DllImport("opengl32.dll")]static extern void glLoadIdentity();
  [DllImport("opengl32.dll")]static extern void glOrtho(double left,double right,double bottom,double top,double near,double far);
  [DllImport("opengl32.dll")]static extern void glEnable(uint capability);
  [DllImport("opengl32.dll")]static extern void glDisable(uint capability);
  [DllImport("opengl32.dll")]static extern void glBlendFunc(uint source,uint destination);
  [DllImport("opengl32.dll")]static extern void glColor4f(float red,float green,float blue,float alpha);
  [DllImport("opengl32.dll")]static extern void glTexCoord2f(float x,float y);
  [DllImport("opengl32.dll")]static extern void glReadPixels(int x,int y,int width,int height,uint format,uint type,IntPtr data);
  [StructLayout(LayoutKind.Sequential)]struct NativeMessage{public IntPtr Window;public uint Message;public UIntPtr WParam;public IntPtr LParam;public uint Time;public int X,Y;public uint Private;}
  [DllImport("user32.dll")]static extern bool PeekMessage(out NativeMessage message,IntPtr window,uint minimum,uint maximum,uint remove);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate uint CreateShader(uint type);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate uint CreateProgram();
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void ShaderSource(uint shader,int count,IntPtr strings,IntPtr lengths);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void ShaderCommand(uint shader);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void AttachShader(uint program,uint shader);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void GetStatus(uint shader,uint parameter,out int result);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate int GetUniform(uint program,string name);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Uniform1(int location,float value);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Uniform2(int location,float x,float y);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate int SwapInterval(int interval);
  [StructLayout(LayoutKind.Sequential)]struct PFD{public ushort Size,Version;public uint Flags;public byte PixelType,ColorBits,RedBits,RedShift,GreenBits,GreenShift,BlueBits,BlueShift,AlphaBits,AlphaShift,AccumBits,AccumRedBits,AccumGreenBits,AccumBlueBits,AccumAlphaBits,DepthBits,StencilBits,AuxBuffers,LayerType,Reserved;public uint LayerMask,VisibleMask,DamageMask;}
  [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
  [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
  [DllImport("gdi32.dll")]static extern int ChoosePixelFormat(IntPtr dc,ref PFD pfd);
  [DllImport("gdi32.dll")]static extern bool SetPixelFormat(IntPtr dc,int format,ref PFD pfd);
  [DllImport("gdi32.dll")]static extern bool SwapBuffers(IntPtr dc);
  [DllImport("opengl32.dll")]static extern IntPtr wglCreateContext(IntPtr dc);
  [DllImport("opengl32.dll")]static extern bool wglMakeCurrent(IntPtr dc,IntPtr rc);
  [DllImport("opengl32.dll")]static extern bool wglDeleteContext(IntPtr rc);
  [DllImport("opengl32.dll")]static extern IntPtr wglGetProcAddress(string name);
  [DllImport("opengl32.dll")]static extern IntPtr glGetString(uint name);
  [DllImport("opengl32.dll")]static extern void glViewport(int x,int y,int w,int h);
  [DllImport("opengl32.dll")]static extern void glBegin(uint mode);
  [DllImport("opengl32.dll")]static extern void glVertex2f(float x,float y);
  [DllImport("opengl32.dll")]static extern void glEnd();
 }
}




