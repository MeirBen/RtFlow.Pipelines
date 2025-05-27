using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    /// <summary>
    /// Tests that verify PipelineHub cancellation behavior, specifically testing
    /// the integration between the factory's CancellationTokenSource and hub disposal.
    /// </summary>
    public class PipelineHubCancellationTests
    {
        [Fact]
        public async Task DisposeAsync_Should_Cancel_Factory_Token_And_Signal_All_Pipelines()
        {
            // Arrange
            var cancellationDetected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pipelineStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var itemsProcessed = 0;

            await using var hub = new PipelineHub(new PipelineFactory());

            // Create a pipeline that will detect cancellation via the factory's token
            var pipeline = hub.GetOrCreatePipeline(
                "CancellationTestPipeline",
                factory => factory
                    .Create<int>()
                    .TransformAsync(async (x, token) =>
                    {
                        pipelineStarted.TrySetResult(true);
                        try
                        {
                            // This should get cancelled when hub disposes
                            await Task.Delay(5000, token);
                            Interlocked.Increment(ref itemsProcessed);
                            return x * 2;
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationDetected.TrySetResult(true);
                            throw;
                        }
                    })
                    .ToPipeline());

            // Start processing
            await pipeline.SendAsync(42);
            
            // Wait for pipeline to start processing
            await pipelineStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act - dispose the hub, which should cancel the factory token
            await hub.DisposeAsync();

            // Assert - verify cancellation was detected
            var wasDetected = await cancellationDetected.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(wasDetected, "Cancellation should have been detected in the pipeline");
            Assert.Equal(0, itemsProcessed); // No items should have been fully processed
        }

        [Fact]
        public async Task Dispose_Should_Cancel_Factory_Token_And_Signal_All_Pipelines()
        {
            // Arrange
            var cancellationDetected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pipelineStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var itemsProcessed = 0;

            using var hub = new PipelineHub(new PipelineFactory());

            // Create a pipeline that will detect cancellation via the factory's token
            var pipeline = hub.GetOrCreatePipeline(
                "SyncCancellationTestPipeline",
                factory => factory
                    .Create<int>()
                    .TransformAsync(async (x, token) =>
                    {
                        pipelineStarted.TrySetResult(true);
                        try
                        {
                            // This should get cancelled when hub disposes
                            await Task.Delay(5000, token);
                            Interlocked.Increment(ref itemsProcessed);
                            return x * 2;
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationDetected.TrySetResult(true);
                            throw;
                        }
                    })
                    .ToPipeline());

            // Start processing
            await pipeline.SendAsync(42);
            
            // Wait for pipeline to start processing
            await pipelineStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act - dispose the hub synchronously, which should cancel the factory token
            hub.Dispose();

            // Assert - verify cancellation was detected
            var wasDetected = await cancellationDetected.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(wasDetected, "Cancellation should have been detected in the pipeline");
            Assert.Equal(0, itemsProcessed); // No items should have been fully processed
        }

        [Fact]
        public async Task Multiple_Pipelines_Should_All_Receive_Cancellation_On_Hub_Dispose()
        {
            // Arrange
            var cancellationDetected1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationDetected2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationDetected3 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            var pipelinesStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var startedCount = 0;

            await using var hub = new PipelineHub(new PipelineFactory());

            // Create multiple pipelines that all use the factory's cancellation token
            var pipeline1 = hub.GetOrCreatePipeline(
                "Pipeline1",
                factory => factory
                    .Create<int>()
                    .TransformAsync(async (x, token) =>
                    {
                        if (Interlocked.Increment(ref startedCount) == 3)
                            pipelinesStarted.TrySetResult(true);
                        
                        try
                        {
                            await Task.Delay(5000, token);
                            return x * 2;
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationDetected1.TrySetResult(true);
                            throw;
                        }
                    })
                    .ToPipeline());

            var pipeline2 = hub.GetOrCreatePipeline(
                "Pipeline2",
                factory => factory
                    .Create<string>()
                    .TransformAsync(async (s, token) =>
                    {
                        if (Interlocked.Increment(ref startedCount) == 3)
                            pipelinesStarted.TrySetResult(true);
                        
                        try
                        {
                            await Task.Delay(5000, token);
                            return s.ToUpper();
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationDetected2.TrySetResult(true);
                            throw;
                        }
                    })
                    .ToPipeline());

            var sinkPipeline = hub.GetOrCreateSinkPipeline(
                "SinkPipeline",
                factory => factory
                    .Create<double>()
                    .ToSinkAsync(async (d, token) =>
                    {
                        if (Interlocked.Increment(ref startedCount) == 3)
                            pipelinesStarted.TrySetResult(true);
                        
                        try
                        {
                            await Task.Delay(5000, token);
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationDetected3.TrySetResult(true);
                            throw;
                        }
                    }));

            // Start all pipelines
            await pipeline1.SendAsync(42);
            await pipeline2.SendAsync("hello");
            await sinkPipeline.SendAsync(3.14);

            // Wait for all pipelines to start
            await pipelinesStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act - dispose the hub
            await hub.DisposeAsync();

            // Assert - all pipelines should detect cancellation
            var detected1 = await cancellationDetected1.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var detected2 = await cancellationDetected2.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var detected3 = await cancellationDetected3.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(detected1, "Pipeline1 should detect cancellation");
            Assert.True(detected2, "Pipeline2 should detect cancellation");
            Assert.True(detected3, "SinkPipeline should detect cancellation");
        }

        [Fact]
        public async Task Factory_Token_Cancellation_Should_Propagate_Through_Linked_Tokens()
        {
            // Arrange
            var cancellationDetected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pipelineStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            using var externalCts = new CancellationTokenSource();
            using var factory = new PipelineFactory();
            await using var hub = new PipelineHub(factory);

            // Create a pipeline with an external cancellation token
            // The factory should link this with its own token
            var pipeline = hub.GetOrCreatePipeline(
                "LinkedTokenPipeline",
                factory => factory
                    .Create<int>(cancellationToken: externalCts.Token)
                    .TransformAsync(async (x, token) =>
                    {
                        pipelineStarted.TrySetResult(true);
                        try
                        {
                            await Task.Delay(5000, token);
                            return x * 2;
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationDetected.TrySetResult(true);
                            throw;
                        }
                    })
                    .ToPipeline());

            // Start processing
            await pipeline.SendAsync(42);
            
            // Wait for pipeline to start
            await pipelineStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act - dispose the hub (should cancel factory token, which should cancel linked token)
            await hub.DisposeAsync();

            // Assert - cancellation should be detected even though we used an external token
            var wasDetected = await cancellationDetected.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(wasDetected, "Cancellation should propagate through linked tokens");
        }

        [Fact]
        public async Task External_Token_Cancellation_Should_Not_Affect_Factory_Token()
        {
            // Arrange
            var factoryTokenCancelled = false;
            var externalTokenCancelled = false;
            var pipelineStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            using var externalCts = new CancellationTokenSource();
            await using var hub = new PipelineHub(new PipelineFactory());

            // Create a pipeline with external token
            var pipeline = hub.GetOrCreatePipeline(
                "ExternalTokenPipeline",
                factory => factory
                    .Create<int>(cancellationToken: externalCts.Token)
                    .TransformAsync(async (x, token) =>
                    {
                        pipelineStarted.TrySetResult(true);
                        try
                        {
                            await Task.Delay(5000, token);
                            return x * 2;
                        }
                        catch (OperationCanceledException)
                        {
                            externalTokenCancelled = true;
                            throw;
                        }
                    })
                    .ToPipeline());

            // Create another pipeline without external token to monitor factory token
            var factoryPipeline = hub.GetOrCreatePipeline(
                "FactoryTokenPipeline",
                factory => factory
                    .Create<int>()
                    .TransformAsync(async (x, token) =>
                    {
                        try
                        {
                            await Task.Delay(10000, token); // Long delay
                            return x * 2;
                        }
                        catch (OperationCanceledException)
                        {
                            factoryTokenCancelled = true;
                            throw;
                        }
                    })
                    .ToPipeline());

            // Start first pipeline
            await pipeline.SendAsync(42);
            await pipelineStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            
            // Start factory pipeline (no need to wait for it to start processing)
            await factoryPipeline.SendAsync(100);

            // Act - cancel the external token (should not affect factory token)
            await externalCts.CancelAsync();

            // Give some time for cancellation to propagate
            await Task.Delay(500);

            // Assert - external token should be cancelled, factory token should not be
            Assert.True(externalTokenCancelled, "External token should be cancelled");
            Assert.False(factoryTokenCancelled, "Factory token should not be cancelled by external token");

            // Cleanup - dispose hub to cancel factory token
            await hub.DisposeAsync();
        }

        [Fact]
        public async Task Hub_Should_Complete_Pipelines_After_Cancellation()
        {
            // Arrange
            var pipelineCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pipelineStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var hub = new PipelineHub(new PipelineFactory());

            var pipeline = hub.GetOrCreatePipeline(
                "CompletionTestPipeline",
                factory => factory
                    .Create<int>()
                    .TransformAsync(async (x, token) =>
                    {
                        pipelineStarted.TrySetResult(true);
                        try
                        {
                            await Task.Delay(5000, token);
                            return x * 2;
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationReceived.TrySetResult(true);
                            throw;
                        }
                    })
                    .ToPipeline());

            // Monitor pipeline completion
            _ = Task.Run(async () =>
            {
                try
                {
                    await pipeline.Completion;
                    pipelineCompleted.TrySetResult(true);
                }
                catch
                {
                    pipelineCompleted.TrySetResult(true); // Still consider it completed even if faulted
                }
            });

            // Start processing
            await pipeline.SendAsync(42);
            await pipelineStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act - dispose the hub
            await hub.DisposeAsync();

            // Assert - verify cancellation was received and pipeline completed
            var cancellationDetected = await cancellationReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var completed = await pipelineCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(cancellationDetected, "Pipeline should receive cancellation");
            Assert.True(completed, "Pipeline should complete after cancellation and explicit Complete() call");
        }

        [Fact]
        public void Factory_Token_Should_Be_Available_And_Not_Cancelled_Initially()
        {
            // Arrange & Act
            using var hub = new PipelineHub(new PipelineFactory());
            var factory = new PipelineFactory();

            // Assert
            Assert.NotNull(factory.CancellationTokenSource);
            Assert.False(factory.CancellationTokenSource.Token.IsCancellationRequested);
        }

        [Fact]
        public async Task Rapid_Dispose_Should_Not_Cause_Race_Conditions()
        {
            // Arrange
            var tasks = new List<Task>();
            
            // Act - create and dispose multiple hubs rapidly
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await using var hub = new PipelineHub(new PipelineFactory());
                    
                    var pipeline = hub.GetOrCreatePipeline(
                        $"RapidPipeline_{i}",
                        factory => factory
                            .Create<int>()
                            .Transform(x => x * 2)
                            .ToPipeline());

                    await pipeline.SendAsync(i);
                    // Immediate dispose
                }));
            }

            // Assert - all operations should complete without exceptions
            await Task.WhenAll(tasks);
        }
    }
}
