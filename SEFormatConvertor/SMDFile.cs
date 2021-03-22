using SELib;
using SELib.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEFormatConvertor
{
    public class SMDFile
    {
        /// <summary>
        /// A list of bones, in order by index
        /// </summary>
        public List<SEModelBone> Bones { get; private set; }
        /// <summary>
        /// A list of meshes, in order
        /// </summary>
        public List<SEModelMesh> Meshes { get; private set; }
        /// <summary>
        /// A list of materials, in order
        /// </summary>
        public List<SEModelMaterial> Materials { get; private set; }
        /// <summary>
        /// A list of animations
        /// </summary>
        public SEAnim Animation { get; private set; }

        public List<string> Namespaces { get; private set; }

        public static readonly Dictionary<string, string> boneReplaceDictionary = new Dictionary<string, string>
        {
            { ".", ":" },
            { "-", "_" },
            { " ", "_" },
            { "~","_" },
        };

        public static readonly Dictionary<string, string> replaceDictionary = new Dictionary<string, string>
        {
            { ".", "_" },
            { "-", "_" },
            { " ", "_" },
            { "~","_" },
        };

        private SMDFile()
        {
            Bones = new List<SEModelBone>();
            Meshes = new List<SEModelMesh>();
            Materials = new List<SEModelMaterial>();

            Animation = new SEAnim { AnimType = AnimationType.Absolute };
            Namespaces = new List<string>();
        }

        public static float F(string str) => float.Parse(str, CultureInfo.InvariantCulture.NumberFormat);

        public bool IsAnimation { get => Meshes.Count == 0; }

        public SEModel ToSEModel()
        {
            var semdl = new SEModel();

            foreach (var bone in Bones)
                semdl.AddBone(bone.BoneName, bone.BoneParent, bone.GlobalPosition, bone.GlobalRotation, bone.LocalPosition, bone.LocalRotation, bone.Scale);

            foreach (var material in Materials)
                semdl.AddMaterial(material);

            foreach (var mesh in Meshes)
                semdl.AddMesh(mesh);

            semdl.GenerateGlobalPositions(true, true);

            return semdl;
        }

        public SEAnim ToSEAnim() => Animation;

        public static string ProcessString(string input)
        {
            string output = "";

            for(int i = 0; i < input.Length; i++)
            {
                if (input[i] < 128)
                    output += input[i];
                else
                    output += "_";
            }

            return output;
        }

        public static SMDFile Load(FileInfo info)
        {
            var file = new SMDFile();

            using (var sr = new StreamReader(info.OpenRead()))
            {
                int parseMode = -1;
                int curFrame = -1;

                int lnCounter = -1;

                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();

                    if (line.StartsWith("//"))
                        continue;

                    var commitPos = line.IndexOf("//");

                    if (commitPos != -1)
                    {
                        line = line.Substring(0, commitPos);
                    } 

                    switch (line)
                    {
                        case "nodes":
                            parseMode = 0;
                            continue;
                        case "skeleton":
                            parseMode = 1;
                            continue;
                        case "triangles":
                            parseMode = 2;
                            continue;
                        case "end":
                            if(parseMode == 0) 
                            { 
                                foreach(var ns in file.Namespaces)
                                {
                                    foreach (var bone in file.Bones)
                                    {
                                        if (bone.BoneName == ns)
                                            bone.BoneName = $"{ns}:{ns}";
                                    }
                                }
                            }
                            else if(parseMode == 2)
                            {
                                if (file.Meshes.Count != 0)
                                {
                                    var faceCount = file.Meshes.Last().VertexCount / 3;

                                    for (uint i = 0; i < faceCount; i++)
                                        file.Meshes.Last().AddFace(i * 3, i * 3 + 1, i * 3 + 2);
                                }
                            }

                            parseMode = -1;
                            continue;
                    }

                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parseMode == 0) // bone list
                    {
                        int firstPos = line.IndexOf('"');
                        int nextPos = line.IndexOf('"', firstPos + 1);

                        string boneName = ProcessString(line.Substring(firstPos + 1, nextPos - firstPos - 1));

                        foreach (var kvp in boneReplaceDictionary)
                            boneName = boneName.Replace(kvp.Key, kvp.Value);

                        boneName = "c_" + boneName;

                        // this bone is under a namespace
                        if(boneName.Contains(':'))
                        {
                            var bns = boneName.Split(':')[0];

                            if (!file.Namespaces.Any(ns => ns == bns))
                                file.Namespaces.Add(bns);
                        }

                        int boneId = int.Parse(parts[0]);
                        int parentBoneId = int.Parse(parts[parts.Length - 1]);

                        var bone = new SEModelBone
                        {
                            BoneName = boneName,
                            BoneParent = parentBoneId,
                        };

                        file.Bones.Add(bone);
                    }
                    else if(parseMode == 1) // keyframes
                    {
                        if (line.Contains("time"))
                        {
                            curFrame = int.Parse(parts[1]);
                            continue;
                        }

                        int index = int.Parse(parts[0]);

                        var pos = new Vector3(F(parts[1]), F(parts[2]), F(parts[3]));
                        file.Animation.AddTranslationKey(file.Bones[index].BoneName, curFrame, pos.X, pos.Y, pos.Z);

                        var rot = Quaternion.FromEulerAngles(F(parts[4]), F(parts[5]), F(parts[6]));
                        file.Animation.AddRotationKey(file.Bones[index].BoneName, curFrame, rot.X, rot.Y, rot.Z, rot.W);

                        if (curFrame == 0)
                        {
                            file.Bones[index].LocalPosition = pos;
                            file.Bones[index].LocalRotation = rot;
                        }
                    }
                    else if(parseMode == 2)
                    {
                        if(++lnCounter % 4 == 0) // Read material line
                        {
                            var mtlName = ProcessString(line.Contains('.') ? line.Substring(0, line.LastIndexOf('.')) : line);

                            foreach (var kvp in replaceDictionary)
                                mtlName = mtlName.Replace(kvp.Key, kvp.Value);

                            mtlName = "mtl_" + mtlName;

                            if (!file.Materials.Any(mtl => mtl.Name == mtlName))
                            {
                                if (file.Meshes.Count != 0)
                                {
                                    var faceCount = file.Meshes.Last().VertexCount / 3;

                                    for (uint i = 0; i < faceCount; i++)
                                        file.Meshes.Last().AddFace(i * 3, i * 3 + 1, i * 3 + 2);
                                }

                                file.Meshes.Add(new SEModelMesh
                                {
                                    MaterialReferenceIndicies = { file.Materials.Count }
                                });

                                file.Materials.Add(new SEModelMaterial
                                {
                                    Name = mtlName,
                                    MaterialData = new SEModelSimpleMaterial
                                    {
                                        DiffuseMap = mtlName + ".png"
                                    }
                                });
                            }

                            continue;
                        }

                        var vertex = new SEModelVertex
                        {
                            Position = new Vector3(F(parts[1]), F(parts[2]), F(parts[3])),
                            VertexNormal = new Vector3(F(parts[4]), F(parts[5]), F(parts[6])),
                            UVSets = { new Vector2(F(parts[7]), 1.0f - F(parts[8])) },
                        };

                        float weightLeft = 1.0f;

                        if (parts.Length > 9)
                        {
                            int weightCount = int.Parse(parts[9]);
                            for (int i = 0; i < weightCount; i++)
                            {
                                vertex.Weights.Add(new SEModelWeight
                                {
                                    BoneIndex = uint.Parse(parts[10 + i * 2]),
                                    BoneWeight = F(parts[11 + i * 2]),
                                });

                                weightLeft -= vertex.Weights.Last().BoneWeight;
                            }
                        }

                        if(weightLeft >= 0.001)
                        {
                            vertex.Weights.Add(new SEModelWeight
                            {
                                BoneIndex = uint.Parse(parts[0]),
                                BoneWeight = weightLeft
                            });
                        }

                        file.Meshes.Last().AddVertex(vertex);
                    }
                }
            }

            return file;
        }
    }
}
