using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SELib;

namespace SEFormatConvertor
{
    public static class SEExt
    {
        public static string ToSMD(this SEModel semodel)
        {
            var buffer = new StringBuilder();
            buffer.AppendLine("// Converted by SEFormat Convertor");
            buffer.AppendLine("version 1");

            #region Bones
            buffer.AppendLine("nodes");

            for(int i = 0; i < semodel.BoneCount; i++)
                buffer.AppendLine($"  {i} \"{semodel.Bones[i].BoneName}\" {semodel.Bones[i].BoneParent}");

            buffer.AppendLine("end");
            #endregion

            #region Bone transforms
            buffer.AppendLine("skeleton");
            {
                buffer.AppendLine("  time 0");

                for (int i = 0; i < semodel.BoneCount; i++)
                {
                    var bone = semodel.Bones[i];
                    var eularAngle = bone.LocalRotation.ToEulerAngles();

                    buffer.AppendLine($"    {i} {bone.LocalPosition.X:F6} {bone.LocalPosition.Y:F6} {bone.LocalPosition.Z:F6} {eularAngle.X:F6} {eularAngle.Y:F6} {eularAngle.Z:F6}");
                }
            }
            buffer.AppendLine("end");
            #endregion

            #region Triangles
            buffer.AppendLine("triangles");

            foreach (var mesh in semodel.Meshes)
            {
                var mtlName = semodel.Materials[mesh.MaterialReferenceIndicies[0]].Name;

                foreach(var face in mesh.Faces)
                {
                    buffer.AppendLine(mtlName);

                    SEModelVertex[] verts = { mesh.Verticies[(int)face.FaceIndex1], mesh.Verticies[(int)face.FaceIndex2], mesh.Verticies[(int)face.FaceIndex3] };

                    foreach(var vert in verts)
                    {
                        buffer.Append($"  {vert.Weights[0].BoneIndex} {vert.Position.X:F6} {vert.Position.Y:F6} {vert.Position.Z:F6} {vert.VertexNormal.X:F6} {vert.VertexNormal.Y:F6} {vert.VertexNormal.Z:F6} {vert.UVSets[0].X:F6} {vert.UVSets[0].X:F6}");

                        if(vert.WeightCount > 1)
                        {
                            buffer.Append(vert.WeightCount - 1);
                            for(int i = 1; i < vert.WeightCount; i++)
                            {
                                buffer.Append($" {vert.Weights[i].BoneIndex} {vert.Weights[i].BoneWeight:F6}");
                            }
                        }

                        buffer.AppendLine();
                    }
                }
            }

            buffer.AppendLine("end");
            #endregion

            return buffer.ToString();
        }
    }
}
