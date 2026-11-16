using System;
using System.Collections.Generic;
using System.Globalization;
namespace Overclock {
 public sealed class PhysicalCapacity {public int Number;public string Name,MediaType;public long Bytes;}
 public sealed class StorageCapacity {public long PhysicalBytes,VolumeBytes,FreeBytes;public int VolumeCount;public List<PhysicalCapacity> Disks=new List<PhysicalCapacity>();public long UsedBytes{get{return Math.Max(0,VolumeBytes-FreeBytes);}}public long OtherBytes{get{return Math.Max(0,PhysicalBytes-VolumeBytes);}}}
 static class StorageFormat {public static string Size(long bytes){return bytes>=1099511627776L?(bytes/1099511627776.0).ToString("0.00",CultureInfo.CurrentCulture)+" TiB":(bytes/1073741824.0).ToString("0.0",CultureInfo.CurrentCulture)+" GiB";}}
}
