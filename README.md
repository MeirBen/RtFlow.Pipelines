# RtFlow.Pipelines

<!-- [![Build Status](https://img.shields.io/github/actions/workflow/status/MeirBen/RtFlow.Pipelines/ci.yml?branch=main)](https://github.com/MeirBen/RtFlow.Pipelines/actions)  
[![NuGet](https://img.shields.io/nuget/v/RtFlow.Pipelines.Core.svg)](https://www.nuget.org/packages/RtFlow.Pipelines.Core) -->

RtFlow.Pipelines is a powerful, fluent API for building high-throughput, resilient data processing pipelines using .NET's TPL Dataflow library. It simplifies the creation of complex data processing workflows with a clean, chainable syntax.

## Key Features

* **Fluent API** - Build complex data pipelines with an intuitive, chainable syntax
* **Type-safe** - Strongly-typed pipeline stages with compile-time checking
* **Back-pressure & Bounded Capacity** - Control flow rate with TPL Dataflow's built-in mechanisms
* **Cancellation Support** - Graceful pipeline shutdown with CancellationToken
* **Batching** - Group elements into batches for efficient processing
* **Hosting Integration** - Lifecycle management via HostedService in ASP.NET Core
* **Side-effect Support** - Add monitoring, logging, or metrics collection without changing data flow

## Installation

```bash
dotnet add package RtFlow.Pipelines.Core
dotnet add package RtFlow.Pipelines.Hosting
```

## Quick Start

```csharp
using RtFlow.Pipelines.Core;
using System.Threading.Tasks.Dataflow;

// Create a simple pipeline that doubles integers
var pipeline = FluentPipeline.Create<int>(opts => { 
        opts.BoundedCapacity = 100; 
    })
    .Transform(x => x * 2)
    .ToPipeline();

// Send data through the pipeline
await pipeline.SendAsync(5);
pipeline.Complete();

// Receive the processed result
int result = await DataflowBlock.ReceiveAsync<int>(pipeline); // 10
```

## Core Components

### FluentPipeline

The main entry point for creating pipelines. Provides static methods to start building a pipeline.

```csharp
// Create a pipeline with a buffer block
var pipeline = FluentPipeline.Create<Order>()
    .Transform(order => EnrichOrder(order))
    .Batch(10)
    .Transform(batch => ProcessBatch(batch))
    .ToPipeline();

// Start with a custom block
var customBlock = new TransformBlock<string, int>(s => int.Parse(s));
var pipeline = FluentPipeline.BeginWith(customBlock)
    .Transform(i => i * 2)
    .ToPipeline();
```

### Transformation Operations

```csharp
// Synchronous transformation
.Transform(item => ProcessItem(item))

// Asynchronous transformation
.TransformAsync(async item => await ProcessItemAsync(item))

// Asynchronous with cancellation
.TransformAsync(async (item, ct) => await ProcessItemAsync(item, ct))

// Add side-effects without changing data
.Tap(item => Console.WriteLine($"Processing: {item}"))
.TapAsync(async item => await LogItemAsync(item))

// Batch processing
.Batch(100)
```

### Terminal Operations

```csharp
// Convert to TPL Dataflow IPropagatorBlock
.ToPipeline()

// Terminate with an action (sink)
.ToSink(item => Console.WriteLine(item))
.ToSinkAsync(async item => await SaveToDbAsync(item))
```

## Hosting Integration

Integrate with ASP.NET Core hosting for lifecycle management:

```csharp
// In Program.cs or Startup.cs
services.AddSingleton<IPipelineDefinition>(sp => 
    new PipelineDefinition<Order, ProcessedOrder>(
        "OrderProcessing",
        ct => FluentPipeline.Create<Order>(opts => { 
                opts.CancellationToken = ct; 
            })
            .TransformAsync(async order => await ProcessOrderAsync(order))
            .ToPipeline()
    ));

services.AddHostedService<PipelineHostedService>();
```

## Advanced Configuration

Configure execution options for fine-grained control:

```csharp
FluentPipeline.Create<Order>()
    .Transform(
        order => EnrichOrder(order),
        opts => {
            opts.MaxDegreeOfParallelism = 4;
            opts.BoundedCapacity = 100;
            opts.EnsureOrdered = true;
        })
    .ToPipeline();
```

## Cancellation Support

RtFlow.Pipelines provides robust cancellation support, allowing you to gracefully stop processing:

```csharp
// Create a cancellation token source
var cts = new CancellationTokenSource();

// Create a pipeline with cancellation support
var pipeline = FluentPipeline
    .Create<int>(cancellationToken: cts.Token)
    .TransformAsync(async (x, token) => {
        await Task.Delay(100, token); // This will respect cancellation
        return x * 2;
    })
    .ToSink(x => Console.WriteLine(x));

// Send data
await pipeline.SendAsync(42);

// Later, when you need to stop processing
cts.Cancel();
```

## Pipeline Factory

The `PipelineFactory` provides integration with ASP.NET Core's hosting lifecycle:

```csharp
// Register in DI container
services.AddSingleton<IPipelineFactory, PipelineFactory>();

// Inject and use in your services
public class MyService 
{
    private readonly IPipelineFactory _factory;
    
    public MyService(IPipelineFactory factory) 
    {
        _factory = factory;
    }
    
    public IPropagatorBlock<int, string> CreatePipeline() 
    {
        // Creates a pipeline that automatically observes the application's shutdown signal
        return _factory.Create<int>()
            .Transform(x => x.ToString())
            .ToPipeline();
    }
}
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Testing

The library includes comprehensive tests for core functionality:

```bash
dotnet test RtFlow.Pipelines.Tests
```

## Contributing

1. Fork & create a branch.  
2. Commit your feature/fix **with tests**.  
3. Open a PR.  

Follow the `.editorconfig`, keep the CI green, and add meaningful documentation.

## License

RtFlow.Pipelines is released under the **MIT License** – feel free to use, modify, and distribute!
