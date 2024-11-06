using GpnuNetwork.Core.Common;

namespace GpnuNetwork.Core.Interop;

internal interface INetworkManagement
{
    // static abstract AdapterInfo[] GetAdaptersInfo();

    static abstract int GetBestInterfaceId();
}