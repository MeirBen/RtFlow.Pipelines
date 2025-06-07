using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

public class PipelineFactory : IPipelineFactory, IDisposable
{
    private readonly List<CancellationTokenSource> _linkedTokenSources = [];
    private readonly object _lock = new();
    private bool _disposed;

    public CancellationTokenSource CancellationTokenSource { get; } = new();

    public IFluentPipelineBuilder<T, T> Create<T>(
        Action<ExecutionDataflowBlockOptions> cfg = null,
        CancellationToken cancellationToken = default)
    {
        CancellationToken effectiveToken;
        
        if (cancellationToken == default)
        {
            effectiveToken = CancellationTokenSource.Token;
        }
        else
        {
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token, cancellationToken);
            lock (_lock)
            {
                if (!_disposed)
                {
                    _linkedTokenSources.Add(linkedTokenSource);
                }
            }
            effectiveToken = linkedTokenSource.Token;
        }

        return FluentPipeline.Create<T>(cfg, effectiveToken);
    }

    public IFluentPipelineBuilder<TIn, TOut> BeginWith<TIn, TOut>(
        IPropagatorBlock<TIn, TOut> head,
        CancellationToken cancellationToken = default)
    {
        CancellationToken effectiveToken;
        
        if (cancellationToken == default)
        {
            effectiveToken = CancellationTokenSource.Token;
        }
        else
        {
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token, cancellationToken);
            lock (_lock)
            {
                if (!_disposed)
                {
                    _linkedTokenSources.Add(linkedTokenSource);
                }
            }
            effectiveToken = linkedTokenSource.Token;
        }

        return FluentPipeline.BeginWith<TIn, TOut>(head, effectiveToken);
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            // Cancel the main token source
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();

            // Dispose all linked token sources
            foreach (var tokenSource in _linkedTokenSources)
            {
                tokenSource?.Dispose();
            }
            _linkedTokenSources.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
