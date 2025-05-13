using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    public class FluentPipelineBuilderTests
    {
        [Fact]
        public async Task Transform_DoublesValues_Correctly()
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
        public async Task Tap_InvokesSideEffect_AndPassesThrough()
        {
            // Arrange: pipeline that adds 1 then taps
            var tapped = new List<int>();
            var pipeline = FluentPipeline
                .Create<int>()
                .Transform(x => x + 1)
                .Tap(x => tapped.Add(x))
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
        public async Task WithPostCompletionAction_IsInvokedAfterCompletion()
        {
            // Arrange
            var results = new List<int>();
            var invoked = false;
            var pipeline = FluentPipeline
                .Create<int>()
                .WithPostCompletionAction(_ => { invoked = true; })
                .ToPipeline();

            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    results.Add(await DataflowBlock.ReceiveAsync(pipeline));
            });

            // Act
            await pipeline.SendAsync(42);
            pipeline.Complete();
            await pipeline.Completion;

            // Assert
            Assert.True(invoked);
            Assert.Equal(new[] { 42 }, results);
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
                // buffer with capacity 20
                .Create<int>(opts => opts.BoundedCapacity = 20)
                // square each item, with a bounded capacity too
                .Transform(
                    x => x * x,
                    new ExecutionDataflowBlockOptions { BoundedCapacity = 10 }
                )
                // batch into arrays of 2
                .Batch(
                    batchSize: 2,
                    new GroupingDataflowBlockOptions { BoundedCapacity = 5 }
                )
                // format each batch
                .Transform(
                    arr => $"[{string.Join(',', arr)}]",
                    new ExecutionDataflowBlockOptions { BoundedCapacity = 5 }
                )
                // tap into your log
                .Tap(s => logged.Add(s))
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
    }
}
