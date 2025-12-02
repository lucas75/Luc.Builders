using System.Threading;

namespace Lwx.Builders.MicroService.Tests;

public static class WorkerCounters
{
    private static int _none;
    private static int _dev;
    private static int _prd;

    public static int None => _none;
    public static int Dev => _dev;
    public static int Prd => _prd;

    public static void IncNone() => Interlocked.Increment(ref _none);
    public static void IncDev() => Interlocked.Increment(ref _dev);
    public static void IncPrd() => Interlocked.Increment(ref _prd);

    public static void Reset()
    {
        Interlocked.Exchange(ref _none, 0);
        Interlocked.Exchange(ref _dev, 0);
        Interlocked.Exchange(ref _prd, 0);
    }
}
