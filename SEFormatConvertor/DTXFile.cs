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
        uint iResType;

        int iVersion;

        ushort width;
        ushort height;

        ushort nMipmap;
        ushort nSection;

        int flags;
        int userFlags;

        byte[] extra;
        byte[] cmdStr;

        byte[] rawBuffer;

        byte storeType => extra[2];

        public async Task LoadFromStream(Stream stream, bool lzma = false)
        {
            var br = new ExtendedBinaryReader(stream);

            // DTX Header

            iResType = br.ReadUInt32();

            iVersion = br.ReadInt32();

            width = br.ReadUInt16();
            height = br.ReadUInt16();

            nMipmap = br.ReadUInt16();
            nSection = br.ReadUInt16();

            flags = br.ReadInt32();
            userFlags = br.ReadInt32();

            extra = br.ReadBytes(12);

            cmdStr = br.ReadBytes(128);

            if (iResType != 0 || iVersion != -5 || nMipmap == 0)
            {
                if (lzma)
                {
                    throw new Exception("Unsupported DTX Type");
                }
                else
                {
                    stream.Position = 0;

                    var lzmaStream = new LzmaDecodeStream(stream);
                    var ms = new MemoryStream();

                    lzmaStream.CopyTo(ms);

                    await LoadFromStream(ms, true);
                    return;
                }
            }

            DXTDecoder decoder = null;

            if(storeType == 4)
            {
                decoder = new DXT1Decoder();
            }   
            else if(storeType == 5)
            {
                decoder = new DXT3Decoder();
            }
            else if(storeType == 6)
            {
                decoder = new DXT5Decoder();
            }

            if(decoder != null)
            {
                rawBuffer = await decoder.DecodeFrame(stream, width, height);
            }
            else
            {
                rawBuffer = br.ReadBytes(width * height * 4);
            }

            br.Close();
        }

        public static async Task<DTXFile> Load(FileInfo info)
        {
            var result = new DTXFile();

            await result.LoadFromStream(info.OpenRead());

            return result;
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
