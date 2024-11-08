using System.Net;
using System.Net.NetworkInformation;

namespace GpnuNetwork.Core.Common;

public record PingExReply(IPAddress Address, PingOptions? Options, IPStatus Status, long RoundtripTime, byte[]? Buffer)
{
}