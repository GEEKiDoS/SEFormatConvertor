using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using SELib;
using SELib.Utilities;
using SevenZip;
using KoreanRomanisation;

namespace SEFormatConvertor
{
    class LTBFile
    {
        /// <summary>
        /// A list of bones, in order by index
        /// </summary>
        public Dictionary<byte, SEModelBone> Bones { get; private set; }
        /// <summary>
        /// A list of meshes, in order
        /// </summary>
        public List<SEModelMesh> Meshes { get; private set; }
        /// <summary>
        /// A list of materials, in order
        /// </summary>
        public List<SEModelMaterial> Materials { get; private set; }

        public Dictionary<int, List<int[]>> WeightSets { get; private set; }
        public Dictionary<int, List<float[]>> Weights { get; private set; }

        public Dictionary<string, SEAnim> Animations { get; private set; }

        public enum PieceType
        {
            RigidMesh = 4,
            SkelMesh = 5,
            VAMesh = 6
        }

        [Flags]
        enum DataType
        {
            Position = 0x0001,
            Normal = 0x0002,
            Color = 0x0004,
            UVSets1 = 0x0010,
            UVSets2 = 0x0020,
            UVSets3 = 0x0040,
            UVSets4 = 0x0080,
            BasisVectors = 0x0100
        }

        enum AnimCompressionType
        {
            None = 0,
            Relevant = 1,
            Relevant_16bit = 2,
            REL_PV16 = 3
        };

        public static readonly Dictionary<string, string> replaceDictionary = new Dictionary<string, string>
        {
            { ".", "_" },
            { "-", "_" },
            { " ", "_" }
        };

        public static readonly Encoding ltbEncode = Encoding.GetEncoding(51949);

        public static readonly Quaternion globalRotation = Quaternion.FromEulerAngles(-Math.PI / 2, 0, -Math.PI / 2);

        private LTBFile()
        {
            Bones = new Dictionary<byte, SEModelBone>();
            Meshes = new List<SEModelMesh>();
            Materials = new List<SEModelMaterial>();

            WeightSets = new Dictionary<int, List<int[]>>();
            Weights = new Dictionary<int, List<float[]>>();

            Animations = new Dictionary<string, SEAnim>();
        }

        public SEModel ToSEModel()
        {
            var semdl = new SEModel();

            foreach (var bone in from x in Bones orderby x.Key ascending select x.Value)
                semdl.AddBone(bone.BoneName, bone.BoneParent, bone.GlobalPosition, bone.GlobalRotation, bone.LocalPosition, bone.LocalRotation, bone.Scale);

            foreach (var material in Materials)
                semdl.AddMaterial(material);

            foreach (var mesh in Meshes)
                semdl.AddMesh(mesh);

            foreach (var mesh in semdl.Meshes)
            {
                foreach (var v in mesh.Verticies)
                {
                    var weightLeft = 1.0f;

                    var maxWeight = 0.0f;
                    var maxWeightIdx = 0u;

                    for (int i = (int)v.WeightCount - 1; i >= 0; i--)
                    {
                        if (v.Weights[i].BoneWeight == 0 || v.Weights[i].BoneIndex >= 0xFF)
                        {
                            //Console.WriteLine($"Removed SB Weight at vertex {mesh.Verticies.IndexOf(v)} on mesh {ltbFile.Meshes.IndexOf(mesh)}");
                            v.Weights.RemoveAt(i);
                        }
                        else
                        {
                            weightLeft -= v.Weights[i].BoneWeight;

                            if (v.Weights[i].BoneWeight > maxWeight)
                            {
                                maxWeight = v.Weights[i].BoneWeight;
                                maxWeightIdx = v.Weights[i].BoneIndex;
                            }
                        }
                    }

                    if (weightLeft != 0.0f)
                    {
                        v.Weights.Find(weight => weight.BoneIndex == maxWeightIdx).BoneWeight += weightLeft;
                    }
                }
            }

            semdl.GenerateLocalPositions(true, true);

            return semdl;
        }

        public static LTBFile Read(FileInfo info)
        {
            var bFlip = info.Name.StartsWith("PV");

            var ltbFile = new LTBFile();

            var br = new ExtendedBinaryReader(info.OpenRead());

            var header = br.ReadUInt16();
            if (header > 20)
            {
                br.Close();
                var lzmaStream = new LzmaDecodeStream(info.OpenRead());
                var ms = new MemoryStream();

                lzmaStream.CopyTo(ms);

                if (ms.Length == 0)
                {
                    Console.WriteLine($"{info.Name} is not a vaild LTB file.");

                    return null;
                }

                br = new ExtendedBinaryReader(ms);
                br.Skip(0, true);
            }

            // Skip header
            br.Skip(0x14, true);

            uint version = br.ReadUInt32();

            uint nKeyFrame = br.ReadUInt32();
            uint nAnim = br.ReadUInt32();
            uint numBones = br.ReadUInt32();
            uint nPieces = br.ReadUInt32();
            uint nChildModels = br.ReadUInt32();
            uint nTris = br.ReadUInt32();
            uint nVerts = br.ReadUInt32();
            uint nVertexWeights = br.ReadUInt32();
            uint nLODs = br.ReadUInt32();
            uint nSockets = br.ReadUInt32();
            uint nWeightSets = br.ReadUInt32();
            uint nStrings = br.ReadUInt32();
            uint StringLengths = br.ReadUInt32();
            uint VertAnimDataSize = br.ReadUInt32();
            uint nAnimData = br.ReadUInt32();

            string cmdString = br.ReadStringWithUInt16Length();
            float globalRadius = br.ReadSingle();

            uint iNumEnabledOBBs = br.ReadUInt32();

            if (iNumEnabledOBBs != 0)
            {
                throw new Exception("LTB with OBB infomations are not supported");
            }

            uint numMesh = br.ReadUInt32();

            var romanisation = new McCuneReischauerRomanisation { PreserveNonKoreanText = true };

            // Parse mesh nodes
            for (int i = 0; i < numMesh; i++)
            {
                string meshName = br.ReadStringWithUInt16Length(ltbEncode);

                meshName = romanisation.RomaniseText(meshName);

                foreach (var kvp in replaceDictionary)
                    meshName = meshName.Replace(kvp.Key, kvp.Value);

                meshName = meshName.ToLower();

                uint numLod = br.ReadUInt32();

                Console.WriteLine($"{meshName} - {numLod} Lods");

                meshName = meshName.ToLower();

                br.Skip((int)numLod * 4 + 8);

                int materialIndex = -1;

                if (!ltbFile.Materials.Any(material => material.Name == meshName))
                {
                    ltbFile.Materials.Add(new SEModelMaterial
                    {
                        Name = "mtl_" + meshName,
                        MaterialData = new SEModelSimpleMaterial
                        {
                            DiffuseMap = meshName + ".png"
                        }
                    });

                    materialIndex = ltbFile.Materials.Count - 1;
                }
                else
                {
                    materialIndex = ltbFile.Materials.FindIndex(mtl => mtl.Name == meshName);
                }

                for (int iLod = 0; iLod < numLod; iLod++)
                {
                    var mesh = new SEModelMesh();
                    mesh.AddMaterialIndex(materialIndex);

                    var nNumTex = br.ReadUInt32();
                    const int MAX_PIECE_TEXTURES = 4;

                    for (int iTex = 0; iTex < MAX_PIECE_TEXTURES; iTex++)
                    {
                        // Texture index
                        br.ReadUInt32();
                    }

                    var renderStyle = br.ReadUInt32();
                    var nRenderPriority = br.ReadByte();

                    var lodType = (PieceType)br.ReadUInt32();

                    var lodSize = br.ReadUInt32();

                    if (lodSize != 0)
                    {
                        uint numVerts = br.ReadUInt32();
                        uint numTris = br.ReadUInt32();

                        uint iMaxBonesPerTri = br.ReadUInt32();
                        uint iMaxBonesPerVert = br.ReadUInt32();

                        Console.WriteLine($"    Lod {iLod}: \n        Vertex count: {numVerts}\n        Triangle count: {numTris}");

                        bool bReIndexBones = false, bUseMatrixPalettes = false;

                        if (lodType == PieceType.SkelMesh)
                            bReIndexBones = br.ReadBoolean();

                        DataType[] streamData = { (DataType)br.ReadUInt32(), (DataType)br.ReadUInt32(), (DataType)br.ReadUInt32(), (DataType)br.ReadUInt32() };

                        uint rigidBone = uint.MaxValue;

                        if (lodType == PieceType.RigidMesh)
                            rigidBone = br.ReadUInt32();
                        else if (lodType == PieceType.SkelMesh)
                            bUseMatrixPalettes = br.ReadBoolean();
                        else throw new Exception("Unsupported lod type");

                        if (bUseMatrixPalettes)
                        {
                            uint iMinBone = br.ReadUInt32();
                            uint iMaxBone = br.ReadUInt32();
                        }

                        var boneMap = new List<uint>();

                        if (bReIndexBones)
                        {
                            uint reindexBoneMapSize = br.ReadUInt32();

                            for (int iMap = 0; iMap < reindexBoneMapSize; iMap++)
                            {
                                boneMap.Add(br.ReadUInt32());
                            }
                        }

                        for (int iStream = 0; iStream < 4; ++iStream)
                        {
                            if (!streamData[iStream].HasFlag(DataType.Position))
                                continue;

                            for (int iVert = 0; iVert < numVerts; iVert++)
                            {
                                var v = new SEModelVertex();

                                if (streamData[iStream].HasFlag(DataType.Position))
                                {
                                    v.Position = new Vector3
                                    {
                                        X = br.ReadSingle(),
                                        Y = br.ReadSingle(),
                                        Z = br.ReadSingle(),
                                    };

                                    if (bFlip)
                                        v.Position.X *= -1;

                                    if (rigidBone == uint.MaxValue)
                                    {
                                        var weightSum = 0.0f;

                                        var maxWeight = bUseMatrixPalettes ? iMaxBonesPerVert : iMaxBonesPerTri;

                                        for (int iWeight = 0; iWeight < maxWeight - 1; iWeight++)
                                        {
                                            var weight = br.ReadSingle();

                                            if (weight > 1)
                                                throw new Exception("wtf");

                                            weightSum += weight;

                                            v.Weights.Add(new SEModelWeight
                                            {
                                                BoneIndex = uint.MaxValue,
                                                BoneWeight = weight
                                            });
                                        }

                                        if (1.0f - weightSum > float.Epsilon)
                                        {
                                            v.Weights.Add(new SEModelWeight
                                            {
                                                BoneIndex = uint.MaxValue,
                                                BoneWeight = 1.0f - weightSum
                                            });
                                        }

                                        if (bUseMatrixPalettes)
                                        {
                                            for (int iWeight = 0; iWeight < 4; iWeight++)
                                            {
                                                var boneIndex = br.ReadByte();

                                                if (bReIndexBones)
                                                {
                                                    boneIndex = (byte)boneMap[boneIndex];
                                                }

                                                if (v.Weights.Count > iWeight)
                                                {
                                                    v.Weights[iWeight].BoneIndex = boneIndex;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (rigidBone >= numBones || rigidBone < 0)
                                            throw new Exception("wtf");

                                        v.Weights.Add(new SEModelWeight
                                        {
                                            BoneIndex = rigidBone,
                                            BoneWeight = 1.0f
                                        });
                                    }
                                }

                                if (streamData[iStream].HasFlag(DataType.Normal))
                                {
                                    v.VertexNormal = new Vector3
                                    {
                                        X = br.ReadSingle(),
                                        Y = br.ReadSingle(),
                                        Z = br.ReadSingle(),
                                    };

                                    if (bFlip)
                                        v.VertexNormal.X *= -1;
                                }

                                if (streamData[iStream].HasFlag(DataType.Color))
                                    br.Skip(4);

                                if (streamData[iStream].HasFlag(DataType.UVSets1))
                                {
                                    v.UVSets.Add(new Vector2
                                    {
                                        X = br.ReadSingle(),
                                        Y = br.ReadSingle()
                                    });

                                    if (v.UVSets[0].X > 1.0f)
                                        v.UVSets[0].X -= 1.0f;
                                }

                                if (streamData[iStream].HasFlag(DataType.UVSets2))
                                    br.Skip(8);
                                if (streamData[iStream].HasFlag(DataType.UVSets3))
                                    br.Skip(8);
                                if (streamData[iStream].HasFlag(DataType.UVSets4))
                                    br.Skip(8);
                                if (streamData[iStream].HasFlag(DataType.BasisVectors))
                                    br.Skip(24);

                                if (v.Position == null || v.WeightCount == 0)
                                    throw new Exception("wtf");

                                mesh.AddVertex(v);
                            }
                        }

                        for (uint iTriangle = 0; iTriangle < numTris; iTriangle++)
                            mesh.AddFace(br.ReadUInt16(), br.ReadUInt16(), br.ReadUInt16());

                        if (lodType == PieceType.SkelMesh && !bUseMatrixPalettes)
                        {
                            var boneComboCount = br.ReadUInt32();

                            for (int iCombo = 0; iCombo < boneComboCount; iCombo++)
                            {
                                int m_BoneIndex_Start = br.ReadUInt16();
                                int m_BoneIndex_End = m_BoneIndex_Start + br.ReadUInt16();

                                Console.WriteLine($"        Weight Combo: {m_BoneIndex_Start} to {m_BoneIndex_End}");

                                var bones = br.ReadBytes(4);

                                uint m_iIndexIndex = br.ReadUInt32();

                                for (int iVertex = m_BoneIndex_Start; iVertex < m_BoneIndex_End; iVertex++)
                                {
                                    for (int iBone = 0; iBone < 4 && bones[iBone] != 0xFF; iBone++)
                                    {
                                        if (mesh.Verticies[iVertex].Weights.Count <= iBone)
                                            break;

                                        mesh.Verticies[iVertex].Weights[iBone].BoneIndex = bones[iBone];
                                    }
                                }
                            }
                        }

                        ltbFile.Meshes.Add(mesh);
                        br.Skip(br.ReadByte());
                    }
                }
            }

            uint[] boneTree = new uint[numBones];

            for (int i = 0; i < numBones; i++)
            {
                var boneName = br.ReadStringWithUInt16Length();
                var boneId = br.ReadByte();
                var num2 = br.ReadUInt16();

                Matrix4x4 transformMatrix = new Matrix4x4();

                for (int j = 0; j < 4; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        transformMatrix[j, k] = br.ReadSingle();
                    }
                }

                boneTree[i] = br.ReadUInt32();

                var bone = new SEModelBone
                {
                    BoneName = boneName.Replace('.', '_').Replace('-', '_').Replace(' ', '_'),
                    GlobalRotation = new Quaternion(transformMatrix),
                    GlobalPosition = new Vector3(transformMatrix)
                };

                if(bFlip)
                {
                    bone.GlobalPosition.X *= -1;

                    bone.GlobalRotation.Y *= -1;
                    bone.GlobalRotation.Z *= -1;
                }

                // rotate root bone;
                if (boneId == 0)
                {
                    bone.GlobalRotation *= globalRotation;
                }

                ltbFile.Bones[boneId] = bone;
            }

            uint[] nSubbone = new uint[numBones];
            nSubbone[0] = boneTree[0];

            ltbFile.Bones[0].BoneParent = -1;

            // Build bone tree
            for (byte i = 1; i < numBones; i++)
            {
                nSubbone[i] = boneTree[i];
                for (int j = i - 1; j >= 0; j--)
                {
                    if (nSubbone[j] > 0)
                    {
                        nSubbone[j]--;
                        ltbFile.Bones[i].BoneParent = j;
                        break;
                    }
                }
            }

            Console.WriteLine("\nInternal filenames:");
            var childModelCount = br.ReadUInt32();

            for (int i = 0; i < childModelCount; i++)
            {
                Console.WriteLine(br.ReadStringWithUInt16Length());

                br.Skip((int)br.ReadUInt32() * 4);
            }

            br.Skip(4);

            if (nAnim > 0)
            {
                var animationCount = br.ReadUInt32();

                Console.WriteLine($"\nAnimation count: {animationCount}\n");

                for (int i = 0; i < animationCount; i++)
                {
                    var seanim = new SEAnim();

                    var dim = new Vector3
                    {
                        X = br.ReadSingle(),
                        Y = br.ReadSingle(),
                        Z = br.ReadSingle(),
                    };

                    var animName = br.ReadStringWithUInt16Length();
                    Console.Write(animName);

                    var compressionType = (AnimCompressionType)br.ReadUInt32();
                    var interpolationMS = br.ReadUInt32();

                    var keyFrameCount = br.ReadUInt32();
                    Console.WriteLine($" has {keyFrameCount} keyframes");

                    for (int iKeyFrame = 0; iKeyFrame < keyFrameCount; iKeyFrame++)
                    {
                        var time = br.ReadUInt32();
                        var animString = br.ReadStringWithUInt16Length();

                        if (!string.IsNullOrEmpty(animString))
                            seanim.AddNoteTrack(animString, iKeyFrame);
                    }

                    for (byte iBone = 0; iBone < numBones; iBone++)
                    {
                        if (compressionType != AnimCompressionType.None)
                        {
                            uint pFrames = br.ReadUInt32();

                            for (int iKeyFrame = 0; iKeyFrame < pFrames; iKeyFrame++)
                            {
                                var v = new Vector3(br.ReadInt16() / 16.0, br.ReadInt16() / 16.0, br.ReadInt16() / 16.0);

                                if (bFlip)
                                    v.X *= -1;

                                seanim.AddTranslationKey(ltbFile.Bones[iBone].BoneName, iKeyFrame, v.X, v.Y, v.Z);
                            }

                            uint rFrames = br.ReadUInt32();

                            for (int iKeyFrame = 0; iKeyFrame < rFrames; iKeyFrame++)
                            {
                                var q = new Quaternion(br.ReadInt16() / 16.0, -br.ReadInt16() / 16.0, -br.ReadInt16() / 16.0, br.ReadInt16() / 16.0);

                                if (bFlip)
                                {
                                    q.Y *= -1;
                                    q.Z *= -1;
                                }

                                // rotate root bone;
                                if (iBone == 0)
                                {
                                    q *= globalRotation;
                                }

                                seanim.AddRotationKey(ltbFile.Bones[iBone].BoneName, iKeyFrame, q.X, q.Y, q.Z, q.W);
                            }
                        }
                        else if (compressionType == AnimCompressionType.None)
                        {
                            bool isVertexAnim = br.ReadBoolean();

                            if (isVertexAnim)
                            {
                                throw new Exception("Vertex animation not supported!");
                            }
                            else
                            {
                                for (int iKeyFrame = 0; iKeyFrame < keyFrameCount; iKeyFrame++)
                                {
                                    var v = new Vector3(-br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                    seanim.AddTranslationKey(ltbFile.Bones[iBone].BoneName, iKeyFrame, v.X, v.Y, v.Z);
                                }

                                for (int iKeyFrame = 0; iKeyFrame < keyFrameCount; iKeyFrame++)
                                {
                                    var q = new Quaternion(br.ReadSingle(), -br.ReadSingle(), -br.ReadSingle(), br.ReadSingle());

                                    // rotate root bone;
                                    if (iBone == 0)
                                    {
                                        q *= globalRotation;
                                    }

                                    seanim.AddRotationKey(ltbFile.Bones[iBone].BoneName, iKeyFrame, q.X, q.Y, q.Z, q.W);
                                }
                            }
                        }
                    }

                    ltbFile.Animations.Add(animName + ".seanim", seanim);
                }
            }

            return ltbFile;
        }
    }
}
