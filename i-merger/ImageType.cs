using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace i_merger
{
    class ImageType
    {
        static string cd001 = "CD001";
        static string cd3do = "ZZZZZ";
        static byte[] sync = { 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00 };


        internal static string FindOutType(string file)
        {
            using (BinaryReader br = new BinaryReader(new FileStream(file, FileMode.Open)))
            {
                if (br.BaseStream.Length > 32768)
                {
                    //стандартный образ 2048 (PS2, PSP) содержит CD001 по смещению 32769 (16 * 2048 + 1)
                    br.BaseStream.Position = 32769;
                    if (Encoding.ASCII.GetString(br.ReadBytes(5)).Equals(cd001)) return "2048";

                    //стандартный образ mode1 2352 содержит CD001 по смещению 32769 (16 * 2352 + 16 + 1)
                    br.BaseStream.Position = 37649;
                    if (Encoding.ASCII.GetString(br.ReadBytes(5)).Equals(cd001)) return "2352";

                    //стандартный образ mode2 2352 (PS1) содержит CD001 по смещению 37657 (16 * 2352 + 16 + 8 + 1)
                    br.BaseStream.Position = 37657;
                    if (Encoding.ASCII.GetString(br.ReadBytes(5)).Equals(cd001)) return "2352";

                    //3DO образ 2048 содержит ZZZZZ по смещению 1 (0 + 1)
                    br.BaseStream.Position = 1;
                    if (Encoding.ASCII.GetString(br.ReadBytes(5)).Equals(cd3do)) return "2048";

                    //3DO образ 2352 содержит ZZZZZ по смещению 17 (0 + 16 + 1)
                    br.BaseStream.Position = 17;
                    if (Encoding.ASCII.GetString(br.ReadBytes(5)).Equals(cd3do)) return "2352";

                    //проверка по наличию синхронизации (DC)
                    br.BaseStream.Position = 10 * 75 * 2352;
                    byte[] chain = br.ReadBytes(12);
                    if (chain.SequenceEqual(sync)) return "2352";
                }
                if (br.BaseStream.Length % 2352 == 0)
                {
                    return "pcm";
                }
            }

            return "file";
        }
    }
}
