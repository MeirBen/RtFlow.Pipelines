using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    /// <summary>
    /// Tests for the core pipeline transformation functionality without cancellation.
    /// </summary>
    public class FluentPipelineTests
    {
        [Fact]
        public async Task Transform_ProcessesValues_Correctly()
        {
            // Arrange: pipeline that multiplies by 2
            var pipeline = FluentPipeline
                .Create<int>(opts => opts.BoundedCapacity = 10)
                .Transform(x => x * 2)
                .ToPipeline();

            var results = new List<int>();
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    results.Add(await DataflowBlock.ReceiveAsync(pipeline));
            });

            // Act
            for (int i = 1; i <= 5; i++)
                await pipeline.SendAsync(i);
            pipeline.Complete();
            await Task.WhenAll(receiver, pipeline.Completion);

            // Assert
            Assert.Equal(new[] { 2, 4, 6, 8, 10 }, results);
        }

        [Fact]
        public async Task TransformAsync_ProcessesValuesAsynchronously()
        {
            // Arrange: pipeline that multiplies by 2 asynchronously
            var pipeline = FluentPipeline
                .Create<int>(opts => opts.BoundedCapacity = 10)
                .TransformAsync(x => Task.FromResult(x * 2))
                .ToPipeline();

            var results = new List<int>();
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    results.Add(await DataflowBlock.ReceiveAsync(pipeline));
            });

            // Act
            for (int i = 1; i <= 5; i++)
                await pipeline.SendAsync(i);
            pipeline.Complete();
            await Task.WhenAll(receiver, pipeline.Completion);

            // Assert
            Assert.Equal(new[] { 2, 4, 6, 8, 10 }, results);
        }

        [Fact]
        public async Task Tap_InvokesSideEffect_AndPassesThrough()
        {
            // Arrange: pipeline that adds 1 then taps
            var tapped = new List<int>();
            var pipeline = FluentPipeline
                .Create<int>()
                .Transform(x => x + 1)
                .Tap(tapped.Add)
                .ToPipeline();

            var outputs = new List<int>();
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    outputs.Add(await DataflowBlock.ReceiveAsync(pipeline));
            });

            // Act
            for (int i = 0; i < 5; i++)
                await pipeline.SendAsync(i);
            pipeline.Complete();
            await Task.WhenAll(receiver, pipeline.Completion);

            // Assert
            Assert.Equal(outputs, tapped);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, outputs);
        }

        [Fact]
        public async Task Batch_GroupsItems_CorrectlyEvenIfIncomplete()
        {
            // Arrange: batches of 3
            var pipeline = FluentPipeline
                .Create<int>()
                .Batch(batchSize: 3)
                .ToPipeline();

            var batches = new List<int[]>();
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    batches.Add(await DataflowBlock.ReceiveAsync(pipeline));
            });

            // Act
            for (int i = 1; i <= 5; i++)
                await pipeline.SendAsync(i);
            pipeline.Complete();
            await Task.WhenAll(receiver, pipeline.Completion);

            // Assert
            Assert.Equal(2, batches.Count);
            Assert.Equal(new[] { 1, 2, 3 }, batches[0]);
            Assert.Equal(new[] { 4, 5 }, batches[1]);
        }

        [Fact]
        public async Task ToSink_ConsumesAllItems()
        {
            // Arrange
            var results = new List<int>();
            var target = FluentPipeline
                .Create<int>()
                .ToSink(results.Add);

            // Act
            await target.SendAsync(42);
            await target.SendAsync(43);
            target.Complete();
            await target.Completion;

            // Assert
            Assert.Equal(new[] { 42, 43 }, results);
        }

        [Fact]
        public async Task BeginWith_CustomHead_AllowsAnyStartingBlock()
        {
            // Arrange: start with a TransformBlock<string,string>
            var head = new TransformBlock<string, string>(s => s.ToUpper());
            var pipeline = FluentPipeline
                .BeginWith(head)
                .Transform(s => $"{s}!")
                .ToPipeline();

            var results = new List<string>();
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    results.Add(await DataflowBlock.ReceiveAsync(pipeline));
            });

            // Act
            await pipeline.SendAsync("abc");
            await pipeline.SendAsync("XyZ");
            pipeline.Complete();
            await Task.WhenAll(receiver, pipeline.Completion);

            // Assert
            Assert.Equal(new[] { "ABC!", "XYZ!" }, results);
        }

        [Fact]
        public async Task Complex_Chain_Of_Operations_Works_EndToEnd()
        {
            // Arrange
            var logged = new List<string>();
            var pipeline = FluentPipeline
                .Create<int>(opts => opts.BoundedCapacity = 20)
                .Transform(
                    x => x * x,
                    opts => opts.BoundedCapacity = 10
                )
                .Batch(
                    batchSize: 2,
                    opts => opts.BoundedCapacity = 5
                )
                .Transform(
                    arr => $"[{string.Join(',', arr)}]",
                    opts => opts.BoundedCapacity = 5
                )
                .Tap(logged.Add)
                .ToPipeline();

            var outputs = new List<string>();
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    outputs.Add(await DataflowBlock.ReceiveAsync(pipeline));
            });

            // Act: send 1..5
            for (int i = 1; i <= 5; i++)
                await pipeline.SendAsync(i);

            pipeline.Complete();
            await Task.WhenAll(receiver, pipeline.Completion);

            // Assert
            Assert.Equal(outputs, logged);
            Assert.Contains("[1,4]", outputs);
            Assert.Contains("[9,16]", outputs);
            Assert.Contains("[25]", outputs);
        }

        [Fact]
        public async Task TapAsync_InvokesSideEffect_AndPassesThrough()
        {
            // Arrange: pipeline that adds 1 then taps asynchronously
            var tapped = new List<int>();
            var pipeline = FluentPipeline
                .Create<int>()
                .Transform(x => x + 1)
                .TapAsync(x =>
                {
                    tapped.Add(x);
                    return Task.CompletedTask;
                })
                .ToPipeline();

            var outputs = new List<int>();
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    outputs.Add(await DataflowBlock.ReceiveAsync(pipeline));
            });

            // Act
            for (int i = 0; i < 5; i++)
                await pipeline.SendAsync(i);
            pipeline.Complete();
            await Task.WhenAll(receiver, pipeline.Completion);

            // Assert
            Assert.Equal(outputs, tapped);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, outputs);
        }

        [Fact]
        public async Task ToSinkAsync_ConsumesAllItemsAsynchronously()
        {
            // Arrange
            var results = new List<int>();
            var target = FluentPipeline
                .Create<int>()
                .ToSinkAsync(x =>
                {
                    results.Add(x);
                    return Task.CompletedTask;
                });

            // Act
            await target.SendAsync(42);
            await target.SendAsync(43);
            target.Complete();
            await target.Completion;

            // Assert
            Assert.Equal(new[] { 42, 43 }, results);
        }

        [Fact]
        public async Task ToSink_WithBatchAndTransform_ProcessesAndConsumesItems()
        {
            // Arrange
            var processedBatches = new List<string>();
            var pipeline = FluentPipeline
                .Create<int>()
                .Batch(batchSize: 2)
                .Transform(batch => $"Batch total: {batch.Sum()}")
                .ToSinkAsync(async result =>
                {
                    // Simulate async processing
                    await Task.Delay(10);
                    processedBatches.Add(result);
                });

            // Act
            for (int i = 1; i <= 5; i++)
                await pipeline.SendAsync(i);

            pipeline.Complete();
            await pipeline.Completion;

            // Assert
            Assert.Equal(3, processedBatches.Count);
            Assert.Equal("Batch total: 3", processedBatches[0]);  // 1+2
            Assert.Equal("Batch total: 7", processedBatches[1]);  // 3+4
            Assert.Equal("Batch total: 5", processedBatches[2]);  // 5
        }

        [Fact]
        public async Task ToSink_WithTransform_ProcessesAndConsumesItems()
        {
            // Arrange
            var results = new List<string>();
            var pipeline = FluentPipeline
                .Create<int>()
                .Transform(x => $"Number: {x * 2}")
                .ToSink(results.Add);

            // Act
            await pipeline.SendAsync(10);
            await pipeline.SendAsync(20);
            await pipeline.SendAsync(30);
            pipeline.Complete();
            await pipeline.Completion;

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("Number: 20", results[0]);
            Assert.Equal("Number: 40", results[1]);
            Assert.Equal("Number: 60", results[2]);
        }
    }
}
