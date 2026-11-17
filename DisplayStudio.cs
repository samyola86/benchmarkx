using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
namespace Overclock {
 // Runs only in --studio-host, never in the UI process. The supervisor kills hung DDC calls.
 sealed class NativeDisplayStudio:IDisposable {
  readonly Dictionary<string,IntPtr> handles=new Dictionary<string,IntPtr>();readonly Dictionary<string,string> wmi=new Dictionary<string,string>();List<StudioDevice> devices=new List<StudioDevice>();
  delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,IntPtr rect,IntPtr data);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct MonitorInfo {public int Size;public RECT Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
  [StructLayout(LayoutKind.Sequential)]struct RECT {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct DisplayDevice {public int Size;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Physical {public IntPtr Handle;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;}
  [DllImport("user32.dll")]static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool EnumDisplayDevices(string device,uint index,ref DisplayDevice info,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool EnumDisplaySettings(string device,int index,IntPtr mode);
  [DllImport("dxva2.dll")]static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor,out uint count);
  [DllImport("dxva2.dll",CharSet=CharSet.Unicode)]static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor,uint count,[Out]Physical[] physical);
  [DllImport("dxva2.dll")]static extern bool DestroyPhysicalMonitor(IntPtr monitor);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetMonitorCapabilities(IntPtr monitor,out uint caps,out uint temperatures);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetMonitorBrightness(IntPtr monitor,out uint min,out uint current,out uint max);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetMonitorContrast(IntPtr monitor,out uint min,out uint current,out uint max);
  [DllImport("dxva2.dll")]static extern bool GetMonitorColorTemperature(IntPtr monitor,out int temperature);
  [DllImport("dxva2.dll")]static extern bool SetMonitorBrightness(IntPtr monitor,uint value);
  [DllImport("dxva2.dll")]static extern bool SetMonitorContrast(IntPtr monitor,uint value);
  [DllImport("dxva2.dll")]static extern bool SetMonitorColorTemperature(IntPtr monitor,int value);
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateDC(string driver,string device,string output,IntPtr init);
  [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetICMProfile(IntPtr dc,ref uint size,StringBuilder profile);
  [DllImport("mscms.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WcsAssociateColorProfileWithDevice(int scope,string profile,string device);
  [DllImport("mscms.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WcsSetDefaultColorProfile(int scope,string device,int type,int subtype,uint id,string profile);
  [DllImport("mscms.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WcsGetUsePerUserProfiles(string device,uint id,out bool use);
  [DllImport("mscms.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WcsSetUsePerUserProfiles(string device,uint id,bool use);
  [DllImport("mscms.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool InstallColorProfile(string machine,string file);
  [StructLayout(LayoutKind.Sequential)]struct ProfileFile {public uint Type;public IntPtr Data;public uint Size;}
  [DllImport("mscms.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenColorProfile(ref ProfileFile profile,uint access,uint share,uint creation);
  [DllImport("mscms.dll")]static extern bool IsColorProfileValid(IntPtr profile,out bool valid);
  [DllImport("mscms.dll")]static extern bool CloseColorProfile(IntPtr profile);
  [StructLayout(LayoutKind.Sequential)]struct EnumType {public uint Size,Version,Fields;public IntPtr Device;[MarshalAs(UnmanagedType.ByValArray,SizeConst=18)]public uint[] Criteria;}
  [DllImport("mscms.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool EnumColorProfiles(string machine,ref EnumType criteria,byte[] buffer,ref uint size,out uint count);
  [DllImport("mscms.dll",CharSet=CharSet.Unicode)]static extern bool GetColorDirectory(string machine,StringBuilder directory,ref uint size);
  static string[] InstalledProfiles(){var criteria=new EnumType{Size=(uint)Marshal.SizeOf(typeof(EnumType)),Version=0x300,Criteria=new uint[18]};uint size=0,count;EnumColorProfiles(null,ref criteria,null,ref size,out count);if(size==0)return new string[0];if(size>1024*1024)throw new InvalidDataException("ICC inventory too large");var buffer=new byte[size];if(!EnumColorProfiles(null,ref criteria,buffer,ref size,out count))throw new Win32Exception();uint chars=32768;var directory=new StringBuilder((int)chars);if(!GetColorDirectory(null,directory,ref chars))throw new Win32Exception();return Encoding.Unicode.GetString(buffer).Split(new[]{'\0'},StringSplitOptions.RemoveEmptyEntries).Select(p=>Path.IsPathRooted(p)?p:Path.Combine(directory.ToString(),p)).Where(p=>p.EndsWith(".icc",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".icm",StringComparison.OrdinalIgnoreCase)).ToArray();}
  static void ValidateWindowsIcc(string file){ValidateIcc(file);IntPtr path=Marshal.StringToHGlobalUni(file);try{var descriptor=new ProfileFile{Type=1,Data=path,Size=(uint)((file.Length+1)*2)};IntPtr profile=OpenColorProfile(ref descriptor,1,1,3);if(profile==IntPtr.Zero)throw new Win32Exception();try{bool valid;if(!IsColorProfileValid(profile,out valid)||!valid)throw new InvalidDataException("Windows rejected this ICC profile");}finally{CloseColorProfile(profile);}}finally{Marshal.FreeHGlobal(path);}}
  static string Key(string id){return (id??"").Replace("\\\\?\\","").Split('{')[0].TrimEnd('#').Replace('#','\\').ToUpperInvariant();}
  public static int Normalize(uint current,uint min,uint max){if(max<=min||current<min||current>max)throw new InvalidDataException("Invalid monitor range");return (int)Math.Round((current-min)*100.0/(max-min));}
  public static uint Raw(int value,uint min,uint max){if(value<0||value>100||max<=min)throw new InvalidDataException("Invalid monitor value");return min+(uint)Math.Round((max-min)*value/100.0);}
  static string CurrentProfile(string device){IntPtr dc=CreateDC("DISPLAY",device,null,IntPtr.Zero);try{uint size=32768;var text=new StringBuilder((int)size);return dc!=IntPtr.Zero&&GetICMProfile(dc,ref size,text)?text.ToString():null;}finally{if(dc!=IntPtr.Zero)DeleteDC(dc);}}
  public static void ValidateIcc(string file){var f=new FileInfo(file);if(!f.Exists||f.Length<128||f.Length>16*1024*1024)throw new InvalidDataException("ICC file size invalid");byte[] b=File.ReadAllBytes(file);uint size=(uint)(b[0]<<24|b[1]<<16|b[2]<<8|b[3]);string type=Encoding.ASCII.GetString(b,12,4);if(size!=b.Length||Encoding.ASCII.GetString(b,36,4)!="acsp"||(type!="disp"&&type!="spac")||Encoding.ASCII.GetString(b,16,4)!="RGB ")throw new InvalidDataException("Only valid RGB display/working-space ICC files are supported");}
  public StudioReply Execute(StudioCommand c){if(c.Action=="profiles")return new StudioReply{Profiles=InstalledProfiles()};if(c.Action=="discover")return Discover();if(c.Action=="import-icc"){ValidateWindowsIcc(c.Profile);if(!InstallColorProfile(null,c.Profile))throw new Win32Exception();return new StudioReply{Message="ICC imported. Select it explicitly to associate with a display."};}
   foreach(var n in new[]{c.Brightness,c.Contrast,c.Speed})if(n.HasValue&&(n.Value<0||n.Value>100))throw new InvalidDataException("Value outside 0-100");
   var d=devices.SingleOrDefault(x=>x.Id==c.Id&&!x.Ambiguous);if(d==null)throw new InvalidOperationException("Display missing or identity ambiguous. Refresh.");
   // Re-enumerate logical identity before writes; never use a handle after a hotplug identity change.
   bool attached=false;EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(m,dc,r,data)=>{var mi=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(GetMonitorInfo(m,ref mi)&&mi.Device==d.ProfileDevice){var dd=new DisplayDevice{Size=Marshal.SizeOf(typeof(DisplayDevice))};if(EnumDisplayDevices(mi.Device,0,ref dd,1)&&Key(dd.Id)==d.Id)attached=true;}return true;},IntPtr.Zero);
   if(!attached)throw new InvalidOperationException("Display topology changed. Refresh before applying.");
   if(c.Action=="hdr"){if(!c.Hdr.HasValue)throw new InvalidOperationException("HDR state missing");HdrDisplay.Set(d,c.Hdr.Value);return new StudioReply{Devices=new List<StudioDevice>{StudioJson.Clone(d)},Message=d.HdrStatus};}
   if((c.Action=="brightness-live"||c.Action=="brightness-read")&&!d.Internal){
    if(c.Action=="brightness-live"&&!c.Brightness.HasValue||!d.Brightness.HasValue)throw new InvalidOperationException("Brightness unavailable. Refresh display capabilities.");
    IntPtr physical;uint min,current,max;if(!handles.TryGetValue(d.Id,out physical)||!GetMonitorBrightness(physical,out min,out current,out max)||max<=min||current<min||current>max)throw new IOException("Brightness read failed. Refresh the display.");
    if(c.Action=="brightness-read"){d.Brightness=Normalize(current,min,max);return new StudioReply{Devices=new List<StudioDevice>{StudioJson.Clone(d)}};}
    if(!SetMonitorBrightness(physical,Raw(c.Brightness.Value,min,max)))throw new IOException("Brightness write failed");
    if(!GetMonitorBrightness(physical,out min,out current,out max)||max<=min||current<min||current>max)throw new IOException("Brightness sent; readback unavailable. Restore previous is available.");
    d.Brightness=Normalize(current,min,max);d.BrightnessMin=min;d.BrightnessMax=max;d.ObservedUtc=DateTime.UtcNow;
    return new StudioReply{Devices=new List<StudioDevice>{StudioJson.Clone(d)},Message="Brightness "+d.Brightness+"%"+(Math.Abs(d.Brightness.Value-c.Brightness.Value)<=1?"; readback confirmed":"; monitor returned a different value")};
   }
   if(c.Action=="brightness-live")c.Action="apply";
   if(c.Action=="read"||c.Action=="brightness-read"){Read(d);return new StudioReply{Devices=new List<StudioDevice>{StudioJson.Clone(d)}};}
   if(c.Action!="apply"&&c.Action!="restore")throw new InvalidDataException("Unknown display command");
   var target=c.Action=="restore"?c.Snapshot:null;if(target!=null){c=StudioJson.Clone(c);c.Brightness=target.Brightness;c.Contrast=target.Contrast;c.Temperature=target.Temperature;c.Profile=target.RestoreIcc?target.Profile:null;}
   Read(d);IntPtr h;handles.TryGetValue(d.Id,out h);
   var outcome=new List<string>();
   if(c.Brightness.HasValue){if(!d.Brightness.HasValue)throw new InvalidOperationException("Brightness unavailable; enable DDC/CI in the monitor OSD");if(d.Internal){using(var search=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightnessMethods")){bool done=false;foreach(ManagementObject item in search.Get())using(item){if((string)item["InstanceName"]==wmi[d.Id]){item.InvokeMethod("WmiSetBrightness",new object[]{(uint)1,(byte)c.Brightness.Value});done=true;}}if(!done)throw new IOException("Internal brightness method unavailable");}}else if(!SetMonitorBrightness(h,Raw(c.Brightness.Value,d.BrightnessMin,d.BrightnessMax)))throw new IOException("Brightness write failed");outcome.Add("brightness sent");}
   if(c.Contrast.HasValue){if(!d.Contrast.HasValue||!SetMonitorContrast(h,Raw(c.Contrast.Value,d.ContrastMin,d.ContrastMax)))throw new IOException("Contrast unsupported or write failed (earlier settings may have changed)");outcome.Add("contrast sent");}
   if(c.Temperature.HasValue){int t=c.Temperature.Value;if(t<1||t>8||(d.TemperatureFlags&(1u<<(t-1)))==0||!d.Temperature.HasValue||!SetMonitorColorTemperature(h,t))throw new IOException("Colour temperature unsupported or write failed (earlier settings may have changed)");outcome.Add("temperature sent");}
   if(c.Profile!=null){if(d.HdrEnabled==true)throw new InvalidOperationException("Turn HDR off before applying an SDR ICC profile");if(!d.PerUserProfiles.HasValue||string.IsNullOrEmpty(d.Profile))throw new IOException("Previous ICC state unavailable; refusing an irreversible profile change");ValidateWindowsIcc(c.Profile);if(!WcsAssociateColorProfileWithDevice(1,c.Profile,d.ProfileDevice)||!WcsSetUsePerUserProfiles(d.ProfileDevice,0x6d6e7472,true)||!WcsSetDefaultColorProfile(1,d.ProfileDevice,0,4,0,c.Profile))throw new Win32Exception(Marshal.GetLastWin32Error(),"ICC association/default failed; previous physical settings may have changed");outcome.Add("current-user ICC associated");}
   if(target!=null&&target.RestoreIcc){if(target.Profile==null&&!WcsSetDefaultColorProfile(1,d.ProfileDevice,0,4,0,null))throw new Win32Exception();if(target.PerUserProfiles.HasValue&&!WcsSetUsePerUserProfiles(d.ProfileDevice,0x6d6e7472,target.PerUserProfiles.Value))throw new Win32Exception();}
   Read(d);bool verified=(!c.Brightness.HasValue||d.Brightness.HasValue&&Math.Abs(c.Brightness.Value-d.Brightness.Value)<=1)&&(!c.Contrast.HasValue||d.Contrast.HasValue&&Math.Abs(c.Contrast.Value-d.Contrast.Value)<=1)&&(!c.Temperature.HasValue||c.Temperature==d.Temperature)&&(c.Profile==null||string.Equals(Path.GetFileName(c.Profile),Path.GetFileName(d.Profile),StringComparison.OrdinalIgnoreCase));return new StudioReply{Devices=new List<StudioDevice>{StudioJson.Clone(d)},Message=string.Join(", ",outcome)+"; "+(verified?"readback confirmed":"readback differs/unavailable; inspect monitor and use Restore previous")};
  }
  StudioReply Discover(){Dispose();devices.Clear();wmi.Clear();EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(m,dc,r,data)=>{var mi=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(!GetMonitorInfo(m,ref mi))return true;var dd=new DisplayDevice{Size=Marshal.SizeOf(typeof(DisplayDevice))};if(!EnumDisplayDevices(mi.Device,0,ref dd,1))return true;string id=Key(dd.Id);if(string.IsNullOrWhiteSpace(id))return true;var d=new StudioDevice{Id=id,Name=dd.Description,ProfileDevice=mi.Device,Status="Read-only / DDC unavailable",Detail=(mi.Monitor.Right-mi.Monitor.Left)+" x "+(mi.Monitor.Bottom-mi.Monitor.Top)+"  /  "+mi.Device};
    // DEVMODEW has a fixed public header; dmDisplayFrequency is at offset 184.
    IntPtr mode=Marshal.AllocHGlobal(220);try{for(int i=0;i<220;i++)Marshal.WriteByte(mode,i,0);Marshal.WriteInt16(mode,68,220);if(EnumDisplaySettings(mi.Device,-1,mode))d.Detail+="  /  "+Marshal.ReadInt32(mode,184)+" Hz";}finally{Marshal.FreeHGlobal(mode);}
    uint count;if(GetNumberOfPhysicalMonitorsFromHMONITOR(m,out count)&&count>0&&count<=16){var physical=new Physical[count];if(GetPhysicalMonitorsFromHMONITOR(m,count,physical)){if(count==1&&!handles.ContainsKey(id))handles[id]=physical[0].Handle;else{d.Ambiguous=true;foreach(var p in physical)DestroyPhysicalMonitor(p.Handle);}}}devices.Add(d);return true;},IntPtr.Zero);
   foreach(var group in devices.GroupBy(x=>x.Id).Where(g=>g.Count()>1))foreach(var d in group)d.Ambiguous=true;
   foreach(var d in devices)if(!d.Ambiguous)Read(d);return new StudioReply{Devices=StudioJson.Clone(devices),Message=devices.Count+" connected display(s). No settings changed."};
  }
  void Read(StudioDevice d){d.ObservedUtc=DateTime.UtcNow;d.StateSource="Windows monitor API readback";HdrDisplay.Read(d);d.Brightness=null;d.Contrast=null;d.Temperature=null;d.TemperatureFlags=0;IntPtr h;uint caps=0,temps=0,min,cur,max;if(handles.TryGetValue(d.Id,out h)){
    // Some monitors fail the aggregate capability query but implement brightness/contrast.
    // A successful bounded read of each setting is the capability check; never enable blindly.
    bool advertised=GetMonitorCapabilities(h,out caps,out temps);
    bool readBrightness=GetMonitorBrightness(h,out min,out cur,out max);int brightnessError=readBrightness?0:Marshal.GetLastWin32Error();
    if(readBrightness&&max>min&&cur>=min&&cur<=max){d.Brightness=Normalize(cur,min,max);d.BrightnessMin=min;d.BrightnessMax=max;}
    bool readContrast=GetMonitorContrast(h,out min,out cur,out max);int contrastError=readContrast?0:Marshal.GetLastWin32Error();
    if(readContrast&&max>min&&cur>=min&&cur<=max){d.Contrast=Normalize(cur,min,max);d.ContrastMin=min;d.ContrastMax=max;}
    int t;if(advertised&&(caps&8)!=0&&GetMonitorColorTemperature(h,out t)&&t>=1&&t<=8){d.Temperature=t;d.TemperatureFlags=temps;}
    d.Diagnostics="Physical handle available; capabilities="+(advertised?"0x"+caps.ToString("X"):"unavailable")+"; brightness="+(d.Brightness.HasValue?"read OK":"Win32 "+brightnessError)+"; contrast="+(d.Contrast.HasValue?"read OK":"Win32 "+contrastError);
   }else d.Diagnostics="Windows did not expose an unambiguous physical monitor handle (check connection/driver).";
   if(!d.Brightness.HasValue){try{using(var search=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightness WHERE Active=True"))foreach(ManagementObject item in search.Get())using(item){string instance=(string)item["InstanceName"];string key=Key(instance);if(key.EndsWith("_0"))key=key.Substring(0,key.Length-2);if(key==d.Id){d.Internal=true;wmi[d.Id]=instance;d.Brightness=Convert.ToInt32(item["CurrentBrightness"]);}}}catch(ManagementException){}}
   bool perUser;d.PerUserProfiles=WcsGetUsePerUserProfiles(d.ProfileDevice,0x6d6e7472,out perUser)?(bool?)perUser:null;d.Profile=CurrentProfile(d.ProfileDevice);d.Status=d.Ambiguous?"Ambiguous identity / read-only":d.Internal?"Internal brightness":d.Brightness.HasValue||d.Contrast.HasValue?"DDC/CI supported":"DDC/CI unavailable / read-only";
  }
  public void Dispose(){foreach(var h in handles.Values)DestroyPhysicalMonitor(h);handles.Clear();}
  public static void Host(){using(var provider=new NativeDisplayStudio()){string line;while((line=Console.ReadLine())!=null){StudioReply result;try{if(line.Length>2*1024*1024)throw new InvalidDataException("Command too large");result=provider.Execute(StudioJson.Parse<StudioCommand>(line));}catch(Exception e){result=new StudioReply{Error=e.Message};}Console.WriteLine(StudioJson.Serialize(result));Console.Out.Flush();}}}
 }
}
