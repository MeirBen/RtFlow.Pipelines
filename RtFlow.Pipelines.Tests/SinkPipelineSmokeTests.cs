using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using RtFlow.Pipelines.Core.Utils;

namespace RtFlow.Pipelines.Tests
{
    public class SinkPipelineSmokeTests
    {
        [Fact]
        public async Task SinkPipelineDefinition_Processes_All_Items()
        {
            const int N = 10_000;
            int count = 0;
            long sum = 0;

            // 1) Define a sink pipeline using the factory
            var sinkDef = PipelineFactory.CreateSink<int>(
                name: "DoubleAndCountSink",
                factory: ct =>
                {
                    // Create pipeline using builder pattern
                    var builder = PipelineBuilder
                        .BeginWith(new BufferBlock<int>(
                            new ExecutionDataflowBlockOptions
                            {
                                CancellationToken = ct
                            }))
                        .LinkTo(new TransformBlock<int, int>(
                            item =>
                            {
                                var doubled = item * 2; // work
                                return doubled;
                            },
                            new ExecutionDataflowBlockOptions
                            {
                                CancellationToken = ct
                            }))
                        .LinkTo(new ActionBlock<int>(
                            item =>
                            {
                                Interlocked.Add(ref sum, item);
                                Interlocked.Increment(ref count); // side-effect
                            },
                            new ExecutionDataflowBlockOptions
                            {
                                CancellationToken = ct
                            }));

                    // Materialize the pipeline as a sink
                    return builder.ToPipeline();
                });

            // 2) Materialize the sink using the type-specific API
            var sink = sinkDef.CreateSink(CancellationToken.None);

            // 3) Send N items asynchronously
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(sink, i);
            }

            // 4) Complete and await completion
            sink.Complete();
            await ((IDataflowBlock)sink).Completion;

            // 5) Verify count and sum of doubled values
            Assert.Equal(N, count);
            long expectedSum = (long)N * (N - 1);
            Assert.Equal(expectedSum, sum);
        }

        [Fact]
        public async Task SinkPipelineDefinition_Via_Interface_Processes_All_Items()
        {
            const int N = 5_000;
            int count = 0;
            long sum = 0;

            // 1) Define a sink pipeline using the factory
            var sinkDef = PipelineFactory.CreateSink<int>(
                name: "DoubleAndCountSink",
                factory: ct =>
                {
                    return new ActionBlock<int>(
                        item =>
                        {
                            var doubled = item * 2;
                            Interlocked.Add(ref sum, doubled);
                            Interlocked.Increment(ref count);
                        },
                        new ExecutionDataflowBlockOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount,
                            CancellationToken = ct
                        });
                });

            // 2) Using the generic interface approach
            IPipelineDefinition genericPipeline = sinkDef;
            var sink = (ITargetBlock<int>)genericPipeline.Create(CancellationToken.None);

            // 3) Send N items asynchronously
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(sink, i);
            }

            // 4) Complete and await completion
            sink.Complete();
            await ((IDataflowBlock)sink).Completion;

            // 5) Verify count and sum of doubled values
            Assert.Equal(N, count);
            long expectedSum = (long)N * (N - 1);
            Assert.Equal(expectedSum, sum);
        }
    }
}