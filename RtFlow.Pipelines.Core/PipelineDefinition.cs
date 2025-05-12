using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Defines a named, cancellable pipeline that propagates data from TIn to TOut.
/// </summary>
public class PipelineDefinition<TIn, TOut> : IPipelineDefinition
{
    public string Name { get; }
    private readonly Func<CancellationToken, IPropagatorBlock<TIn, TOut>> _factory;

    public PipelineDefinition(
        string name,
        Func<CancellationToken, IPropagatorBlock<TIn, TOut>> factory)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Materializes the pipeline as a propagator block.
    /// </summary>
    public IPropagatorBlock<TIn, TOut> Create(CancellationToken ct)
        => _factory(ct);

    /// <summary>
    /// Materializes the pipeline as a dataflow block.
    /// </summary>
    IDataflowBlock IPipelineDefinition.Create(CancellationToken ct)
        => Create(ct);
}