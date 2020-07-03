using SELib;
using SELib.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEFormatConvertor
{
    static class SEModelExt
    {
        const float UNIT_CONVERTION = 2.54f;

        public static float L(string str)
        {
            return float.Parse(str) * UNIT_CONVERTION;
        }
        public static object FromSMD(string smdData)
        {
            var bones = new List<SEModelBone>();
            var materials = new List<SEModelMaterial>();
            var meshs = new List<SEModelMesh>();
            var vertexes = new List<SEModelVertex>();

            var posKeyframes = new List<Vector3>();
            var rotKeyframes = new List<Quaternion>();

            var anim = new SEAnim();
            var curFrame = -1;

            var mode = -1;

            int mode2lineCounter = -1;

            var lines = smdData.Replace("\r", "").Split('\n');
            foreach(var line in lines)
            {
                if (line == "nodes")
                {
                    mode = 0;
                    continue;
                }
                if (line == "skeleton")
                { 
                    mode = 1;
                    continue;
                }
                if (line == "triangles")
                {
                    mode = 2;
                    continue;
                }
                if(line == "end")
                {
                    mode = -1;
                    continue;
                }

                if(mode == 0)
                {
                    int firstPos = line.IndexOf('"');
                    int nextPos = line.IndexOf('"', firstPos + 1);

                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    string boneName = line.Substring(firstPos + 1, nextPos - firstPos - 1).Replace('.', '_').Replace('-', '_').Replace(' ', '_');
                    int boneId = int.Parse(parts[0]);
                    int parentBoneId = int.Parse(parts[parts.Length - 1]);

                    var bone = new SEModelBone
                    {
                        BoneName = boneName,
                        BoneParent = parentBoneId,
                    };

                    bones.Add(bone);
                }

                if(mode == 1)
                {
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if(line.Contains("time"))
                    {
                        curFrame = int.Parse(parts[1]);
                        continue;
                    }

                    int index = int.Parse(parts[0]);

                    var pos = new Vector3(L(parts[1]), L(parts[2]), L(parts[3]));
                    anim.AddTranslationKey(bones[index].BoneName, curFrame, pos.X, pos.Y, pos.Z);
                    
                    var rot = Quaternion.FromEulerAngles(float.Parse(parts[4]), float.Parse(parts[5]), float.Parse(parts[6]));
                    anim.AddRotationKey(bones[index].BoneName, curFrame, rot.X, rot.Y, rot.Z, rot.W);

                    if(curFrame == 0)
                    {
                        bones[index].LocalPosition = pos;
                        bones[index].LocalRotation = rot;
                    }
                }

                if(mode == 2)
                {
                    mode2lineCounter++;

                    if(mode2lineCounter % 4 == 0)
                    {
                        var mtlName = line.Contains('.') ? line.Substring(0, line.LastIndexOf('.')) : line;

                        mtlName = mtlName.Replace('.', '_').Replace('-', '_').Replace(' ', '_');

                        if (!materials.Any(mtl => mtl.Name == mtlName))
                        {
                            if(meshs.Count != 0)
                            {
                                var faceCount = meshs.Last().VertexCount / 3;

                                for (uint i = 0; i < faceCount; i++)
                                    meshs.Last().AddFace(i * 3, i * 3 + 1, i * 3 + 2);
                            }
                            
                            meshs.Add(new SEModelMesh
                            {
                                MaterialReferenceIndicies = { materials.Count }
                            });

                            materials.Add(new SEModelMaterial
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

                    var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    var vertex = new SEModelVertex
                    {
                        Position = new Vector3(L(parts[1]), L(parts[2]), L(parts[3])),
                        VertexNormal = new Vector3(float.Parse(parts[4]), float.Parse(parts[5]), float.Parse(parts[6])),
                        UVSets = { new Vector2(float.Parse(parts[7]), float.Parse(parts[8])) },
                    };

                    float weightSum = 0.0f;

                    if (parts.Length > 9)
                    {
                        int weightCount = int.Parse(parts[9]);
                        for (int i = 0; i < weightCount; i++)
                        {
                            vertex.Weights.Add(new SEModelWeight
                            {
                                BoneIndex = uint.Parse(parts[10 + i * 2]),
                                BoneWeight = float.Parse(parts[11 + i * 2]),
                            });

                            weightSum += vertex.Weights.Last().BoneWeight;
                        }
                    }

                    vertex.Weights.Add(new SEModelWeight
                    {
                        BoneIndex = uint.Parse(parts[0]),
                        BoneWeight = 1.0f - weightSum
                    });

                    meshs.Last().AddVertex(vertex);
                }
            }

            if(meshs.Count == 0)
            {
                return anim;
            }

            var mdl = new SEModel();
            foreach(var bone in bones)
            {
                mdl.AddBone(bone.BoneName, bone.BoneParent,
                     bone.GlobalPosition, bone.GlobalRotation,
                     bone.LocalPosition, bone.LocalRotation, bone.Scale);
            }

            foreach(var mtl in materials)
            {
                mdl.AddMaterial(mtl);
            }

            foreach(var mesh in meshs)
            {
                mdl.AddMesh(mesh);
            }

            mdl.GenerateGlobalPositions(true, true);

            return mdl;
        }
    }
}
