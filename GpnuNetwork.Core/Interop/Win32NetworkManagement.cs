using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.IpHelper;

using GpnuNetwork.Core.Common;

namespace GpnuNetwork.Core.Interop;

public abstract class Win32NetworkManagement : INetworkManagement
{
    public static unsafe AdapterInfo[] GetAdaptersInfo()
    {
        var flag = GET_ADAPTERS_ADDRESSES_FLAGS.GAA_FLAG_INCLUDE_GATEWAYS;
        var size = (uint)Marshal.SizeOf<IP_ADAPTER_ADDRESSES_LH>() * 8;
        var buffer = Marshal.AllocHGlobal((int)size);

        var err = (WIN32_ERROR)PInvoke.GetAdaptersAddresses(0, flag, (IP_ADAPTER_ADDRESSES_LH*)buffer.ToPointer(), ref size);
        if (err == WIN32_ERROR.ERROR_BUFFER_OVERFLOW)
        {
            Marshal.FreeHGlobal(buffer);
            buffer = Marshal.AllocHGlobal((int)size);
            err = (WIN32_ERROR)PInvoke.GetAdaptersAddresses(0, flag, (IP_ADAPTER_ADDRESSES_LH*)buffer.ToPointer(), ref size);
        }

        if (err == WIN32_ERROR.NO_ERROR)
        {
            var result = new List<AdapterInfo>();

            var apiResult = (IP_ADAPTER_ADDRESSES_LH*)buffer.ToPointer();
            while (apiResult != null)
            {
                result.Add(new AdapterInfo
                {
                    Name = apiResult->AdapterName.ToString(),
                    FriendlyName = apiResult->FriendlyName.ToString(),
                    Address4 = new IPAddress(apiResult->Ipv4Metric),
                });
                apiResult = apiResult->Next;
            }

            Marshal.FreeHGlobal(buffer);
            return result.ToArray();
        }

        Marshal.FreeHGlobal(buffer);
        throw new Win32Exception((int)err);
    }

    public static int GetBestInterfaceId()
    {
        // PInvoke.GetBestInterface()
        return 0;
    }
}