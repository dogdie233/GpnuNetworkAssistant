using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

namespace GpnuNetwork.Core.Utils;

public static class Defender
{
    public readonly struct CatchResult<T>
    {
        private CatchResult(bool success, T data, Exception? exception)
        {
            Data = data;
            IsSuccess = success;
        }

        [Pure]
        [MemberNotNullWhen(false, nameof(Exception))]
        public bool IsSuccess { get; init; }

        [Pure]
        public T Data { get; init; }

        [Pure]
        public Exception? Exception { get; init; }

        public static CatchResult<T> CreateSuccess(T data) => new(true, data, null);
        public static CatchResult<T> CreateFailure(Exception exception) => new(false, default!, exception);
    }

    public static CatchResult<T> Try<T>(Func<T> func)
    {
        try
        {
            return CatchResult<T>.CreateSuccess(func());
        }
        catch (Exception ex)
        {
            return CatchResult<T>.CreateFailure(ex);
        }
    }

    public static async Task<CatchResult<T>> TryAsync<T>(Func<Task<T>> func)
    {
        try
        {
            return CatchResult<T>.CreateSuccess(await func());
        }
        catch (Exception ex)
        {
            return CatchResult<T>.CreateFailure(ex);
        }
    }

    public static async Task<CatchResult<T>> TryAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken ct)
    {
        try
        {
            return CatchResult<T>.CreateSuccess(await func(ct));
        }
        catch (Exception ex)
        {
            return CatchResult<T>.CreateFailure(ex);
        }
    }
}