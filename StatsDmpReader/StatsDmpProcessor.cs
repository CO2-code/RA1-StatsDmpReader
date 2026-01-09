using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameStatsProcessor
{
    public class StatsDmpProcessor
    {
        private const int MaxPlayers = 8;

        public Dictionary<string, Dictionary<string, object>>? ProcessStatsDmp(
            string file,
            List<string> countableTags)
        {
            if (file == null || !File.Exists(file))
                return null;

            var result = new Dictionary<string, Dictionary<string, object>>();

            using (FileStream fs = new FileStream(file, FileMode.Open))
            using (BinaryReader br = new BinaryReader(fs))
            {
                // Skip 4-byte header (same as PHP)
                br.ReadBytes(4);

                while (fs.Position + 8 <= fs.Length)
                {
                    string tag = Encoding.ASCII.GetString(br.ReadBytes(4)).TrimEnd('\0');

                    ushort type = ReadUInt16BE(br.ReadBytes(2));
                    ushort length = ReadUInt16BE(br.ReadBytes(2));

                    byte[] data = br.ReadBytes(length);

                    int pad = (length % 4 != 0) ? (4 - (length % 4)) : 0;
                    if (pad > 0)
                        br.ReadBytes(pad);

                    object? value = ParseValue(type, data);

                    result[tag] = new Dictionary<string, object>
                    {
                        { "tag", tag },
                        { "length", length },
                        { "raw", Convert.ToBase64String(data) },
                        { "value", value ?? "" }
                    };
                }
            }

            // Parse array-type tags (unit/building stats)
            foreach (string baseTag in countableTags)
            {
                for (int player = 0; player < MaxPlayers; player++)
                {
                    string key = baseTag + player;

                    if (!result.ContainsKey(key))
                        continue;

                    byte[] raw = Convert.FromBase64String((string)result[key]["raw"]);
                    int length = Convert.ToInt32(result[key]["length"]);

                    Dictionary<int, uint> indexCounts = new Dictionary<int, uint>();

                    for (int i = 0, t = 0; i + 4 <= length; i += 4, t++)
                    {
                        uint count =
                            (uint)((raw[i] << 24) |
                                   (raw[i + 1] << 16) |
                                   (raw[i + 2] << 8) |
                                   raw[i + 3]);

                        if (count > 0)
                            indexCounts[t] = count;
                    }

                    result[key]["counts"] = indexCounts;
                }
            }

            return result;
        }

        private object? ParseValue(ushort type, byte[] data)
        {
            return type switch
            {
                1 => data.Length > 0 ? data[0] : 0,                       // BYTE
                2 => data.Length > 0 && data[0] != 0,                     // BOOLEAN
                3 or 4 => data.Length >= 2 ? ReadUInt16BE(data) : 0,      // SHORT/USHORT
                5 or 6 => data.Length >= 4 ? ReadUInt32BE(data) : 0,      // LONG/ULONG
                7 => Encoding.ASCII.GetString(data).TrimEnd('\0'),        // CHAR
                20 => null,                                               // CUSTOM LENGTH (arrays)
                _ => null
            };
        }

        private ushort ReadUInt16BE(byte[] input)
        {
            byte[] data = (byte[])input.Clone();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        private uint ReadUInt32BE(byte[] input)
        {
            byte[] data = (byte[])input.Clone();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }
    }
}