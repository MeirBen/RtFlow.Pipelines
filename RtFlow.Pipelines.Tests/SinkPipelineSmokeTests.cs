using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using RtFlow.Pipelines.Core.Utils; // <-- for the AsSink() extension

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

            // 1) Define a sink pipeline that doubles each item and increments a counter
            var sinkDef = new SinkPipelineDefinition<int>(
              name: "DoubleAndCountSink",
              factory: ct =>
              {
                  // a) start with a BufferBlock<int>
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

                  // c) materialize the propagator and then turn it into a sink
                  var propagator = builder.ToPipeline(); // IPropagatorBlock<int,int>
                  return propagator; // ITargetBlock<int>
              });

            // 2) Materialize the sink
            var sink = sinkDef.CreateSink(CancellationToken.None);

            // 3) Send N items asynchronously
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(sink, i);
            }

            // 4) Complete and await completion
            sink.Complete();
            await ((IDataflowBlock)sink).Completion;

            // 6) verify count and sum of doubled values
            Assert.Equal(N, count);
            long expectedSum = (long)N * (N - 1);
            Assert.Equal(expectedSum, sum);
        }
    }
}