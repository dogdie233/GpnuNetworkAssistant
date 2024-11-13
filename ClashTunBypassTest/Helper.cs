using System.Text;

namespace ClashTunBypassTest;

public class Helper
{
    public static string ReadDnsLabel(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);
        var part = new List<string>();
        long? initialPosition = null;

        while (true)
        {
            var length = reader.ReadByte();

            if (length == 0)
                break;

            // Check if length indicates a compressed label
            if ((length & 0xC0) == 0xC0)
            {
                long offset = ((length & 0x3F) << 8) | reader.ReadByte();
                initialPosition ??= stream.Position;
                stream.Position = offset;
                continue;
            }

            var labelBytes = reader.ReadBytes(length);
            part.Add(Encoding.ASCII.GetString(labelBytes));
        }

        stream.Position = initialPosition ?? stream.Position;
        return string.Join('.', part);
    }
}