using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using RtFlow.Pipelines.Core.Utils;

namespace RtFlow.Pipelines.Tests
{
    public class PipelineSmokeTests
    {
        [Fact]
        public async Task Million_Items_Pipeline_With_Type_Specific_API_Completes_Correctly()
        {
            const int N = 1_000_000; // Reduced from 15M for faster test execution

            // 1) Define pipeline using the factory
            var pipeline = PipelineFactory.CreatePipeline<int, int>(
                "double-values",
                ct => PipelineBuilder
                        .BeginWith(new BufferBlock<int>(new() { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<int, int>(
                            x => x * 2,
                            new ExecutionDataflowBlockOptions
                            {
                                MaxDegreeOfParallelism = Environment.ProcessorCount,
                                CancellationToken = ct
                            }))
                        .ToPipeline()
            );

            // 2) Create it using the type-specific API
            var propagator = pipeline.Create(CancellationToken.None);

            long sum = 0;
            int count = 0;

            // 3) Drain outputs asynchronously, updating count & sum
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(propagator))
                {
                    var item = await DataflowBlock.ReceiveAsync(propagator);
                    Interlocked.Add(ref sum, item);
                    Interlocked.Increment(ref count);
                }
            });

            // 4) Push N inputs asynchronously
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(propagator, i);
            }

            // 5) Complete & wait
            propagator.Complete();
            await Task.WhenAll(receiver, propagator.Completion);

            // 6) Verify count and sum of doubled values
            Assert.Equal(N, count);
            long expectedSum = (long)N * (N - 1);
            Assert.Equal(expectedSum, sum);
        }

        [Fact]
        public async Task Pipeline_Via_Interface_Processes_All_Items()
        {
            const int N = 100_000;

            // 1) Define pipeline using the factory
            var pipeline = PipelineFactory.CreatePipeline<int, string>(
                "int-to-string-converter",
                ct => new TransformBlock<int, string>(
                    x => $"Item {x} doubled: {x * 2}",
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = ct
                    })
            );

            // 2) Create using the interface approach
            IPipelineDefinition genericPipeline = pipeline;
            var block = (IPropagatorBlock<int, string>)genericPipeline.Create(CancellationToken.None);

            int count = 0;
            List<string> results = new List<string>();

            // 3) Drain outputs asynchronously
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(block))
                {
                    var item = await DataflowBlock.ReceiveAsync(block);
                    results.Add(item);
                    Interlocked.Increment(ref count);
                }
            });

            // 4) Push N inputs asynchronously
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(block, i);
            }

            // 5) Complete & wait
            block.Complete();
            await Task.WhenAll(receiver, block.Completion);

            // 6) Verify results
            Assert.Equal(N, count);
            Assert.Equal(N, results.Count);
            Assert.Contains($"Item 42 doubled: 84", results);
        }

        [Fact]
        public async Task Complex_Pipeline_With_Multiple_Stages()
        {
            const int N = 10_000;

            // 1) Define a more complex pipeline with multiple stages
            var complexPipeline = PipelineFactory.CreatePipeline<int, string>(
                name: "complex-multi-stage",
                factory: ct =>
                {
                    // Multiple stages of processing
                    return PipelineBuilder
                        .BeginWith(new BufferBlock<int>(new() { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<int, double>(
                            x => Math.Sqrt(x),
                            new ExecutionDataflowBlockOptions { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<double, (double value, string category)>(
                            x => (x, x < 50 ? "small" : "large"),
                            new ExecutionDataflowBlockOptions { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<(double value, string category), string>(
                            item => $"{item.category}: {item.value:F2}",
                            new ExecutionDataflowBlockOptions { CancellationToken = ct }))
                        .ToPipeline();
                });

            // 2) Create it 
            var propagator = complexPipeline.Create(CancellationToken.None);

            var results = new List<string>();
            int count = 0;

            // 3) Collect results
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(propagator))
                {
                    var item = await DataflowBlock.ReceiveAsync(propagator);
                    results.Add(item);
                    Interlocked.Increment(ref count);
                }
            });

            // 4) Send items
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(propagator, i);
            }

            // 5) Complete and wait
            propagator.Complete();
            await Task.WhenAll(receiver, propagator.Completion);

            // 6) Verify results
            Assert.Equal(N, count);
            Assert.Equal(N, results.Count);
            Assert.Contains("small: 3.00", results);
            Assert.Contains("small: 7.00", results);
            Assert.Contains("large: 50.00", results);
            Assert.Contains("large: 90.02", results);
        }

        [Fact]
        public async Task Pipeline_With_BatchBlock_And_BatchProcessing()
        {
            const int N = 50_000;
            const int batchSize = 100;

            // 1) Define pipeline with batch processing
            var batchPipeline = PipelineFactory.CreatePipeline<int, double>(
                "batch-average-calculator",
                ct =>
                {
                    // Create pipeline with batching
                    return PipelineBuilder
                        .BeginWith(new BufferBlock<int>(
                            new ExecutionDataflowBlockOptions
                            {
                                BoundedCapacity = N * 2,
                                CancellationToken = ct
                            }))
                        .LinkTo(new BatchBlock<int>(
                            batchSize,
                            new GroupingDataflowBlockOptions
                            {
                                BoundedCapacity = N / batchSize,
                                CancellationToken = ct
                            }))
                        .LinkTo(new TransformBlock<int[], double>(
                            batch =>
                            {
                                // Calculate the average of the batch
                                return batch.Average();
                            },
                            new ExecutionDataflowBlockOptions
                            {
                                MaxDegreeOfParallelism = Environment.ProcessorCount,
                                CancellationToken = ct
                            }))
                        .ToPipeline();
                });

            // 2) Create it
            var propagator = batchPipeline.Create(CancellationToken.None);

            var averages = new List<double>();
            int count = 0;

            // 3) Collect results
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(propagator))
                {
                    var average = await DataflowBlock.ReceiveAsync(propagator);
                    averages.Add(average);
                    Interlocked.Increment(ref count);
                }
            });

            // 4) Send sequential numbers
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(propagator, i);
            }

            // 5) Complete & wait
            propagator.Complete();
            await Task.WhenAll(receiver, propagator.Completion);

            // 6) Verify results
            int expectedBatches = N / batchSize + (N % batchSize > 0 ? 1 : 0);
            Assert.Equal(expectedBatches, count);
            Assert.Equal(expectedBatches, averages.Count);
            
            // Verify some batch averages
            // First batch should be 0-99 with average 49.5
            Assert.Contains(49.5, averages);
            
            // Second batch should be 100-199 with average 149.5
            Assert.Contains(149.5, averages);
            
            // If each batch contains sequential numbers from i*batchSize to (i+1)*batchSize-1,
            // the average would be (i*batchSize + (i+1)*batchSize-1) / 2
            for (int i = 0; i < N/batchSize; i++)
            {
                double expectedAverage = (i * batchSize + (i+1) * batchSize - 1) / 2.0;
                Assert.Contains(expectedAverage, averages);
            }
        }
        
        [Fact]
        public async Task Pipeline_With_BatchBlock_Via_Interface()
        {
            const int N = 5_000;
            const int batchSize = 25;

            // 1) Define pipeline with batch processing that groups strings by length
            var batchPipeline = PipelineFactory.CreatePipeline<string, Dictionary<int, List<string>>>(
                "string-length-grouper",
                ct =>
                {
                    return PipelineBuilder
                        .BeginWith(new BufferBlock<string>())
                        .LinkTo(new BatchBlock<string>(
                            batchSize,
                            new GroupingDataflowBlockOptions
                            {
                                BoundedCapacity = 1000,
                                CancellationToken = ct
                            }))
                        .LinkTo(new TransformBlock<string[], Dictionary<int, List<string>>>(
                            batch =>
                            {
                                // Group strings in the batch by their length
                                var result = new Dictionary<int, List<string>>();
                                
                                foreach (var str in batch)
                                {
                                    int length = str.Length;
                                    if (!result.ContainsKey(length))
                                    {
                                        result[length] = new List<string>();
                                    }
                                    result[length].Add(str);
                                }
                                
                                return result;
                            },
                            new ExecutionDataflowBlockOptions
                            {
                                CancellationToken = ct
                            }))
                        .ToPipeline();
                });

            // 2) Create using the interface
            IPipelineDefinition genericPipeline = batchPipeline;
            var block = (IPropagatorBlock<string, Dictionary<int, List<string>>>)genericPipeline.Create(CancellationToken.None);

            var results = new List<Dictionary<int, List<string>>>();

            // 3) Drain results
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(block))
                {
                    var grouping = await DataflowBlock.ReceiveAsync(block);
                    results.Add(grouping);
                }
            });

            // 4) Send strings of varying lengths
            string[] testData = {
                "a", "bb", "ccc", "dddd", "eeeee", "ffffff", "ggggggg", "hhhhhhhh", "iiiiiiiii",
                "a", "bb", "ccc", "dddd", "eeeee", "ffffff", "ggggggg", "hhhhhhhh", "iiiiiiiii",
                // Add many more strings to fill batches
            };
            
            // Generate more test data to reach N
            List<string> allTestData = new List<string>(testData);
            
            for (int i = allTestData.Count; i < N; i++)
            {
                int length = i % 10 + 1; // Lengths from 1 to 10
                allTestData.Add(new string('x', length));
            }
            
            foreach (var str in allTestData)
            {
                await DataflowBlock.SendAsync(block, str);
            }

            // 5) Complete & wait
            block.Complete();
            await Task.WhenAll(receiver, block.Completion);

            // 6) Verify results
            int expectedBatches = N / batchSize + (N % batchSize > 0 ? 1 : 0);
            Assert.Equal(expectedBatches, results.Count);
            
            // Verify that we have dictionaries grouping strings by length
            foreach (var dict in results)
            {
                // Each dictionary should have strings grouped by length
                foreach (var lengthGroup in dict)
                {
                    // Every string in this group should have the same length
                    foreach (var str in lengthGroup.Value)
                    {
                        Assert.Equal(lengthGroup.Key, str.Length);
                    }
                }
            }
        }
    }
}