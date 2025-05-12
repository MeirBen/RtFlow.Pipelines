using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Defines a named, cancellable pipeline that only consumes TIn items (no output).
/// </summary>
public class SinkPipelineDefinition<TIn> : IPipelineDefinition
{
    public string Name { get; }
    private readonly Func<CancellationToken, ITargetBlock<TIn>> _factory;

    public SinkPipelineDefinition(
        string name,
        Func<CancellationToken, ITargetBlock<TIn>> factory)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Materializes the sink pipeline as a target block.
    /// </summary>
    public ITargetBlock<TIn> CreateSink(CancellationToken ct)
        => _factory(ct);

    /// <summary>
    /// Materializes the sink pipeline as a dataflow block.
    /// </summary>
    IDataflowBlock IPipelineDefinition.Create(CancellationToken ct)
        => CreateSink(ct);
}