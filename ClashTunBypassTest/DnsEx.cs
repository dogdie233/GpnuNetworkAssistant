using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using GpnuNetwork.Core.Extensions;

namespace ClashTunBypassTest;

public class DnsEx
{
    public static NetworkInterface? UsingInterface { get; set; }

    public static async Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress)
    {
        if (IPAddress.TryParse(hostNameOrAddress, out var addr))
            return [addr];

        if (UsingInterface is null)
            throw new InvalidOperationException($"{nameof(UsingInterface)} is not set");

        var servers = UsingInterface.GetDnsServers().Where(IPAddressExtension.IsV4).ToArray();
        Logging.Debug($"[[dns]] 正在请求解析地址{hostNameOrAddress}, 按顺序向以下dns服务器发起请求：{string.Join('，', servers.AsEnumerable())}");
        foreach (var server in servers)
            if (await GetHostAddressFromSpecificDnsAsync(hostNameOrAddress, server) is { Length: > 0 } addresses)
                return addresses;

        return [];
    }

    public static async Task<IPAddress[]> GetHostAddressFromSpecificDnsAsync(string hostNameOrAddress, IPAddress dns)
    {
        if (IPAddress.TryParse(hostNameOrAddress, out var addr))
            return [addr];

        if (UsingInterface is null)
            throw new InvalidOperationException($"{nameof(UsingInterface)} is not set");

        var sourceAddress = UsingInterface.GetIpv4Address().FirstOrDefault();
        if (sourceAddress is null)
            throw new InvalidOperationException("No ipv4 address found");

        // Create an udp client which bind to the using interface
        using var client = new UdpClient(new IPEndPoint(sourceAddress, 0));
        client.Connect(dns, 53);
        Logging.Debug($"[[dns]]  > 创建Udp链接 {client.Client.LocalEndPoint} --> {client.Client.RemoteEndPoint}");

        // Create a dns query packet
        var dnsPacket = DnsPacket.CreateV4Query(hostNameOrAddress);
        var id = dnsPacket.Header.Id;
        using var ms = new MemoryStream();
        dnsPacket.Serialize(ms);

        var timeoutCancellationTokenSource = new CancellationTokenSource();

        // Send dns query data
        Logging.Debug($"[[dns]] 向 {client.Client.RemoteEndPoint} 发出dns请求，id为{dnsPacket.Header.Id}");
        await client.SendAsync(ms.GetBuffer(), (int)ms.Length);
        var udpReceiveTask = client.ReceiveAsync(timeoutCancellationTokenSource.Token);
        timeoutCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));  // 30 seconds timeout

        while (true)
        {
            // Receive dns response
            UdpReceiveResult receiveResult;
            try
            {
                receiveResult = await udpReceiveTask;
                Logging.Debug($"[[dns]]  > 接收到长度为{receiveResult.Buffer.Length}bytes的数据");
            }
            catch (OperationCanceledException ex) when (timeoutCancellationTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException("Dns query timeout", ex);
            }

            // Clear memory stream and write received data
            ms.SetLength(0);
            ms.Write(receiveResult.Buffer, 0, receiveResult.Buffer.Length);
            ms.Seek(0, SeekOrigin.Begin);
            dnsPacket.Deserialize(ms);

            Logging.Debug($"[[dns]]  > 接收到的dns回复id为{dnsPacket.Header.Id}，期望为{id}");
            if (dnsPacket.Header.Id == id)
                break;
        }

        // XD
        return (from answer in dnsPacket.Answers
            where answer.Type is DnsPacket.RecordType.A or DnsPacket.RecordType.AAAA
            select answer.GetIpAddress()).ToArray();
    }
}