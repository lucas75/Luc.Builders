using System.Threading;

namespace Lwx.Builders.MicroService.Tests.MockServices;

public interface IWorkerCounters
{
    int WorkerStageNoneTicks { get; }
    int WorkerStageDevelopmentOnlyTicks { get; }
    int WorkerStageAllTicks { get; }

    void IncWorkerStageNoneTicks();
    void IncWorkerStageDevelopmentOnlyTicks();
    void IncWorkerStageAllTicks();
    void Reset();
}

public sealed class WorkerCounters : IWorkerCounters
{
    private int _none;
    private int _dev;
    private int _prd;

    public int WorkerStageNoneTicks => _none;
    public int WorkerStageDevelopmentOnlyTicks => _dev;
    public int WorkerStageAllTicks => _prd;

    public void IncWorkerStageNoneTicks() => Interlocked.Increment(ref _none);
    public void IncWorkerStageDevelopmentOnlyTicks() => Interlocked.Increment(ref _dev);
    public void IncWorkerStageAllTicks() => Interlocked.Increment(ref _prd);

    public void Reset()
    {
        Interlocked.Exchange(ref _none, 0);
        Interlocked.Exchange(ref _dev, 0);
        Interlocked.Exchange(ref _prd, 0);
    }
}
