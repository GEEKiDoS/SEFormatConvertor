using SELib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SEFormatConvertor
{
    class Program
    {
        static Queue<ThreadStart> taskQuene = new Queue<ThreadStart>();
        static Thread[] tasks;

        static void Main(string[] args)
        {
            Console.WriteLine("SEFormatConvertor by GEEKiDoS");

            bool bSingleThread = args.Contains("-s");

            tasks = new Thread[bSingleThread ? 1 : Environment.ProcessorCount * 2];

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: drag files to this exe.\nAny key to exit");
                Console.ReadKey();

                return;
            }

            foreach (var arg in args)
            {
                if(Directory.Exists(arg))
                {
                    var dir = new DirectoryInfo(arg);

                    ForeachFolder(dir, dir.Parent.FullName);
                }
                else
                {
                    var fileInfo = new FileInfo(arg);

                    if (fileInfo.Exists)
                    {
                        ProcessFile(fileInfo, fileInfo.Directory.FullName);
                    }
                }
            }

            int activeTasks = 0;

            do
            {
                activeTasks = 0;

                for (int i = tasks.Length - 1; i >= 0;i --)
                {
                    if(tasks[i] != null && tasks[i].ThreadState != ThreadState.Running)
                        tasks[i] = null;
                }

                for(int i = 0; i < tasks.Length; i++)
                {
                    if(tasks[i] == null)
                    {
                        if (taskQuene.Count == 0)
                            break;

                        tasks[i] = new Thread(taskQuene.Dequeue());
                        tasks[i].Start();
                    }
                }

                foreach(var task in tasks)
                {
                    if (task != null && task.ThreadState == ThreadState.Running)
                        activeTasks++;
                }
                
                Thread.Sleep(1);
            } 
            while (activeTasks > 0);

            Console.WriteLine("Done!\nAny key to exit");
            Console.ReadLine();
        }

        static void ProcessFile(FileInfo fileInfo, string prefix)
        {
            taskQuene.Enqueue(async () =>
            {
                var ext = fileInfo.Extension.ToLower();
                
                if(ext == ".semodel")
                {
                    var dir = "converted_files/";

                    if (prefix != "")
                        dir += $"{fileInfo.FullName.Replace(prefix, "").Replace(fileInfo.Name, "")}/";

                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);


                    var semodel = SEModel.Read(fileInfo.OpenRead());

                    var smd = semodel.ToSMD();

                    File.WriteAllText($"{dir}{fileInfo.Name.Replace(fileInfo.Extension, "")}.smd", smd);
                }
                else if (ext == ".smd")
                {
                    Console.WriteLine("Converting SMD...");

                    var dir = "converted_files/";

                    if (prefix != "")
                        dir += $"{fileInfo.FullName.Replace(prefix, "").Replace(fileInfo.Name, "")}/";

                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var smd = SMDFile.Load(fileInfo);

                    if (smd.IsAnimation)
                    {
                        smd.ToSEAnim().Write(File.OpenWrite($"{dir}{fileInfo.Name.Replace(fileInfo.Extension, "")}.seanim"), false);
                    }
                    else
                    {
                        smd.ToSEModel().Write(File.OpenWrite($"{dir}{fileInfo.Name.Replace(fileInfo.Extension, "")}.semodel"));
                    }
                }
                else if (ext == ".ltb")
                {
                    Console.WriteLine($"{fileInfo.Name}");

                    var ltb = LTBFile.Read(fileInfo);

                    if (ltb != null)
                    {
                        if (!Directory.Exists($"converted_files/{fileInfo.FullName.Replace(prefix, "")}/anims/"))
                            Directory.CreateDirectory($"converted_files/{fileInfo.FullName.Replace(prefix, "")}/anims/");


                        var semodel = ltb.ToSEModel();

                        foreach (var anim in ltb.Animations)
                        {
                            anim.Value.Write(File.OpenWrite($"converted_files/{fileInfo.FullName.Replace(prefix, "")}/anims/{anim.Key}"), false);
                        }

                        semodel.Write(File.OpenWrite($"converted_files/{fileInfo.FullName.Replace(prefix, "")}/{fileInfo.Name.Replace(fileInfo.Extension, ".semodel")}"));
                    }
                }
                else if (ext == ".dtx")
                {
                    if (!Directory.Exists($"converted_files/_images/{fileInfo.Directory.FullName.Replace(prefix, "")}"))
                        Directory.CreateDirectory($"converted_files/_images/{fileInfo.Directory.FullName.Replace(prefix, "")}");

                    var dtxFile = await DTXFile.Load(fileInfo);
                    dtxFile.Save(File.OpenWrite($"converted_files/_images/{fileInfo.FullName.Replace(fileInfo.Extension, ".png").Replace(prefix, "")}"));
                }
                else if (ext == ".dat")
                {
                    var datFile = new DATFileV85(new SELib.Utilities.ExtendedBinaryReader(fileInfo.OpenRead()));
                }
                else if(ext == ".ogg")
                {
                    var br = new BinaryReader(fileInfo.OpenRead());

                    uint rate = 0;
                    ulong length = 0;

                    br.BaseStream.Seek(-4, SeekOrigin.End);

                    while (length == 0)
                    {
                        if (Encoding.ASCII.GetString(br.ReadBytes(4)) == "OggS")
                        {
                            br.BaseStream.Seek(2, SeekOrigin.Current);
                            length = br.ReadUInt64();
                        }

                        br.BaseStream.Seek(-5, SeekOrigin.Current);
                    }

                    br.BaseStream.Seek(0, SeekOrigin.Begin);

                    while(rate == 0)
                    {
                        if (Encoding.ASCII.GetString(br.ReadBytes(6)) == "vorbis")
                        {
                            br.BaseStream.Seek(5, SeekOrigin.Current);
                            rate = br.ReadUInt32();
                        }

                        br.BaseStream.Seek(-5, SeekOrigin.Current);
                    }

                    Console.WriteLine($"{fileInfo.Name} has {length} Samples, Sample rate {rate}, time is {length / (float)rate} sec");
                }
                else
                {
                    Console.WriteLine($"{fileInfo.Name} - Unsupported format!");
                }
            });
        }

        static void ForeachFolder(DirectoryInfo dir, string prefix)
        {
            foreach (var item in dir.GetFiles())
                ProcessFile(item, prefix);

            foreach (var sdir in dir.GetDirectories())
                ForeachFolder(sdir, prefix);
        }
    }
}
