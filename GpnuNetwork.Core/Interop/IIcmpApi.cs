using System.Net.NetworkInformation;

using GpnuNetwork.Core.Common;

namespace GpnuNetwork.Core.Interop;

public interface IIcmpApi
{
    public static abstract void InitPingEx(PingEx pingEx);
    public static abstract void SendPingEx(PingEx pingEx);
}