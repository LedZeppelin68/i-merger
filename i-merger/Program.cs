using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Security.Cryptography;

namespace i_merger
{
    class Program
    {
        static string[] zero_hashes =
        {
            "c99a74c555371a433d121f551d6c6398",
            "b40791e224bd425c59f005551da11645",
            "9e297efc7a522480ef89a4a7f39ce560"
        };

        struct dir_files
        {
            public string directory;
            public List<string> files;
        };

        struct stats
        {
            public int total;
            public int dupes;
        }

        static byte[] standart_sync = { 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00 };

        static void Main(string[] args)
        {
            foreach (string working_dir in args)
            {
                //проверяю на директорию
                if (new FileInfo(working_dir).Attributes != FileAttributes.Directory) return;

                List<string> directories = new List<string>();
                directories.AddRange(Directory.GetDirectories(working_dir));
                if (directories.Count == 0) directories.Add(working_dir);

                List<dir_files> dirandfiles = new List<dir_files>();
                foreach (string directory in directories)
                {
                    dir_files df = new dir_files();
                    df.directory = directory;
                    df.files = new List<string>(Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories));

                    dirandfiles.Add(df);
                }

                XmlDocument datafile = new XmlDocument();
                datafile.XmlResolver = null;
                datafile.LoadXml("<datafile />");

                string datafile_name = new DirectoryInfo(working_dir).Name;

                //создать структуру для писателей
                BinaryWriterCollection.BinaryWriters writers = new BinaryWriterCollection().Create(working_dir);

                //Cuesheet для аудио
                List<long> audio_offsets = new List<long>();
                long temp_offset = 0;

                //создаю словари для дупликатов
                DupesDictionary dupes = new DupesDictionary();
                MD5Collection.MD5Hash md5_hashes = new MD5Collection().Create();

                //создаю курсоры
                OffsetCursor cursors = new OffsetCursor();

                //string test = writers.fs_form1.Name;

                //словарь для дубликатов карт
                Dictionary<string, long> map_dupes = new Dictionary<string, long>();

                foreach (dir_files df in dirandfiles)
                {
                    Console.WriteLine(Path.GetFileName(df.directory));

                    XmlElement game = datafile.CreateElement("game");
                    string directory_name = new DirectoryInfo(df.directory).Name;

                    game.SetAttribute("name", directory_name);
                    //получаю список файлов
                    //string[] files = Directory.GetFiles(df.directory, "*.*", SearchOption.AllDirectories);

                    //начинаю разбирать каждый файл

                    foreach (string file in df.files)
                    {
                        Console.WriteLine("file: " + Path.GetFileName(file));

                        stats file_statistic = new stats();

                        //сейчас заведомо известен тип образа
                        //до этого надо запустить writer
                        string file_type = ImageType.FindOutType(file);
                        string form1_data_md5 = string.Empty;
                        string form2_data_md5 = string.Empty;
                        string cdda_data_md5 = string.Empty;

                        string file_name = file.Replace(df.directory + Path.DirectorySeparatorChar, "");

                        //string test_file_name = Path.

                        BinaryWriter map_for_file = new BinaryWriter(new MemoryStream());

                        MD5 file_md5 = MD5.Create();

                        XmlElement rom = datafile.CreateElement("rom");
                        rom.SetAttribute("name", file_name);
                        rom.SetAttribute("type", file_type);

                        using (BinaryReader br = new BinaryReader(new FileStream(file, FileMode.Open)))
                        {
                            rom.SetAttribute("size", br.BaseStream.Length.ToString());

                            long map_offset = writers.map.BaseStream.Position;

                            switch (file_type)
                            {
                                case "file":
                                    long temp_size = br.BaseStream.Length;
                                    while (br.BaseStream.Position != br.BaseStream.Length)
                                    {
                                        byte[] temp_block = new byte[2048];
                                        if (temp_size > 2048)
                                        {
                                            temp_block = br.ReadBytes(2048);
                                            temp_size -= 2048;

                                            file_md5.TransformBlock(temp_block, 0, 2048, null, 0);
                                        }
                                        else
                                        {
                                            br.ReadBytes((int)temp_size).CopyTo(temp_block, 0);

                                            file_md5.TransformBlock(temp_block, 0, (int)temp_size, null, 0);
                                        }


                                        form1_data_md5 = BitConverter.ToString(MD5.Create().ComputeHash(temp_block)).Replace("-", "").ToLower();
                                        if (zero_hashes.Contains(form1_data_md5))
                                        {
                                            map_for_file.Write((uint)0xffffffff);

                                            file_statistic.total += 2048;
                                        }
                                        else
                                        {
                                            if (!dupes.form1.ContainsKey(form1_data_md5))
                                            {
                                                dupes.form1.Add(form1_data_md5, cursors.form1);
                                                map_for_file.Write(cursors.form1++);
                                                writers.form1.Write(temp_block);

                                                md5_hashes.form1.TransformBlock(temp_block, 0, 2048, null, 0);

                                                file_statistic.total += 2048;
                                            }
                                            else
                                            {
                                                map_for_file.Write(dupes.form1[form1_data_md5]);

                                                file_statistic.dupes += 2048;
                                            }
                                        }
                                    }

                                    break;
                                case "2048":
                                    while (br.BaseStream.Position != br.BaseStream.Length)
                                    {
                                        byte[] temp_block = br.ReadBytes(2048);

                                        file_md5.TransformBlock(temp_block, 0, 2048, null, 0);

                                        form1_data_md5 = BitConverter.ToString(MD5.Create().ComputeHash(temp_block)).Replace("-", "").ToLower();
                                        if (zero_hashes.Contains(form1_data_md5))
                                        {
                                            map_for_file.Write((uint)0xffffffff);

                                            file_statistic.total += 2048;
                                        }
                                        else
                                        {
                                            if (!dupes.form1.ContainsKey(form1_data_md5))
                                            {
                                                dupes.form1.Add(form1_data_md5, cursors.form1);
                                                map_for_file.Write(cursors.form1++);
                                                writers.form1.Write(temp_block);

                                                md5_hashes.form1.TransformBlock(temp_block, 0, 2048, null, 0);

                                                file_statistic.total += 2048;
                                            }
                                            else
                                            {
                                                map_for_file.Write(dupes.form1[form1_data_md5]);

                                                file_statistic.dupes += 2048;
                                            }
                                        }
                                    }
                                    break;
                                case "2352":
                                case "pcm":
                                    while (br.BaseStream.Position != br.BaseStream.Length)
                                    {
                                        long current_position = br.BaseStream.Position;
                                        //считываю каждый сектор образа, размер сектора зависит от типа образа, ISO или RAW
                                        byte[] temp_block = br.ReadBytes(2352);

                                        bool data_sector = CompareChain(standart_sync, temp_block);

                                        if (data_sector)
                                        {
                                            file_md5.TransformBlock(temp_block, 0, 2352, null, 0);
                                            //12 байт синхра, 3 байта MSF, 1 байт MODE, от MODE зависит наличие субхедера и т.д.

                                            byte mode = temp_block[15];

                                            //скидываю в карту MSF и MODE


                                            //выбираю, как разбирать образ
                                            switch (mode)
                                            {
                                                case 1:
                                                    map_for_file.Write(mode);
                                                    map_for_file.Write(temp_block, 12, 3);

                                                    form1_data_md5 = BitConverter.ToString(MD5.Create().ComputeHash(temp_block, 16, 2048)).Replace("-", "").ToLower();
                                                    if (zero_hashes.Contains(form1_data_md5))
                                                    {
                                                        map_for_file.Write((uint)0xffffffff);
                                                    }
                                                    else
                                                    {
                                                        if (!dupes.form1.ContainsKey(form1_data_md5))
                                                        {
                                                            dupes.form1.Add(form1_data_md5, cursors.form1);
                                                            map_for_file.Write(cursors.form1++);
                                                            writers.form1.Write(temp_block, 16, 2048);

                                                            md5_hashes.form1.TransformBlock(temp_block, 16, 2048, null, 0);
                                                        }
                                                        else
                                                        {
                                                            map_for_file.Write(dupes.form1[form1_data_md5]);
                                                        }
                                                    }
                                                    break;
                                                case 2:
                                                    //делится на form1 и form2, 2048 байт в секторе или 2324 байта в секторе
                                                    //тип сектора проверяется по субхедеру
                                                    //00 00 20 00 00 00 20 00 form2 subheader, по 0x20

                                                    //проверки можно здесь сделать

                                                    mode |= TestSub(temp_block);

                                                    int form = temp_block[18] & 0x20;

                                                    switch (form)
                                                    {
                                                        default:
                                                            break;
                                                        case 0x20:
                                                            mode |= TestNoEdc(temp_block);
                                                            break;
                                                    }

                                                    map_for_file.Write(mode);
                                                    map_for_file.Write(temp_block, 12, 3);
                                                    //скидываю в карту субхедер
                                                    map_for_file.Write(temp_block, 16, 8);


                                                    switch (form)
                                                    {
                                                        default:
                                                            form1_data_md5 = BitConverter.ToString(MD5.Create().ComputeHash(temp_block, 24, 2048)).Replace("-", "").ToLower();
                                                            if (zero_hashes.Contains(form1_data_md5))
                                                            {
                                                                map_for_file.Write((uint)0xffffffff);

                                                                file_statistic.total += 2352;
                                                            }
                                                            else
                                                            {
                                                                if (!dupes.form1.ContainsKey(form1_data_md5))
                                                                {
                                                                    dupes.form1.Add(form1_data_md5, cursors.form1);
                                                                    map_for_file.Write(cursors.form1++);
                                                                    writers.form1.Write(temp_block, 24, 2048);

                                                                    md5_hashes.form1.TransformBlock(temp_block, 24, 2048, null, 0);

                                                                    file_statistic.total += 2352;
                                                                }
                                                                else
                                                                {
                                                                    map_for_file.Write(dupes.form1[form1_data_md5]);

                                                                    file_statistic.dupes += 2352;
                                                                }
                                                            }
                                                            break;
                                                        case 0x20:
                                                            form2_data_md5 = BitConverter.ToString(MD5.Create().ComputeHash(temp_block, 24, 2324)).Replace("-", "").ToLower();
                                                            if (zero_hashes.Contains(form2_data_md5))
                                                            {
                                                                map_for_file.Write((uint)0xffffffff);

                                                                file_statistic.total += 2352;
                                                            }
                                                            else
                                                            {
                                                                if (!dupes.form2.ContainsKey(form2_data_md5))
                                                                {
                                                                    dupes.form2.Add(form2_data_md5, cursors.form2);
                                                                    map_for_file.Write(cursors.form2++);
                                                                    writers.form2.Write(temp_block, 24, 2324);

                                                                    md5_hashes.form2.TransformBlock(temp_block, 24, 2324, null, 0);

                                                                    file_statistic.total += 2352;
                                                                }
                                                                else
                                                                {
                                                                    map_for_file.Write(dupes.form2[form2_data_md5]);

                                                                    file_statistic.dupes += 2352;
                                                                }
                                                            }
                                                            break;
                                                    }

                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            //audio процедура
                                            //диапазон аудио; курсор на начало аудио сектора 
                                            br.BaseStream.Seek(current_position, SeekOrigin.Begin);

                                            int audio_size = 0;

                                            while (br.BaseStream.Position != br.BaseStream.Length)
                                            {
                                                byte[] audio = br.ReadBytes(2352);

                                                if (!CompareChain(standart_sync, audio)) { audio_size += 2352; }
                                                else { break; }
                                            }
                                            long audio_end = current_position + audio_size;

                                            br.BaseStream.Seek(current_position, SeekOrigin.Begin);

                                            bool last_audio_block = false;
                                            int last_audio_block_size = 0;

                                            while (br.BaseStream.Position != audio_end)
                                            {
                                                int null_samples = 0;
                                                UInt32 sample = 0;

                                                while ((sample == 0) & (br.BaseStream.Position != audio_end))
                                                {
                                                    current_position = br.BaseStream.Position;
                                                    sample = br.ReadUInt32();
                                                    if (sample == 0) null_samples++;
                                                }

                                                if (null_samples != 0)
                                                {
                                                    map_for_file.Write((byte)(0x10));
                                                    map_for_file.Write(null_samples);

                                                    //md5 нулевых сэмплов
                                                    for (int x = 0; x < 4; x++)
                                                    {
                                                        file_md5.TransformBlock(new byte[null_samples], 0, null_samples, null, 0);
                                                    }
                                                }

                                                //audio_offsets.Add(writers.cdda.BaseStream.Position - 44);

                                                if (br.BaseStream.Position != audio_end)
                                                {
                                                    br.BaseStream.Position = current_position;

                                                    //temp_offset = writers.cdda.BaseStream.Position - 44;

                                                    audio_offsets.Add(writers.cdda.BaseStream.Position - 44);

                                                    long audio_range = audio_end - current_position;
                                                    while (audio_range != 0)
                                                    {
                                                        current_position = br.BaseStream.Position;
                                                        byte[] temp_audio_block = new byte[2352];

                                                        if (audio_range >= 2352)
                                                        {
                                                            temp_audio_block = br.ReadBytes(2352);
                                                            audio_range -= 2352;

                                                            //file_md5.TransformBlock(temp_audio_block, 0, 2352, null, 0);

                                                            cdda_data_md5 = BitConverter.ToString(MD5.Create().ComputeHash(temp_audio_block)).Replace("-", "").ToLower();

                                                            if (zero_hashes.Contains(cdda_data_md5))
                                                            {
                                                                br.BaseStream.Seek(current_position, SeekOrigin.Begin);

                                                                break;
                                                            }
                                                            else
                                                            {
                                                                file_md5.TransformBlock(temp_audio_block, 0, 2352, null, 0);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            br.ReadBytes((int)audio_range).CopyTo(temp_audio_block, 0);

                                                            last_audio_block = true;
                                                            last_audio_block_size = (int)audio_range;

                                                            audio_range -= audio_range;
                                                            cdda_data_md5 = BitConverter.ToString(MD5.Create().ComputeHash(temp_audio_block)).Replace("-", "").ToLower();
                                                            if (zero_hashes.Contains(cdda_data_md5))
                                                            {
                                                                br.BaseStream.Seek(current_position, SeekOrigin.Begin);

                                                                break;
                                                            }
                                                            else
                                                            {
                                                                file_md5.TransformBlock(temp_audio_block, 0, last_audio_block_size, null, 0);
                                                            }
                                                        }

                                                        if (!dupes.cdda.ContainsKey(cdda_data_md5))
                                                        {
                                                            dupes.cdda.Add(cdda_data_md5, cursors.cdda);
                                                            if (!last_audio_block)
                                                            {
                                                                map_for_file.Write((byte)0);
                                                                map_for_file.Write(cursors.cdda++);
                                                            }
                                                            else
                                                            {
                                                                map_for_file.Write((byte)0x20);
                                                                map_for_file.Write(cursors.cdda++);
                                                                map_for_file.Write(last_audio_block_size);
                                                            }
                                                            writers.cdda.Write(temp_audio_block);

                                                            md5_hashes.cdda.TransformBlock(temp_audio_block, 0, 2352, null, 0);
                                                        }
                                                        else
                                                        {
                                                            if (!last_audio_block)
                                                            {
                                                                map_for_file.Write((byte)0);
                                                                map_for_file.Write(dupes.cdda[cdda_data_md5]);
                                                            }
                                                            else
                                                            {
                                                                map_for_file.Write((byte)0x20);
                                                                map_for_file.Write(dupes.cdda[cdda_data_md5]);
                                                                map_for_file.Write(last_audio_block_size);
                                                            }
                                                        }

                                                    }
                                                }
                                            }
                                        }
                                    }
                                    break;
                            }


                            file_md5.TransformFinalBlock(new byte[0], 0, 0);

                            string file_hash = BitConverter.ToString(file_md5.Hash).Replace("-", "").ToLower();

                            rom.SetAttribute("md5", file_hash);

                            if (!map_dupes.ContainsKey(file_hash))
                            {
                                rom.SetAttribute("map", map_offset.ToString());
                                map_dupes.Add(file_hash, map_offset);

                                map_for_file.BaseStream.Position = 0;
                                map_for_file.BaseStream.CopyTo(writers.map.BaseStream);
                            }
                            else
                            {
                                rom.SetAttribute("map", map_dupes[file_hash].ToString());
                            }

                            //datafile.DocumentElement.AppendChild(rom);
                        }

                        game.AppendChild(rom);

                        //Console.WriteLine(string.Format("Saved: {0}%", Math.Round((((double)file_statistic.dupes / (double)file_statistic.total)) * 100), 2));
                    }
                    datafile.DocumentElement.AppendChild(game);
                }
                XmlElement partition = datafile.CreateElement("partition");

                //string file_name_mask = datafile_name

                if (writers.form1.BaseStream.Length > 0)
                {
                    XmlElement form1_partition = datafile.CreateElement("form1");
                    md5_hashes.form1.TransformFinalBlock(new byte[0], 0, 0);
                    form1_partition.SetAttribute("name", datafile_name + ".form1");
                    form1_partition.SetAttribute("md5", BitConverter.ToString(md5_hashes.form1.Hash).Replace("-", "").ToLower());
                    form1_partition.SetAttribute("size", writers.form1.BaseStream.Length.ToString());
                    partition.AppendChild(form1_partition);
                    string name = writers.fs_form1.Name;
                    writers.form1.Dispose();
                    File.Move(name, Path.Combine(working_dir, datafile_name + ".form1"));
                }
                else
                {
                    string name = writers.fs_form1.Name;
                    writers.form1.Dispose();
                    File.Delete(name);
                }
                if (writers.form2.BaseStream.Length > 0)
                {
                    XmlElement form2_partition = datafile.CreateElement("form2");
                    md5_hashes.form2.TransformFinalBlock(new byte[0], 0, 0);
                    form2_partition.SetAttribute("name", datafile_name + ".form2");
                    form2_partition.SetAttribute("md5", BitConverter.ToString(md5_hashes.form2.Hash).Replace("-", "").ToLower());
                    form2_partition.SetAttribute("size", writers.form2.BaseStream.Length.ToString());
                    partition.AppendChild(form2_partition);
                    string name = writers.fs_form2.Name;
                    writers.form2.Dispose();
                    File.Move(name, Path.Combine(working_dir, datafile_name + ".form2"));
                }
                else
                {
                    string name = writers.fs_form2.Name;
                    writers.form2.Dispose();
                    File.Delete(name);
                }
                if (writers.cdda.BaseStream.Length > 44)
                {
                    XmlElement cdda_partition = datafile.CreateElement("cdda");
                    md5_hashes.cdda.TransformFinalBlock(new byte[0], 0, 0);
                    cdda_partition.SetAttribute("name", datafile_name + ".wav");
                    cdda_partition.SetAttribute("md5", BitConverter.ToString(md5_hashes.cdda.Hash).Replace("-", "").ToLower());
                    cdda_partition.SetAttribute("size", (writers.cdda.BaseStream.Length - 44).ToString());
                    partition.AppendChild(cdda_partition);
                    string name = writers.fs_cdda.Name;

                    writers.cdda.BaseStream.Seek(4, SeekOrigin.Begin);
                    writers.cdda.Write((int)writers.cdda.BaseStream.Length - 8);
                    writers.cdda.BaseStream.Seek(40, SeekOrigin.Begin);
                    writers.cdda.Write((int)writers.cdda.BaseStream.Length - 44);

                    if (audio_offsets.Count != 0)
                    {
                        List<string> cuesheet = GenCue(audio_offsets, writers.cdda.BaseStream.Length - 44, datafile_name);
                        File.WriteAllLines(Path.Combine(working_dir, datafile_name + ".cue"), cuesheet);
                    }
                    writers.cdda.Dispose();
                    File.Move(name, Path.Combine(working_dir, datafile_name + ".wav"));

                    //List<string> cuesheet = GenCue(audio_offsets);
                    //File.WriteAllLines(Path.Combine(working_dir, datafile_name + ".cue"), cuesheet);
                }
                else
                {
                    string name = writers.fs_cdda.Name;
                    writers.cdda.Dispose();
                    File.Delete(name);
                }
                XmlElement map_partition = datafile.CreateElement("map");

                writers.map.BaseStream.Position = 0;
                map_partition.SetAttribute("name", datafile_name + ".map");
                map_partition.SetAttribute("md5", BitConverter.ToString(md5_hashes.map.ComputeHash(writers.map.BaseStream)).Replace("-", "").ToLower());
                map_partition.SetAttribute("size", writers.map.BaseStream.Length.ToString());
                string map_file_name = writers.fs_map.Name;
                writers.map.Dispose();
                File.Move(map_file_name, Path.Combine(working_dir, datafile_name + ".map"));

                md5_hashes.Dispose();

                partition.AppendChild(map_partition);

                datafile.DocumentElement.AppendChild(partition);

                datafile.Save(Path.Combine(working_dir, datafile_name + ".xml"));

                //List<string> cuesheet = GenCue(audio_offsets);
                //File.WriteAllLines(Path.Combine(working_dir, datafile_name + ".cue"), cuesheet);
            }
        }

        private static List<string> GenCue(List<long> audio_offsets, long leadout, string file_name)
        {
            List<string> cue_sheet = new List<string>();

            cue_sheet.Add(string.Format("FILE \"{0}.wav\" WAVE", file_name));

            int track_counter = 1;
            long prev_offset = -2352 * 75;
            foreach (long audio_offset in audio_offsets)
            {
                if (audio_offset != leadout)
                {
                    if (audio_offset != prev_offset)
                    {
                        if (audio_offset - 2352 * 75 >= prev_offset)
                        {
                            if (!(audio_offset + 2352 * 75 >= leadout))
                            {
                                long minutes = audio_offset / 10584000;
                                long seconds = (audio_offset % 10584000) / 176400;
                                long frames = (audio_offset % 10584000) % 176400 / 2352;

                                cue_sheet.Add(string.Format("  TRACK {0:D2} AUDIO", track_counter++));
                                cue_sheet.Add(string.Format("    INDEX 01 {0:d2}:{1:d2}:{2:d2}", minutes, seconds, frames));
                                prev_offset = audio_offset;
                            }
                        }
                    }

                }
            }

            return cue_sheet;
        }

        private static bool CompareChain(byte[] standart_sync, byte[] temp_block)
        {
            for (int i = 0; i < standart_sync.Length; i++)
            {
                if (standart_sync[i] != temp_block[i]) return false;
            }

            return true;
        }

        private static byte TestSub(byte[] temp_block)
        {
            for (int i = 0; i < 8; i++)
            {
                if (temp_block[i + 16] != 0) return 0;
            }

            for (int i = 0; i < 280; i++)
            {
                if (temp_block[i + 2072] != 0) return 0x20;
            }
            return 0;
        }

        private static byte TestNoEdc(byte[] temp_block)
        {
            using (BinaryReader br = new BinaryReader(new MemoryStream(temp_block)))
            {
                br.BaseStream.Position = 2348;
                if (br.ReadUInt32() != 0) return 0;
            }
            return 0x10;
        }
    }

    class DupesDictionary
    {
        public Dictionary<string, int> form1 = new Dictionary<string, int>();
        public Dictionary<string, int> form2 = new Dictionary<string, int>();
        public Dictionary<string, int> cdda = new Dictionary<string, int>();
    }

    class BinaryWriterCollection
    {
        byte[] riff_header = {
                                 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41,
                                 0x56, 0x45, 0x66, 0x6d, 0x74, 0x20, 0x10, 0x00, 0x00, 0x00, 
                                 0x01, 0x00, 0x02, 0x00, 0x44, 0xac, 0x00, 0x00, 0x10, 0xb1,
                                 0x02, 0x00, 0x04, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61, 
                                 0x00, 0x00, 0x00, 0x00
                             };


        public struct BinaryWriters
        {
            public BinaryWriter form1;
            public BinaryWriter form2;
            public BinaryWriter cdda;
            public BinaryWriter map;

            internal FileStream fs_form1;
            internal FileStream fs_form2;
            internal FileStream fs_cdda;
            internal FileStream fs_map;

            internal void Dispose()
            {
                form1.Dispose();
                form2.Dispose();
                cdda.Dispose();
                map.Dispose();
            }
        }

        internal BinaryWriters Create(string working_dir)
        {
            BinaryWriters writers = new BinaryWriters();

            writers.fs_form1 = new FileStream(Path.Combine(working_dir, "form1"), FileMode.Create);
            writers.fs_form2 = new FileStream(Path.Combine(working_dir, "form2"), FileMode.Create);
            writers.fs_map = new FileStream(Path.Combine(working_dir, "map"), FileMode.Create);

            writers.fs_cdda = new FileStream(Path.Combine(working_dir, "cdda"), FileMode.Create);

            writers.form1 = new BinaryWriter(writers.fs_form1);
            writers.form2 = new BinaryWriter(writers.fs_form2);
            writers.map = new BinaryWriter(writers.fs_map);

            writers.cdda = new BinaryWriter(writers.fs_cdda);
            writers.cdda.Write(riff_header);

            return writers;
        }
    }

    class OffsetCursor
    {
        public int form1 = 0;
        public int form2 = 0;
        public int cdda = 0;
    }

    class MD5Collection
    {
        public struct MD5Hash
        {
            public MD5 form1;
            public MD5 form2;
            public MD5 cdda;
            public MD5 map;

            internal void Dispose()
            {
                form1.Dispose();
                form2.Dispose();
                map.Dispose();
                map.Dispose();
            }
        }

        internal MD5Hash Create()
        {
            MD5Hash hashes = new MD5Hash();
            {
                hashes.form1 = MD5.Create();
                hashes.form2 = MD5.Create();
                hashes.cdda = MD5.Create();
                hashes.map = MD5.Create();
            }
            return hashes;
        }
    }
}
