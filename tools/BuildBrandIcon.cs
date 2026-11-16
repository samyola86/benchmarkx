using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
class BuildBrandIcon {
 static void Main(string[] args){using(var source=new Bitmap(args[0]))using(var stream=File.Create(args[1]))using(var writer=new BinaryWriter(stream)){int[] sizes={16,24,32,48,64,128,256};var images=new List<byte[]>();foreach(int size in sizes){using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Rectangle(0,0,size,size));}using(var png=new MemoryStream()){bitmap.Save(png,ImageFormat.Png);images.Add(png.ToArray());}}}writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}foreach(var png in images)writer.Write(png);}}
}
