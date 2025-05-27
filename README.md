# RtFlow.Pipelines

<!-- [![Build Status](https://img.shields.io/github/actions/workflow/status/MeirBen/RtFlow.Pipelines/ci.yml?branch=main)](https://github.com/MeirBen/RtFlow.Pipelines/actions)  
[![NuGet](https://img.shields.io/nuget/v/RtFlow.Pipelines.Core.svg)](https://www.nuget.org/packages/RtFlow.Pipelines.Core) -->

**RtFlow.Pipelines** is a powerful, fluent API for building high-throughput, resilient data processing pipelines using .NET's TPL Dataflow library. It simplifies the creation of complex data processing workflows with an intuitive, chainable syntax while providing enterprise-grade features like graceful shutdown, cancellation support, and lifecycle management.

## 📋 Table of Contents

- [✨ Key Features](#-key-features)
- [📦 Installation](#-installation)
- [🚀 Quick Start](#-quick-start)
- [🏗️ Core Components](#️-core-components)
- [🔧 Advanced Configuration](#-advanced-configuration)
- [🚫 Cancellation Support](#-cancellation-support)
- [🏠 ASP.NET Core Integration](#-aspnet-core-integration)
- [💡 Common Use Cases](#-common-use-cases)
- [❓ Troubleshooting](#-troubleshooting)
- [🧪 Testing](#-testing)
- [📚 Examples & Best Practices](#-examples--best-practices)
- [🤝 Contributing](#-contributing)
- [📄 License](#-license)

## ✨ Key Features

- **🔗 Fluent API** - Build complex data pipelines with an intuitive, chainable syntax
- **🛡️ Type-safe** - Strongly-typed pipeline stages with compile-time checking  
- **⚡ High Performance** - Built on TPL Dataflow with back-pressure and bounded capacity control
- **🚫 Cancellation Support** - Graceful pipeline shutdown with comprehensive CancellationToken integration
- **📦 Batching** - Group elements into batches for efficient bulk processing
- **🏗️ Pipeline Hub** - Share and manage named pipelines across services
- **🔍 Side-effects** - Add monitoring, logging, or metrics collection without changing data flow
- **🏠 ASP.NET Core Integration** - Lifecycle management via HostedService
- **⚙️ Advanced Configuration** - Fine-grained control over execution options and parallelism

## 📦 Installation

Install the packages you need for your scenario:

```bash
# Core functionality (required)
dotnet add package RtFlow.Pipelines.Core

# Optional: Extensions for advanced scenarios
dotnet add package RtFlow.Pipelines.Extensions

# Optional: ASP.NET Core hosting integration
dotnet add package RtFlow.Pipelines.Hosting
```

## 🚀 Quick Start

### Basic Pipeline

Create a simple pipeline that processes integers:

```csharp
using RtFlow.Pipelines.Core;
using System.Threading.Tasks.Dataflow;

// Create a pipeline that doubles integers
var pipeline = FluentPipeline
    .Create<int>(opts => opts.BoundedCapacity = 100)
    .Transform(x => x * 2)
    .ToPipeline();

// Send data through the pipeline
await pipeline.SendAsync(5);
await pipeline.SendAsync(10);
pipeline.Complete();

// Receive processed results
while (await DataflowBlock.OutputAvailableAsync(pipeline))
{
    int result = await DataflowBlock.ReceiveAsync(pipeline);
    Console.WriteLine(result); // Outputs: 10, 20
}

await pipeline.Completion;
```

### Pipeline with Sink

Create a pipeline that processes and consumes data:

```csharp
var results = new List<string>();

var sinkPipeline = FluentPipeline
    .Create<int>()
    .Transform(x => $"Processed: {x * 2}")
    .ToSink(result => results.Add(result));

// Send data
await sinkPipeline.SendAsync(1);
await sinkPipeline.SendAsync(2);
sinkPipeline.Complete();
await sinkPipeline.Completion;

// Results: ["Processed: 2", "Processed: 4"]
```

## 🏗️ Core Components

### FluentPipeline

The main entry point for creating pipelines with static factory methods:

```csharp
// Start with a buffer block (most common)
var pipeline = FluentPipeline
    .Create<Order>()
    .Transform(order => EnrichOrder(order))
    .Batch(10)
    .Transform(batch => ProcessBatch(batch))
    .ToPipeline();

// Start with a custom block for advanced scenarios
var customBlock = new TransformBlock<string, int>(s => int.Parse(s));
var pipeline = FluentPipeline
    .BeginWith(customBlock)
    .Transform(i => i * 2)
    .ToPipeline();
```

### Pipeline Operations

#### Transformations

```csharp
// Synchronous transformation
.Transform(item => ProcessItem(item))

// Asynchronous transformation  
.TransformAsync(async item => await ProcessItemAsync(item))

// Asynchronous with cancellation support
.TransformAsync(async (item, ct) => await ProcessItemAsync(item, ct))
```

#### Side Effects (Tap Operations)

Add monitoring, logging, or metrics without changing the data flow:

```csharp
// Synchronous side effects
.Tap(item => Console.WriteLine($"Processing: {item}"))
.Tap(item => _metrics.Increment("items_processed"))

// Asynchronous side effects
.TapAsync(async item => await LogItemAsync(item))
.TapAsync(async (item, ct) => await SendToMonitoringAsync(item, ct))
```

#### Batching

Group items for efficient bulk processing:

```csharp
// Batch items into groups of 100
.Batch(100)

// Batch with configuration
.Batch(50, opts => opts.BoundedCapacity = 200)
```

#### Terminal Operations

```csharp
// Convert to TPL Dataflow IPropagatorBlock for further composition
.ToPipeline()

// Terminate with a synchronous action (sink)
.ToSink(item => Console.WriteLine(item))
.ToSink(item => _database.Save(item))

// Terminate with asynchronous action
.ToSinkAsync(async item => await SaveToDbAsync(item))
.ToSinkAsync(async (item, ct) => await ProcessWithCancellationAsync(item, ct))
```

### PipelineHub - Named Pipeline Management

The `PipelineHub` allows you to create, share, and manage named pipelines across your application:

```csharp
// Create a hub with a factory
var factory = new PipelineFactory();
var hub = new PipelineHub(factory);

// Create or get a named pipeline
var stringToIntPipeline = hub.GetOrCreatePipeline(
    "StringToIntConverter",
    factory => factory
        .Create<string>()
        .Transform(s => int.Parse(s))
        .ToPipeline());

// Create a named sink pipeline  
var loggingPipeline = hub.GetOrCreateSinkPipeline(
    "ApplicationLogger", 
    factory => factory
        .Create<string>()
        .Transform(msg => $"[{DateTime.Now}] {msg}")
        .ToSink(Console.WriteLine));

// Use the pipelines from anywhere in your application
await stringToIntPipeline.SendAsync("123");
await loggingPipeline.SendAsync("Application started");

// Hub provides convenient extension methods
var metricsPipeline = hub.CreateSinkPipeline(
    "Metrics",
    metric => Console.WriteLine($"METRIC: {metric}"));
```

### PipelineFactory - Lifecycle Management

The `PipelineFactory` provides integrated cancellation token management:

```csharp
// Register in DI container
services.AddSingleton<IPipelineFactory, PipelineFactory>();

// Inject and use in your services
public class OrderService 
{
    private readonly IPipelineFactory _factory;
    
    public OrderService(IPipelineFactory factory) 
    {
        _factory = factory;
    }
    
    public IPropagatorBlock<Order, ProcessedOrder> CreateOrderPipeline() 
    {
        // Factory automatically provides cancellation token linked to application lifecycle
        return _factory
            .Create<Order>()
            .TransformAsync(async (order, ct) => await EnrichOrderAsync(order, ct))
            .Transform(order => ValidateOrder(order))
            .ToPipeline();
    }
}
```

## 🔧 Advanced Configuration

### Execution Options

Fine-tune pipeline performance and behavior:

```csharp
var pipeline = FluentPipeline
    .Create<Order>(opts => {
        opts.BoundedCapacity = 1000;           // Control back-pressure
        opts.MaxDegreeOfParallelism = 4;       // Parallel processing
        opts.EnsureOrdered = true;             // Maintain order
        opts.CancellationToken = cancellationToken;
    })
    .Transform(
        order => ProcessOrder(order),
        opts => {
            opts.MaxDegreeOfParallelism = 8;   // Transform-specific settings
            opts.BoundedCapacity = 500;
        })
    .Batch(
        batchSize: 50,
        opts => opts.BoundedCapacity = 100)    // Batch-specific settings  
    .ToPipeline();
```

### Complex Processing Pipeline

Real-world example with multiple stages:

```csharp
var etlPipeline = FluentPipeline
    .Create<RawData>(opts => opts.BoundedCapacity = 1000)
    
    // Extract & validate
    .Transform(raw => ParseRawData(raw))
    .Tap(data => _metrics.Increment("records_parsed"))
    
    // Async enrichment with cancellation
    .TransformAsync(async (data, ct) => {
        data.EnrichedInfo = await _enrichmentService.EnrichAsync(data.Id, ct);
        return data;
    })
    
    // Batch for efficient processing
    .Batch(100)
    .Tap(batch => _logger.LogInformation($"Processing batch of {batch.Length} items"))
    
    // Bulk database operations
    .TransformAsync(async (batch, ct) => {
        await _database.BulkInsertAsync(batch, ct);
        return new BatchResult { ProcessedCount = batch.Length };
    })
    
    // Final sink
    .ToSinkAsync(async result => {
        _metrics.Gauge("batch_size", result.ProcessedCount);
        await _notificationService.NotifyBatchCompleted(result);
    });
```

## 🚫 Cancellation Support

RtFlow.Pipelines provides comprehensive cancellation support for graceful shutdown:

### Basic Cancellation

```csharp
var cts = new CancellationTokenSource();

var pipeline = FluentPipeline
    .Create<int>(opts => opts.CancellationToken = cts.Token)
    .TransformAsync(async (x, token) => {
        // This operation will respect cancellation
        await Task.Delay(100, token); 
        return x * 2;
    })
    .ToSink(x => Console.WriteLine(x));

// Send some data
await pipeline.SendAsync(42);

// Later, cancel processing
cts.Cancel();
```

### Factory-Level Cancellation

The `PipelineFactory` automatically manages cancellation tokens:

```csharp
// Factory provides application-wide cancellation
var factory = new PipelineFactory();

// All pipelines from this factory share cancellation context
var pipeline1 = factory.Create<string>()
    .TransformAsync(async (s, ct) => await ProcessAsync(s, ct))
    .ToPipeline();

var pipeline2 = factory.Create<int>()
    .TransformAsync(async (i, ct) => await CalculateAsync(i, ct))
    .ToPipeline();

// Disposing factory cancels all associated pipelines
factory.Dispose(); // Graceful shutdown of all pipelines
```

### Hub-Level Cancellation

The `PipelineHub` provides coordinated shutdown:

```csharp
await using var hub = new PipelineHub(new PipelineFactory());

// Create multiple pipelines
var pipeline1 = hub.GetOrCreatePipeline("ProcessA", factory => /* ... */);
var pipeline2 = hub.GetOrCreatePipeline("ProcessB", factory => /* ... */);

// Disposing hub gracefully shuts down all managed pipelines
await hub.DisposeAsync(); // All pipelines receive cancellation and complete gracefully
```

## 🏠 ASP.NET Core Integration

### Basic Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register pipeline infrastructure
builder.Services.AddSingleton<IPipelineFactory, PipelineFactory>();
builder.Services.AddSingleton<IPipelineHub, PipelineHub>();

// Register your services that use pipelines
builder.Services.AddScoped<OrderProcessingService>();
builder.Services.AddScoped<LoggingService>();

var app = builder.Build();
```

### Service Integration

```csharp
public class OrderProcessingService
{
    private readonly IPipelineHub _hub;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(IPipelineHub hub, ILogger<OrderProcessingService> logger)
    {
        _hub = hub;
        _logger = logger;
        
        // Initialize processing pipeline
        InitializeOrderPipeline();
    }

    private void InitializeOrderPipeline()
    {
        _hub.GetOrCreateSinkPipeline(
            "OrderProcessing",
            factory => factory
                .Create<Order>()
                .TransformAsync(async (order, ct) => await ValidateOrderAsync(order, ct))
                .Tap(order => _logger.LogInformation("Processing order {OrderId}", order.Id))
                .TransformAsync(async (order, ct) => await ProcessPaymentAsync(order, ct))
                .Batch(10) // Process payments in batches
                .ToSinkAsync(async (batch, ct) => await SaveOrdersAsync(batch, ct))
        );
    }

    public async Task ProcessOrderAsync(Order order)
    {
        var pipeline = _hub.GetSinkPipeline<Order>("OrderProcessing");
        await pipeline.SendAsync(order);
    }
}
```

### Hosted Service Integration

```csharp
public class DataProcessingHostedService : BackgroundService
{
    private readonly IPipelineHub _hub;
    private readonly IServiceProvider _serviceProvider;

    public DataProcessingHostedService(IPipelineHub hub, IServiceProvider serviceProvider)
    {
        _hub = hub;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create data processing pipeline
        var processingPipeline = _hub.GetOrCreateSinkPipeline(
            "DataProcessor",
            factory => factory
                .Create<DataEvent>(opts => opts.CancellationToken = stoppingToken)
                .TransformAsync(async (evt, ct) => await ProcessEventAsync(evt, ct))
                .ToSinkAsync(async (result, ct) => await PublishResultAsync(result, ct))
        );

        // Process incoming data until cancellation
        try
        {
            await foreach (var dataEvent in GetDataStreamAsync(stoppingToken))
            {
                await processingPipeline.SendAsync(dataEvent);
            }
        }
        finally
        {
            // Pipeline will be gracefully shut down when hub is disposed
            // by the hosting infrastructure
        }
    }
}
```

## 🧪 Testing

The library includes comprehensive tests covering all core functionality, cancellation scenarios, and edge cases:

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test RtFlow.Pipelines.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

The test suite includes:
- **Unit tests** for all pipeline operations and transformations
- **Integration tests** for complex pipeline scenarios  
- **Cancellation tests** for graceful shutdown behavior
- **Performance tests** for throughput and memory usage
- **Edge case tests** for error handling and boundary conditions

## 📚 Examples & Best Practices

### Error Handling

```csharp
var resilientPipeline = FluentPipeline
    .Create<string>()
    .Transform(input => {
        try 
        {
            return ProcessRiskyOperation(input);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for input: {Input}", input);
            return new ProcessedResult { HasError = true, Input = input };
        }
    })
    .TapAsync(async result => {
        if (result.HasError)
            await _deadLetterQueue.SendAsync(result.Input);
    })
    .ToSink(result => {
        if (!result.HasError)
            _successHandler.Handle(result);
    });
```

### Resource Management

```csharp
// Always dispose hubs and factories for proper cleanup
await using var hub = new PipelineHub(new PipelineFactory());

// Or use using statements for sync disposal
using var factory = new PipelineFactory();
using var hub = new PipelineHub(factory);
```

### Performance Tips

- **Use bounded capacity** to prevent memory issues with fast producers
- **Batch operations** for I/O bound work (database, network)
- **Configure parallelism** based on your workload characteristics
- **Monitor back-pressure** with appropriate buffer sizes
- **Use async methods** for I/O operations to avoid thread pool starvation

## 💡 Common Use Cases

### Data ETL Pipelines
```csharp
var etlPipeline = FluentPipeline
    .Create<RawData>()
    .Transform(data => ExtractData(data))           // Extract
    .TransformAsync(async data => await EnrichData(data))  // Transform
    .Batch(100)                                     // Batch for efficiency
    .ToSinkAsync(async batch => await LoadToDatabase(batch)); // Load
```

### Message Processing
```csharp
var messageProcessor = hub.GetOrCreateSinkPipeline(
    "MessageProcessor",
    factory => factory
        .Create<Message>()
        .Transform(msg => DeserializeMessage(msg))
        .TapAsync(async msg => await LogProcessing(msg))
        .TransformAsync(async msg => await ProcessBusinessLogic(msg))
        .ToSinkAsync(async result => await PublishResult(result))
);
```

### File Processing
```csharp
var fileProcessor = FluentPipeline
    .Create<FileInfo>()
    .TransformAsync(async file => await ReadFileAsync(file))
    .Transform(content => ParseContent(content))
    .Batch(50)  // Process in batches
    .TransformAsync(async batch => await ValidateBatch(batch))
    .ToSinkAsync(async batch => await SaveToStorage(batch));
```

### Real-time Analytics
```csharp
var analyticsStream = FluentPipeline
    .Create<Event>()
    .Tap(evt => _metrics.Record(evt))              // Record raw event
    .Transform(evt => AggregateEvent(evt))         // Calculate metrics
    .Batch(100, TimeSpan.FromSeconds(5))           // Time-based batching
    .TransformAsync(async batch => await CalculateInsights(batch))
    .ToSinkAsync(async insights => await UpdateDashboard(insights));
```

## ❓ Troubleshooting

### Common Issues

**Pipeline hangs or doesn't complete**
```csharp
// ✅ Always call Complete() to signal end of input
pipeline.Complete();
await pipeline.Completion;

// ✅ Check for proper cancellation token usage
var cts = new CancellationTokenSource();
var pipeline = FluentPipeline
    .Create<int>(opts => opts.CancellationToken = cts.Token)
    // ... pipeline stages
    .ToPipeline();
```

**Out of memory with large datasets**
```csharp
// ✅ Use bounded capacity to control memory usage
var pipeline = FluentPipeline
    .Create<LargeObject>(opts => opts.BoundedCapacity = 100)  // Limit buffer size
    .Transform(obj => ProcessObject(obj))
    .ToPipeline();
```

**Poor performance**
```csharp
// ✅ Increase parallelism for CPU-bound work
.Transform(
    data => CpuIntensiveOperation(data),
    opts => opts.MaxDegreeOfParallelism = Environment.ProcessorCount)

// ✅ Use async methods for I/O-bound work
.TransformAsync(async data => await DatabaseOperation(data))

// ✅ Batch operations for efficiency
.Batch(100)  // Process 100 items at once
```

**Unhandled exceptions**
```csharp
// ✅ Implement proper error handling
.Transform(data => {
    try 
    {
        return ProcessData(data);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Processing failed");
        return CreateErrorResult(data, ex);
    }
})
```

### Debugging Tips

1. **Add Tap operations** to inspect data flow:
   ```csharp
   .Tap(data => Console.WriteLine($"Processing: {data}"))
   ```

2. **Monitor pipeline completion**:
   ```csharp
   pipeline.Completion.ContinueWith(task => {
       if (task.IsFaulted)
           Console.WriteLine($"Pipeline failed: {task.Exception}");
       else
           Console.WriteLine("Pipeline completed successfully");
   });
   ```

3. **Use logging for visibility**:
   ```csharp
   .TapAsync(async data => await _logger.LogInformationAsync("Processed {Data}", data))
   ```

We welcome contributions! Here's how to get started:

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/amazing-feature`
3. **Implement** your feature with appropriate tests
4. **Follow** the existing code style and conventions
5. **Ensure** all tests pass: `dotnet test`
6. **Commit** your changes: `git commit -m 'Add amazing feature'`
7. **Push** to your branch: `git push origin feature/amazing-feature`
8. **Open** a Pull Request

### Code Guidelines

- Follow the `.editorconfig` settings
- Add XML documentation for public APIs
- Include unit tests for new functionality
- Update the README if adding new features
- Keep commit messages clear and descriptive

### Reporting Issues

If you find a bug or have a feature request:

1. **Search** existing issues first
2. **Create** a new issue with detailed description
3. **Include** code samples that reproduce the problem
4. **Specify** your .NET version and environment

## 📄 License

RtFlow.Pipelines is released under the **MIT License**. See the [LICENSE](LICENSE) file for details.

---

**Built with ❤️ for the .NET community**
