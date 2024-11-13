using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
// ReSharper disable InconsistentNaming

namespace ClashTunBypassTest;

public class DnsPacket
{
    public enum RecordType
    {
        A = 1,
        NS = 2,
        CNAME = 5,
        SOA = 6,
        WKS = 11,
        PTR = 12,
        HINFO = 13,
        MINFO = 14,
        MX = 15,
        TXT = 16,
        AAAA = 28,
        SRV = 33,
        ANY = 255
    }
    
    public enum RecordClass
    {
        IN = 1,
        CS = 2,
        CH = 3,
        HS = 4,
        ANY = 255
    }
    
    public DnsPacketHeader Header { get; set; }
    public List<DnsPacketQuestion> Questions { get; set; }
    public List<DnsPacketAnswer> Answers { get; set; }

    private DnsPacket()
    {
        Header = new DnsPacketHeader();
        Questions = [];
        Answers = [];
    }

    public void Serialize(Stream dest)
    {
        Header.Serialize(dest, (ushort)Questions.Count, (ushort)Answers.Count);
        foreach (var question in Questions)
            question.Serialize(dest);
    }

    public void Deserialize(Stream src)
    {
        Header.Deserialize(src, out var questionCount, out var answerCount);
        Questions.Clear();
        for (var i = 0; i < questionCount; i++)
        {
            var question = new DnsPacketQuestion();
            question.Deserialize(src);
            Questions.Add(question);
        }

        Answers.Clear();
        for (var i = 0; i < answerCount; i++)
        {
            var answer = new DnsPacketAnswer();
            answer.Deserialize(src);
            Answers.Add(answer);
        }
    }

    public static DnsPacket CreateV4Query(string domain)
    {
        var packet = new DnsPacket
        {
            Header = new DnsPacketHeader
            {
                Id = (ushort)Random.Shared.Next(),
                OpCode = 0,
                RecursionDesiredFlag = true
            }
        };

        packet.Questions.Add(new DnsPacketQuestion
        {
            Domain = domain,
            Type = RecordType.A,
            Class = RecordClass.IN
        });

        return packet;
    }
}

public class DnsPacketHeader
{
    public enum ReturnCodeType
    {
        NoError = 0,
        FormatError = 1,
        ServerFailure = 2,
        NameError = 3,
        NotImplemented = 4,
        Refused = 5
    }

    public ushort Id { get; set; }
    public bool ResponseFlag { get; set; }
    public int OpCode { get; set; }
    public bool AuthoritativeAnswerFlag { get; set; }
    public bool TruncationFlag { get; set; }
    public bool RecursionDesiredFlag { get; set; }
    public bool RecursionAvailableFlag { get; set; }
    public ReturnCodeType ReturnCode { get; set; }

    public void Serialize(Stream dest, ushort questionCount, ushort answerCount)
    {
        Span<byte> buffer = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, Id);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[2..], (ushort)(
            ((ResponseFlag ? 1 : 0) << 15) |
            ((OpCode & 0b1111) << 11) |
            ((AuthoritativeAnswerFlag ? 1 : 0) << 10) |
            ((TruncationFlag ? 1 : 0) << 9) |
            ((RecursionDesiredFlag ? 1 : 0) << 8) |
            ((RecursionAvailableFlag ? 1 : 0) << 7) |
            (int)ReturnCode
        ));
        BinaryPrimitives.WriteUInt16BigEndian(buffer[4..], questionCount);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[6..], answerCount);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[8..], 0);  // authority count
        BinaryPrimitives.WriteUInt16BigEndian(buffer[10..], 0);  // additional count

        dest.Write(buffer);
    }

    public void Deserialize(Stream dest, out ushort questionCount, out ushort answerCount)
    {
        Span<byte> buffer = stackalloc byte[12];
        dest.ReadExactly(buffer);
        Id = BinaryPrimitives.ReadUInt16BigEndian(buffer);
        var flags = BinaryPrimitives.ReadUInt16BigEndian(buffer[2..]);
        ResponseFlag = (flags & (1 << 15)) != 0;
        OpCode = (flags >> 11) & 0b1111;
        AuthoritativeAnswerFlag = (flags & (1 << 10)) != 0;
        TruncationFlag = (flags & (1 << 9)) != 0;
        RecursionDesiredFlag = (flags & (1 << 8)) != 0;
        RecursionAvailableFlag = (flags & (1 << 7)) != 0;
        ReturnCode = (ReturnCodeType)(flags & 0b1111);
        questionCount = BinaryPrimitives.ReadUInt16BigEndian(buffer[4..]);
        answerCount = BinaryPrimitives.ReadUInt16BigEndian(buffer[6..]);
    }
}

public class DnsPacketQuestion
{
    public string Domain { get; set; }
    public DnsPacket.RecordType Type { get; set; }
    public DnsPacket.RecordClass Class { get; set; }

    public void Serialize(Stream dest)
    {
        var domainParts = Domain.Split('.');
        foreach (var part in domainParts)
        {
            dest.WriteByte((byte)part.Length);
            dest.Write(Encoding.ASCII.GetBytes(part));
        }
        dest.WriteByte(0);  // end of domain
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)Type);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[2..], (ushort)Class);
        dest.Write(buffer);
    }

    public void Deserialize(Stream src)
    {
        var domainParts = new List<string>();
        while (true)
        {
            var length = src.ReadByte();
            if (length == 0)
                break;
            var partBuffer = new byte[length];
            src.ReadExactly(partBuffer);
            domainParts.Add(Encoding.ASCII.GetString(partBuffer));
        }
        Domain = string.Join('.', domainParts);

        Span<byte> buffer = stackalloc byte[4];
        src.ReadExactly(buffer);
        Type = (DnsPacket.RecordType)BinaryPrimitives.ReadUInt16BigEndian(buffer);
        Class = (DnsPacket.RecordClass)BinaryPrimitives.ReadUInt16BigEndian(buffer[2..]);
    }
}

public class DnsPacketAnswer
{
    public string Domain { get; set; }
    public DnsPacket.RecordType Type { get; set; }
    public DnsPacket.RecordClass Class { get; set; }
    public int TimeToLive { get; set; }
    public int DataLength { get; set; }
    public byte[] Data { get; set; }

    [MemberNotNull(nameof(Domain), nameof(Data))]
    public void Deserialize(Stream src)
    {
        Domain = string.Join('.', Helper.ReadDnsLabel(src));

        Span<byte> buffer = stackalloc byte[10];
        src.ReadExactly(buffer);
        Type = (DnsPacket.RecordType)BinaryPrimitives.ReadUInt16BigEndian(buffer);
        Class = (DnsPacket.RecordClass)BinaryPrimitives.ReadUInt16BigEndian(buffer[2..]);
        TimeToLive = BinaryPrimitives.ReadInt32BigEndian(buffer[4..]);
        DataLength = BinaryPrimitives.ReadInt16BigEndian(buffer[8..]);
        Data = new byte[DataLength];
        src.ReadExactly(Data);
    }

    public IPAddress GetIpAddress()
    {
        if (Type is not DnsPacket.RecordType.A and not DnsPacket.RecordType.AAAA)
            throw new InvalidOperationException("Not an ip address record");

        return Type switch
        {
            DnsPacket.RecordType.A => new IPAddress(Data),
            DnsPacket.RecordType.AAAA => new IPAddress(Data, 0),
            _ => throw new InvalidOperationException("Not an ip address record")
        };
    }
}