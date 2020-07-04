using SELib.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DDSReader.Internal.Decoders;
using SevenZip;

namespace SEFormatConvertor
{
    public class DTXFile
    {
        int width, height;

        byte[] rawBuffer;

        public static async Task<DTXFile> Load(FileInfo info)
        {
            var result = new DTXFile();

            var br = new ExtendedBinaryReader(info.OpenRead());
            {
                var check = br.ReadUInt32();

                if(check != 0)
                {
                    br.Skip(-4);
                    result.height = br.ReadUInt16();
                    result.width = br.ReadUInt16();

                    if((result.height & (result.height - 1)) != 0 || (result.width & (result.width - 1)) != 0)
                    {
                        br.Close();

                        var lzmaStream = new LzmaDecodeStream(info.OpenRead());
                        var ms = new MemoryStream();

                        lzmaStream.CopyTo(ms);

                        br = new ExtendedBinaryReader(ms);
                        br.Skip(0, true);

                        check = br.ReadUInt32();

                        if(check != 0)
                        {
                            br.Skip(-4);

                            result.height = br.ReadUInt16();
                            result.width = br.ReadUInt16();

                            br.Skip(2);
                        }
                        else
                        {
                            br.Skip(4);
                            result.height = br.ReadUInt16();
                            result.width = br.ReadUInt16();
                        }
                    }
                    else
                    {
                        br.Skip(2);
                    }
                }
                else
                {
                    br.Skip(4);
                    result.height = br.ReadUInt16();
                    result.width = br.ReadUInt16();
                }

                var mipmaps = br.ReadUInt32();
                var fmt1 = br.ReadUInt32();
                var fmt2 = br.ReadUInt32();

                br.Skip(2);

                var fmt3 = br.ReadUInt16();

                br.Skip(136);

                Console.Write($"{info.Name}: {result.width}x{result.height} Format:");

                var dx = (result.width + 3) >> 2;
                var dy = (result.height + 3) >> 2;

                if(fmt3 <= 3)
                {
                    result.rawBuffer = br.ReadBytes(4 * result.width * result.height);
                    Console.Write("ABGR");
                }
                else if(fmt3 == 4) // DXT1
                {
                    var decoder = new DXT1Decoder();
                    result.rawBuffer = await decoder.DecodeFrame(br.BaseStream, (uint)result.width, (uint)result.height);

                    Console.Write("DXT1");
                }
                else if(fmt3 == 6 || fmt3 == 5) // DXT5
                {
                    var decoder = new DXT5Decoder();
                    result.rawBuffer = await decoder.DecodeFrame(br.BaseStream, (uint)result.width, (uint)result.height);

                    Console.Write("DXT5");
                }

                Console.WriteLine("");

                br.Close();
                return result;
            }
        }

        

        public string Save(Stream output)
        {
            if (rawBuffer != null)
            {
                int depth = rawBuffer.Length / height / width * 8;

                int stride = ((width * depth + (depth - 1)) & ~(depth - 1)) / 8;

                var image = BitmapSource.Create(width, height, 72, 72, depth == 24 ? PixelFormats.Bgr24 : PixelFormats.Bgra32, BitmapPalettes.Gray256, rawBuffer, stride);

                var encoder = new PngBitmapEncoder();

                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(output);

                output.Dispose();

                return ".png";
            }

            return null;
        }
    }
}
