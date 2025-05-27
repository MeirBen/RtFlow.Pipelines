using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    public class PipelineErrorHandlingTests
    {
        [Fact]
        public async Task Pipeline_Propagates_Exceptions_From_Transforms()
        {
            // Arrange
            var factory = new PipelineFactory();
            var pipeline = factory.Create<string>()
                .Transform<string>(_ => throw new InvalidOperationException("Test exception"))
                .ToPipeline();

            // Act
            await pipeline.SendAsync("test");

            // Assert - The pipeline will fault after the exception
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await pipeline.Completion);

            Assert.Equal("Test exception", exception.Message);
        }

        [Fact]
        public async Task Pipeline_Fault_Is_Observable_Through_Completion_Task()
        {
            // Arrange
            var factory = new PipelineFactory();
            var exceptionThrown = false;

            var pipeline = factory.Create<int>()
                .Transform(i =>
                {
                    if (i > 10) throw new ArgumentOutOfRangeException(nameof(i), "Value too large");
                    return i * 2;
                })
                .ToPipeline();

            // Setup completion task to observe fault
            _ = pipeline.Completion.ContinueWith(task =>
            {
                exceptionThrown = task.IsFaulted;
            });

            // Act - Send valid data first, then invalid
            await pipeline.SendAsync(5); // This will work

            try
            {
                await pipeline.SendAsync(20); // This will throw

                // Wait a bit to ensure the transform has a chance to execute
                await Task.Delay(500);

                // Try to complete the pipeline, which should propagate any errors
                pipeline.Complete();
                await pipeline.Completion;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Expected exception
                exceptionThrown = true;
            }

            // Assert
            Assert.True(exceptionThrown);
        }

        [Fact]
        public void PipelineHub_Throws_When_Getting_Pipeline_With_Wrong_Types()
        {
            // Arrange
            var hub = new PipelineHub(new PipelineFactory());
            hub.GetOrCreatePipeline("test", f =>
                f.Create<string>().Transform(s => int.Parse(s)).ToPipeline());

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                hub.GetPipeline<string, string>("test"));

            Assert.Contains("exists but with different types", exception.Message);
        }

        [Fact]
        public async Task PipelineHub_Raises_Events_When_Pipeline_Completes_Or_Faults()
        {
            // Arrange
            var hub = new PipelineHub(new PipelineFactory());
            var completedPipelineName = string.Empty;
            var faultedPipelineName = string.Empty;
            Exception faultingException = null;
            var faultEventReceived = new TaskCompletionSource<bool>();

            hub.PipelineCompleted += (sender, args) => completedPipelineName = args.PipelineName;
            hub.PipelineFaulted += (sender, args) =>
            {
                faultedPipelineName = args.PipelineName;
                faultingException = args.Exception;
                faultEventReceived.TrySetResult(true);
            };

            // Create normal pipeline
            var normalPipeline = hub.GetOrCreatePipeline(
                "normal",
                f => f.Create<int>().Transform(i => i * 2).ToPipeline());

            // Create faulting pipeline
            var faultingPipeline = hub.GetOrCreatePipeline(
                "faulting",
                f => f.Create<string>().Transform<int>(s =>
                {
                    throw new FormatException("Test exception");
                }).ToPipeline());

            // Act - send data and complete normal pipeline
            await normalPipeline.SendAsync(1);

            var result = await normalPipeline.ReceiveAsync();

            normalPipeline.Complete();
            await normalPipeline.Completion;

            // Send data to faulting pipeline which will trigger an exception
            await faultingPipeline.SendAsync("test");

            // Create a task that safely observes the pipeline fault
            async Task ObserveFaultAsync()
            {
                try
                {
                    await faultingPipeline.Completion;
                }
                catch (Exception)
                {
                    // Exception is expected - we just need to observe it
                }
            }

            // Launch the observation task and wait for the event or timeout
            var observationTask = ObserveFaultAsync();

            // Wait for the fault event to be raised with a reasonable timeout
            await Task.WhenAny(
                Task.WhenAll(observationTask, faultEventReceived.Task),
                Task.Delay(1000));

            // Assert
            Assert.Equal("normal", completedPipelineName);
            Assert.Equal("faulting", faultedPipelineName);
            Assert.IsType<FormatException>(faultingException);
            Assert.Equal("Test exception", faultingException.Message);
        }

        [Fact]
        public void PipelineHub_Can_Remove_Pipeline()
        {
            // Arrange
            var hub = new PipelineHub(new PipelineFactory());
            hub.GetOrCreatePipeline("test", f =>
                f.Create<string>().Transform(s => int.Parse(s)).ToPipeline());

            // Verify pipeline exists
            Assert.True(hub.PipelineExists("test"));

            // Act
            bool removed = hub.RemovePipeline("test");

            // Assert
            Assert.True(removed);
            Assert.False(hub.PipelineExists("test"));

            // Trying to get a removed pipeline should throw
            Assert.Throws<InvalidOperationException>(() => hub.GetPipeline<string, int>("test"));
        }
    }
}
