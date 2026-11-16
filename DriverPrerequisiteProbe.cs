using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

static class DriverPrerequisiteProbe {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint mode,uint attributes,IntPtr template);
 static int Main(string[] args){
  if(args.Length!=2||args[0]!="--check")return 64;
  try{
   string installed="";using(var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var key=hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"))if(key!=null)installed=Convert.ToString(key.GetValue("DisplayVersion"));
   Version version;if(!Version.TryParse(installed,out version)||version<new Version(2,0)){File.WriteAllText(args[1],"PawnIO 2.0 or newer is required. Installed version: "+(installed==""?"not installed":installed));return 10;}
   using(var device=CreateFile(@"\\?\GLOBALROOT\Device\PawnIO",0xC0000000,3,IntPtr.Zero,3,0,IntPtr.Zero)){
    if(device.IsInvalid){int error=Marshal.GetLastWin32Error();File.WriteAllText(args[1],"PawnIO "+version+" is registered but hardware access is unavailable (Windows error "+error+"). Administrator access or a restart may be required.");return 11;}
   }
   File.WriteAllText(args[1],"READY: PawnIO "+version+"; driver device opened successfully.");return 0;
  }catch(Exception error){try{File.WriteAllText(args[1],"PawnIO readiness check failed: "+error.Message);}catch{}return 12;}
 }
}
