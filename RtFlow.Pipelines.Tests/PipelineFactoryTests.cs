using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    /// <summary>
    /// Service X - responsible for pipeline configuration and initialization
    /// </summary>
    public class ServiceX
    {
        private readonly IPipelineHub _pipelineHub;
        private const string PipelineName = "StringToIntPipeline";

        public ServiceX(IPipelineHub pipelineHub)
        {
            _pipelineHub = pipelineHub;

            // Initialize the pipeline if it doesn't exist
            _pipelineHub.GetOrCreatePipeline(
                PipelineName,
                factory => factory
                    .Create<string>()
                    .Transform(s => int.Parse(s))
                    .ToPipeline());
        }

        public void CompletePipeline()
        {
            var pipeline = _pipelineHub.GetPipeline<string, int>(PipelineName);
            pipeline.Complete();
        }
    }

    /// <summary>
    /// Service Y - pushes data to the pipeline
    /// </summary>
    public class ServiceY
    {
        private readonly IPipelineHub _pipelineHub;
        private const string PipelineName = "StringToIntPipeline";

        public ServiceY(IPipelineHub pipelineHub)
        {
            _pipelineHub = pipelineHub;
        }

        public async Task PushDataAsync(string data)
        {
            // Get the shared pipeline and push data to it
            var pipeline = _pipelineHub.GetPipeline<string, int>(PipelineName);
            await pipeline.SendAsync(data);
        }
    }

    /// <summary>
    /// Service Z - consumes data from the pipeline
    /// </summary>
    public class ServiceZ
    {
        private readonly IPipelineHub _pipelineHub;
        private readonly List<int> _receivedData = [];
        private const string PipelineName = "StringToIntPipeline";

        public ServiceZ(IPipelineHub pipelineHub)
        {
            _pipelineHub = pipelineHub;
        }

        public async Task ConsumeDataAsync()
        {
            // Get the shared pipeline and consume data from it
            var pipeline = _pipelineHub.GetPipeline<string, int>(PipelineName);

            while (await pipeline.OutputAvailableAsync())
            {
                _receivedData.Add(await pipeline.ReceiveAsync());
            }
        }

        public List<int> GetReceivedData() => _receivedData;
    }

    /// <summary>
    /// Example of a service that produces logging events
    /// </summary>
    public class LogProducerService
    {
        private readonly IPipelineHub _pipelineHub;
        private const string LogPipelineName = "LoggingPipeline";

        public LogProducerService(IPipelineHub pipelineHub)
        {
            _pipelineHub = pipelineHub;

            // Initialize the logging sink if it doesn't exist yet
            _pipelineHub.GetOrCreateSinkPipeline(
                LogPipelineName,
                factory => factory
                    .Create<string>()
                    .ToSink(message => Console.WriteLine($"[LOG] {DateTime.Now}: {message}"))
            );
        }

        public async Task LogAsync(string message)
        {
            var pipeline = _pipelineHub.GetSinkPipeline<string>(LogPipelineName);
            await pipeline.SendAsync(message);
        }
    }

    /// <summary>
    /// Example of a service that produces metrics
    /// </summary>
    public class MetricService
    {
        private readonly IPipelineHub _pipelineHub;
        private const string MetricsPipelineName = "MetricsPipeline";

        private class Metric
        {
            public string Name { get; set; }
            public double Value { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public MetricService(IPipelineHub pipelineHub)
        {
            _pipelineHub = pipelineHub;

            // Example of a more complex pipeline with a sink at the end
            _pipelineHub.GetOrCreateSinkPipeline(
                MetricsPipelineName,
                factory => factory
                    .Create<KeyValuePair<string, double>>()
                    .Transform(kv => new Metric
                    {
                        Name = kv.Key,
                        Value = kv.Value,
                        Timestamp = DateTime.UtcNow
                    })
                    .ToSink(metric =>
                    {
                        // In a real app, this would send metrics to a monitoring system
                        Console.WriteLine($"[METRIC] {metric.Name}: {metric.Value}");
                    })
            );
        }

        public async Task TrackMetricAsync(string name, double value)
        {
            var pipeline = _pipelineHub.GetSinkPipeline<KeyValuePair<string, double>>(MetricsPipelineName);
            await pipeline.SendAsync(new KeyValuePair<string, double>(name, value));
        }
    }

    /// <summary>
    /// Tests that verify integration with cancellation tokens.
    /// </summary>
    public class PipelineFactoryTests
    {
        [Fact]
        public async Task Create_Pipeline_Observes_CancellationToken()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            var factory = new PipelineFactory();

            var pipeline = factory
                .Create<int>(cancellationToken: cts.Token)
                .TransformAsync(async (x, ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return x;
                })
                .ToPipeline();

            // Act
            var sendTask = pipeline.SendAsync(123);
            await cts.CancelAsync();

            // Assert: TPL Dataflow throws TaskCanceledException on cancellation
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await Task.WhenAll(sendTask, pipeline.Completion));
        }

        [Fact]
        public async Task BeginWith_Pipeline_Observes_CancellationToken()
        {
            // Arrange
            var factory = new PipelineFactory();
            using CancellationTokenSource cts = new();

            // Give the head block the same shutdown token
            var headOpts = new ExecutionDataflowBlockOptions
            {
                CancellationToken = cts.Token
            };
            var head = new BufferBlock<string>(headOpts);

            var pipeline = factory
                .BeginWith(head)
                .TransformAsync(async (s, ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return s;
                })
                .ToPipeline();

            // Act
            var sendTask = pipeline.SendAsync("hello");
            await cts.CancelAsync();

            // Assert: now cancellation propagates through head and all blocks
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await Task.WhenAll(sendTask, pipeline.Completion));
        }

        [Fact]
        public async Task Pipeline_Shared_Through_Hub_Used_By_Multiple_Services()
        {
            // Arrange
            var factory = new PipelineFactory();
            using CancellationTokenSource cts = new();

            // Create the pipeline hub
            var pipelineHub = new PipelineHub(factory);

            // Create services that all use the same hub
            var serviceX = new ServiceX(pipelineHub);
            var serviceY = new ServiceY(pipelineHub);
            var serviceZ = new ServiceZ(pipelineHub);

            // Act
            var consumeTask = Task.Run(serviceZ.ConsumeDataAsync);

            // Push data from Service Y
            await serviceY.PushDataAsync("123");
            await serviceY.PushDataAsync("456");
            await serviceY.PushDataAsync("789");

            // Complete the pipeline to allow the consumer to finish
            serviceX.CompletePipeline();
            await consumeTask;

            // Assert
            var receivedData = serviceZ.GetReceivedData();
            Assert.Equal(3, receivedData.Count);
            Assert.Equal(123, receivedData[0]);
            Assert.Equal(456, receivedData[1]);
            Assert.Equal(789, receivedData[2]);
        }

        [Fact]
        public async Task PipelineHub_Can_Manage_Multiple_Pipeline_Types()
        {
            // Arrange
            var factory = new PipelineFactory();
            var hub = new PipelineHub(factory);

            // Create multiple pipelines with different types
            var stringToIntPipeline = hub.GetOrCreatePipeline(
                "StringToInt",
                f => f.Create<string>()
                    .Transform(s => int.Parse(s))
                    .ToPipeline());

            var intToDoublePipeline = hub.GetOrCreatePipeline(
                "IntToDouble",
                f => f.Create<int>()
                    .Transform(i => i * 1.5)
                    .ToPipeline());

            var stringReverserPipeline = hub.GetOrCreatePipeline(
                "StringReverser",
                f => f.Create<string>()
                    .Transform(s => new string([.. s.Reverse()]))
                    .ToPipeline());

            // Act - use all pipelines
            await stringToIntPipeline.SendAsync("42");
            await intToDoublePipeline.SendAsync(10);
            await stringReverserPipeline.SendAsync("hello");

            // Get results
            var intResult = await stringToIntPipeline.ReceiveAsync();
            var doubleResult = await intToDoublePipeline.ReceiveAsync();
            var reverseResult = await stringReverserPipeline.ReceiveAsync();

            // Complete all pipelines
            await hub.CompleteAllAsync();

            // Assert
            Assert.Equal(42, intResult);
            Assert.Equal(15.0, doubleResult);
            Assert.Equal("olleh", reverseResult);

            // Verify we get the same pipeline instance when requesting it again
            var sameStringToIntPipeline = hub.GetPipeline<string, int>("StringToInt");
            Assert.Same(stringToIntPipeline, sameStringToIntPipeline);
        }

        [Fact]
        public async Task PipelineHub_Extensions_Simplify_Pipeline_Creation()
        {
            // Arrange
            var factory = new PipelineFactory();
            var hub = new PipelineHub(factory);

            // Create pipelines using extension methods
            var stringToIntPipeline = hub.CreateStringToIntPipeline("NumberParser");

            var doubleTransformPipeline = hub.CreateTransformPipeline<double, string>(
                "DoubleFormatter",
                d => d.ToString("F2"));

            var asyncPipeline = hub.CreateTransformAsyncPipeline<string, string>(
                "AsyncUppercase",
                async (s, ct) =>
                {
                    await Task.Delay(10, ct); // Simulate async work
                    return s.ToUpper();
                });

            // Act
            await stringToIntPipeline.SendAsync("42");
            await doubleTransformPipeline.SendAsync(3.14159);
            await asyncPipeline.SendAsync("hello world");

            var intResult = await stringToIntPipeline.ReceiveAsync();
            var formattedResult = await doubleTransformPipeline.ReceiveAsync();
            var upperResult = await asyncPipeline.ReceiveAsync();

            // Assert
            Assert.Equal(42, intResult);
            Assert.Equal("3.14", formattedResult);
            Assert.Equal("HELLO WORLD", upperResult);
        }

        [Fact]
        public async Task PipelineHub_Supports_Sink_Operations()
        {
            // Arrange
            var factory = new PipelineFactory();
            var hub = new PipelineHub(factory);

            var numbers = new List<int>();
            var doubleNumbers = new List<double>();
            var uppercaseStrings = new List<string>();

            // Create various sink pipelines
            var intSink = hub.CreateSinkPipeline<int>(
                "IntegerCollector",
                numbers.Add);

            var doubleTransformSink = hub.CreateTransformSinkPipeline<int, double>(
                "DoubleTransformer",
                n => n * 1.5,
                doubleNumbers.Add);

            var asyncSink = hub.CreateSinkAsyncPipeline<string>(
                "AsyncUppercaseCollector",
                async (s) =>
                {
                    await Task.Delay(10); // Simulate async work
                    uppercaseStrings.Add(s.ToUpper());
                });

            // Act
            await intSink.SendAsync(42);
            await intSink.SendAsync(100);

            await doubleTransformSink.SendAsync(10);
            await doubleTransformSink.SendAsync(20);

            await asyncSink.SendAsync("hello");
            await asyncSink.SendAsync("world");

            // Complete all pipelines and wait for them to finish
            await hub.CompleteAllAsync();

            // Assert
            Assert.Equal(2, numbers.Count);
            Assert.Equal(42, numbers[0]);
            Assert.Equal(100, numbers[1]);

            Assert.Equal(2, doubleNumbers.Count);
            Assert.Equal(15.0, doubleNumbers[0]);
            Assert.Equal(30.0, doubleNumbers[1]);

            Assert.Equal(2, uppercaseStrings.Count);
            Assert.Equal("HELLO", uppercaseStrings[0]);
            Assert.Equal("WORLD", uppercaseStrings[1]);

            // Check that the tasks completed successfully
            Assert.True(intSink.Completion.IsCompletedSuccessfully);
            Assert.True(doubleTransformSink.Completion.IsCompletedSuccessfully);
            Assert.True(asyncSink.Completion.IsCompletedSuccessfully);

            // Verify we get the same sink instance when requesting it again
            var sameSink = hub.GetSinkPipeline<int>("IntegerCollector");
            Assert.Same(intSink, sameSink);
        }

        [Fact]
        public async Task Multiple_Services_Can_Share_Sink_Pipeline()
        {
            // Arrange
            var factory = new PipelineFactory();
            var hub = new PipelineHub(factory);

            // Create services that will share pipelines
            var logService1 = new LogProducerService(hub);
            var logService2 = new LogProducerService(hub);
            var metricService = new MetricService(hub);

            // Act - use the services in parallel
            var logTasks = Task.WhenAll(
                logService1.LogAsync("Service 1: Starting"),
                logService2.LogAsync("Service 2: Ready"),
                logService1.LogAsync("Service 1: Processing"),
                logService2.LogAsync("Service 2: Processing")
            );

            var metricTasks = Task.WhenAll(
                metricService.TrackMetricAsync("CPU", 45.6),
                metricService.TrackMetricAsync("Memory", 1243.8),
                metricService.TrackMetricAsync("DiskIO", 32.5)
            );

            await Task.WhenAll(logTasks, metricTasks);

            // We're not making assertions here since the output is just written to console
            // The test passes if no exceptions are thrown and all messages are processed

            // Complete all pipelines gracefully
            await hub.DisposeAsync();

            // Assert: Check that the log and metric messages were processed
            // In a real-world scenario, you would verify the output in a more robust way
            // For example, you could use a mock or a spy to capture the output
            // and assert on it. Here we just check that the tasks completed successfully.
            Assert.True(logTasks.IsCompletedSuccessfully);
            Assert.True(metricTasks.IsCompletedSuccessfully);
            // Verify we get the same pipeline instance when requesting it again
        }
    }
}
