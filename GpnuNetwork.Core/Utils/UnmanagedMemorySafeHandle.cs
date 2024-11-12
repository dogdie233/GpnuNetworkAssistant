using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GpnuNetwork.Core.Utils;

public class UnmanagedMemorySafeHandle : SafeHandle
{
    public UnmanagedMemorySafeHandle(IntPtr invalidHandleValue, bool ownsHandle) : base(invalidHandleValue, ownsHandle)
    {
    }

    protected override bool ReleaseHandle()
    {
        Marshal.FreeHGlobal(handle);
        // handle = nint.Zero;
        return true;
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public static UnmanagedMemorySafeHandle Alloc(int size)
        => new(Marshal.AllocHGlobal(size), true);
}