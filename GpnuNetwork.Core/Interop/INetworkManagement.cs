using GpnuNetwork.Core.Network.Common;

namespace GpnuNetwork.Core.Interop;

internal interface INetworkManagement
{
    // static abstract AdapterInfo[] GetAdaptersInfo();

    static abstract int GetBestInterfaceId();
}