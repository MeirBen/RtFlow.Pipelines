using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Extensions;

public static class PipelineBuilderSinkExtensions
{
    /// <summary>
    /// Converts a propagator pipeline into a sink-only pipeline.
    /// </summary>
    public static ITargetBlock<T> AsSink<T>(this IPropagatorBlock<T, T> propagator)
    {
        return DataflowBlock.Encapsulate(propagator, propagator);
    }
}
