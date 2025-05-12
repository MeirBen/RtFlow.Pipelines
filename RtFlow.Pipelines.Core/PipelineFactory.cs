using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Factory methods for creating pipeline definitions.
/// </summary>
public static class PipelineFactory
{
    /// <summary>
    /// Creates a processing pipeline that transforms input to output.
    /// </summary>
    public static PipelineDefinition<TIn, TOut> CreatePipeline<TIn, TOut>(
        string name,
        Func<CancellationToken, IPropagatorBlock<TIn, TOut>> factory)
    {
        return new PipelineDefinition<TIn, TOut>(name, factory);
    }

    /// <summary>
    /// Creates a sink pipeline that only consumes data (no output).
    /// </summary>
    public static SinkPipelineDefinition<TIn> CreateSink<TIn>(
        string name,
        Func<CancellationToken, ITargetBlock<TIn>> factory)
    {
        return new SinkPipelineDefinition<TIn>(name, factory);
    }
}