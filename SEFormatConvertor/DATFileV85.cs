using SELib.Utilities;
using SELib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SEFormatConvertor
{
    class DATMesh
    { 
        public string Name { get; set; }
    }

    class DATFileV85
    {
        public uint Version { get; set; }
        public string Info { get; set; }
        public uint MeshCount { get; set; }
        public DATMesh[] Meshes { get; set; }

        public DATFileV85(ExtendedBinaryReader br)
        {
            Version = br.ReadUInt32();

            br.BaseStream.Seek(4 * 6 + 32, SeekOrigin.Current);

            Info = ReadInfo(br);

            br.BaseStream.Seek(15 * 4 + 1, SeekOrigin.Current);

            var count = br.ReadUInt32();
            br.BaseStream.Seek(14 + 32 * count, SeekOrigin.Current);

            MeshCount = br.ReadUInt32();
            Meshes = ParseMeshes(MeshCount, br);

            Console.ReadKey();
        }

        private DATMesh[] ParseMeshes(uint meshCount, ExtendedBinaryReader br)
        {
            var meshes = new List<DATMesh>();

            for(int i = 0; i < meshCount; ++i)
            {
                var mesh = new DATMesh();
                br.BaseStream.Seek(8, SeekOrigin.Current);

                mesh.Name = br.ReadStringWithUInt16Length();

            }

            return meshes.ToArray();
        }

        private string ReadInfo(ExtendedBinaryReader br)
        {
            var len = br.ReadInt32();
            var bytes = br.ReadBytes(len);

            return Encoding.UTF8.GetString(bytes);
        }


    }
}
