using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class GameStatService
{
    private const int MaxPlayers = 8;

    /// <summary>
    /// Process a stats.dmp file and return a dictionary with parsed results.
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> ProcessStatsDmp(string filePath, List<string> countableTags)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        var result = new Dictionary<string, Dictionary<string, object>>();

        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            // Skip header
            br.ReadBytes(4);

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                if (br.BaseStream.Position + 8 > br.BaseStream.Length)
                    break;

                // tag (4 bytes) + type (2 bytes) + length (2 bytes)
                string tag = Encoding.ASCII.GetString(br.ReadBytes(4));
                ushort type = br.ReadUInt16();
                ushort length = br.ReadUInt16();

                int pad = (length % 4) != 0 ? 4 - (length % 4) : 0;

                byte[] data = length > 0 ? br.ReadBytes(length) : Array.Empty<byte>();

                if (pad > 0)
                    br.ReadBytes(pad);

                var fieldValue = GetFieldValue(type, data, length);

                result[tag] = new Dictionary<string, object>
                {
                    { "tag", tag },
                    { "length", length },
                    { "raw", Convert.ToBase64String(data) },
                    { "value", fieldValue }
                };
            }
        }

        // Handle countable objects per player
        foreach (var tag in countableTags)
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                string key = $"{tag}{i}";
                if (result.ContainsKey(key))
                {
                    byte[] raw = Convert.FromBase64String((string)result[key]["raw"]);
                    int length = (int)result[key]["length"];
                    var counts = new Dictionary<int, uint>();

                    for (int j = 0, t = 0; j + 4 <= length; j += 4, t++)
                    {
                        uint count = BitConverter.ToUInt32(new byte[] { raw[j + 3], raw[j + 2], raw[j + 1], raw[j] }, 0); // big-endian
                        if (count > 0)
                            counts[t] = count;
                    }

                    result[key]["counts"] = counts;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Decode a field value based on type
    /// </summary>
    private object GetFieldValue(ushort type, byte[] data, int length)
    {
        switch (type)
        {
            case 1: // BYTE
                return data[0];

            case 2: // BOOLEAN
                return data[0] != 0;

            case 3: // SHORT
            case 4: // UNSIGNED SHORT
                return BitConverter.ToUInt16(data, 0);

            case 5: // LONG
            case 6: // UNSIGNED LONG
                return BitConverter.ToUInt32(data, 0);

            case 7: // CHAR
                {
                    string str = Encoding.ASCII.GetString(data);
                    var sb = new StringBuilder();
                    foreach (char c in str)
                        sb.Append(c >= 32 && c <= 126 ? c : '?');
                    return sb.ToString();
                }

            case 20: // CUSTOM LENGTH
                return null;

            default:
                return null;
        }
    }
}