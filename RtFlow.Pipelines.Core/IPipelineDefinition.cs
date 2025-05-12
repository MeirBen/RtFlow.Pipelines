using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Base interface for all pipeline definitions.
/// </summary>
public interface IPipelineDefinition
{
    string Name { get; }
    IDataflowBlock Create(CancellationToken ct);
}
