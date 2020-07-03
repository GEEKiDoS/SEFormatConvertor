using SELib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEFormatConvertor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SEFormatConvertor by GEEKiDoS");

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: drag files to this exe.\nAny key to exit");
                Console.ReadKey();

                return;
            }

            foreach (var arg in args)
            {
                var fileInfo = new FileInfo(arg);
                
                if(fileInfo.Exists)
                {
                    var ext = fileInfo.Extension.ToLower();
                    if (ext == ".smd")
                    {
                        Console.WriteLine("Converting SMD...");

                        if(!Directory.Exists($"converted_files/{fileInfo.Name}/"))
                            Directory.CreateDirectory($"converted_files/{fileInfo.Name}/");

                        var semdl = SEModelExt.FromSMD(fileInfo.FullName) as SEModel;
                        semdl.Write(File.OpenWrite($"converted_files/{fileInfo.Name}/{fileInfo.Name.Replace(fileInfo.Extension, ".semodel")}"));
                    }
                    else if(ext == ".ltb" )
                    {
                        Console.WriteLine("Converting LTB...");

                        if (!Directory.Exists($"converted_files/{fileInfo.Name}/anims/"))
                            Directory.CreateDirectory($"converted_files/{fileInfo.Name}/anims/");

                        var ltb = LTBFile.Read(fileInfo.FullName);
                        var semodel = ltb.ToSEModel();

                        foreach (var anim in ltb.Animations)
                        {
                            anim.Value.Write(File.OpenWrite($"converted_files/{fileInfo.Name}/anims/{anim.Key}"), false);
                        }

                        semodel.Write(File.OpenWrite($"converted_files/{fileInfo.Name}/{fileInfo.Name.Replace(fileInfo.Extension, ".semodel")}"));
                    }
                    else
                    {
                        Console.WriteLine("Unsupported format!");
                    }
                }

                
            }

            Console.WriteLine("Done!\nAny key to exit");
            Console.ReadLine();
        }
    }
}
