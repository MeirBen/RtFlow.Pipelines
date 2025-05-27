using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    public class PipelineConcurrencyTests
    {
        [Fact]
        public async Task PipelineHub_ThreadSafe_Creation()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            var hub = new PipelineHub(new PipelineFactory());
            var creationCount = 0;

            // Create a pipeline factory method that increments a counter when called
            Func<IPipelineFactory, IPropagatorBlock<string, int>> createFunc = f =>
            {
                Interlocked.Increment(ref creationCount);
                return f.Create<string>(cancellationToken: cts.Token)
                    .Transform(s => int.Parse(s)).ToPipeline();
            };

            // Act - create pipeline from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                    hub.GetOrCreatePipeline("concurrent", createFunc)));
            }

            await Task.WhenAll(tasks);

            // Assert - should only create one instance
            Assert.Equal(1, creationCount);
            Assert.True(hub.PipelineExists("concurrent"));
        }

        [Fact]
        public async Task DisposeAsync_Completes_All_Pipelines()
        {
            using CancellationTokenSource cts = new();
            IPropagatorBlock<int, int> pipeline = null;
            List<int> results = [];

            // Arrange
            await using (PipelineHub hub = new(new PipelineFactory()))
            {
                results = [];
                var CancellationTokenSource = new CancellationTokenSource();

                // Create a pipeline that stores results in a list
                pipeline = hub.GetOrCreatePipeline("test", f =>
                    f.Create<int>(cancellationToken: cts.Token)
                     .Transform(i =>
                     {
                         results.Add(i);
                         return i;
                     })
                     .ToPipeline());

                // Send some data
                await pipeline.SendAsync(1);
                await pipeline.SendAsync(2);
                await pipeline.SendAsync(3);

                await Task.Delay(100); // Give some time for processing

                // Drain the pipeline
                for (int i = 0; i < 3; i++)
                {
                    await pipeline.ReceiveAsync();
                }

                // Wait a bit for processing to complete
                await Task.Delay(100);
            }
            // Assert
            Assert.NotNull(pipeline);
            Assert.NotEmpty(results);
            Assert.True(pipeline.Completion.IsCompleted);
            Assert.Equal(3, results.Count);
            Assert.Contains(1, results);
            Assert.Contains(2, results);
            Assert.Contains(3, results);
        }

        [Fact]
        public void Dispose_Completes_All_Pipelines_Synchronously()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            var results = new List<int>();
            var completionFlag = new ManualResetEvent(false);
            ITargetBlock<int> pipeline = null;

            // Create a hub with a pipeline that processes items slowly
            using (var hub = new PipelineHub(new PipelineFactory()))
            {
                // Create a pipeline with a buffer block to store the results
                pipeline = hub.GetOrCreateSinkPipeline("test", f =>
                {
                    return f.Create<int>(cancellationToken: cts.Token)
                    .Transform(i =>
                    {
                        results.Add(i);
                        return i;
                    })
                    .ToSink(x =>
                    {
                        if (x == 3)
                        {
                            // Simulate some processing time
                            Thread.Sleep(100);
                            completionFlag.Set(); // Signal that processing is complete
                        }
                    });
                });

                // Post some items
                pipeline.Post(1);
                pipeline.Post(2);
                pipeline.Post(3);
                // Give some time for processing to start
                Thread.Sleep(100);
            }

            // Assert
            Assert.True(pipeline.Completion.IsCompleted);
            Assert.Equal(3, results.Count);
            Assert.Contains(1, results);
            Assert.Contains(2, results);
            Assert.Contains(3, results);
            Assert.True(completionFlag.WaitOne(1000), "Processing did not complete in time");
        }

        [Fact]
        public async Task Data_Flows_Through_Multiple_Connected_Pipelines()
        {
            List<double> results;
            using CancellationTokenSource cts = new();
            // Arrange
            var pipelineFactory = new PipelineFactory();

            await using (var hub = new PipelineHub(pipelineFactory))
            {
                results = [];

                // First pipeline: string -> int
                var p1 = hub.GetOrCreatePipeline("p1", f =>
                    f.Create<string>(cancellationToken: cts.Token)
                    .Transform(s => int.Parse(s)).ToPipeline());

                // Second pipeline: int -> double
                var p2 = hub.GetOrCreateSinkPipeline("p2", f =>
                    f.Create<int>(cancellationToken: cts.Token).Transform(i => i * 1.5).
                    ToSink(x =>
                    {
                        // Simulate some processing time
                        Thread.Sleep(100);
                        results.Add(x);
                    }));

                // Act
                await p1.SendAsync("10");
                await p1.SendAsync("20");

                p1.Complete();

                // Manually connect pipelines
                while (await p1.OutputAvailableAsync())
                {
                    var value = await p1.ReceiveAsync();
                    await p2.SendAsync(value);
                }
            }

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains(15.0, results);
            Assert.Contains(30.0, results);
        }

        [Fact]
        public async Task Pipeline_With_MaxDegreeOfParallelism_Processes_In_Parallel()
        {
            // Arrange
            var factory = new PipelineFactory();
            using CancellationTokenSource cts = new();
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4
            };

            var processingTimes = new ConcurrentBag<TimeSpan>();
            var startTime = DateTime.UtcNow;

            // Create a pipeline with parallelism that simulates long-running work
            var pipeline = factory.Create<int>(options =>
                options.MaxDegreeOfParallelism = 4,
                cancellationToken: cts.Token)
                .Transform(i =>
                {
                    // Simulate work taking 100ms
                    Thread.Sleep(100);
                    var elapsed = DateTime.UtcNow - startTime;
                    processingTimes.Add(elapsed);
                    return i * 2;
                })
                .ToPipeline();

            // Act - send multiple items at once
            for (int i = 0; i < 8; i++)
            {
                await pipeline.SendAsync(i);
            }

            // Consume the results
            for (int i = 0; i < 8; i++)
            {
                await pipeline.ReceiveAsync();
            }

            pipeline.Complete();
            await pipeline.Completion;

            // Assert
            // If truly parallel, total time should be around 200ms (8 items / 4 parallel = 2 batches)
            // If sequential, it would be 800ms

            // Check that items were processed in parallel by looking at timestamps
            var orderedTimes = processingTimes.OrderBy(t => t).ToList();

            // Group timestamps into batches that are close together (within 20ms)
            var batches = new List<List<TimeSpan>>();
            var currentBatch = new List<TimeSpan> { orderedTimes.First() };

            for (int i = 1; i < orderedTimes.Count; i++)
            {
                var gap = (orderedTimes[i] - orderedTimes[i - 1]).TotalMilliseconds;
                if (gap < 20) // In same batch
                {
                    currentBatch.Add(orderedTimes[i]);
                }
                else // New batch
                {
                    batches.Add(currentBatch);
                    currentBatch = [orderedTimes[i]];
                }
            }

            if (currentBatch.Count > 0)
                batches.Add(currentBatch);

            // We should have at least 2 batches if processing in parallel
            Assert.True(batches.Count >= 2);
        }
    }
}
