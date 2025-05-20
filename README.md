# RtFlow.Pipelines

<!-- [![Build Status](https://img.shields.io/github/actions/workflow/status/MeirBen/RtFlow.Pipelines/ci.yml?branch=main)](https://github.com/MeirBen/RtFlow.Pipelines/actions)  
[![NuGet](https://img.shields.io/nuget/v/RtFlow.Pipelines.Core.svg)](https://www.nuget.org/packages/RtFlow.Pipelines.Core) -->

RtFlow.Pipelines is a powerful, fluent API for building high-throughput, resilient data processing pipelines using .NET's TPL Dataflow library. It simplifies the creation of complex data processing workflows with a clean, chainable syntax.

## Key Features

* **Fluent API** - Build complex data pipelines with an intuitive, chainable syntax
* **Type-safe** - Strongly-typed pipeline stages with compile-time checking
* **Back-pressure & Bounded Capacity** - Control flow rate with TPL Dataflow's built-in mechanisms
* **Cancellation Support** - Graceful pipeline shutdown with CancellationToken
* **Resilience Policies** - Retry mechanisms via Polly integration
* **Batching** - Group elements into batches for efficient processing
* **Hosting Integration** - Lifecycle management via HostedService in ASP.NET Core
* **Side-effect Support** - Add monitoring, logging, or metrics collection without changing data flow

## Installation

```bash
dotnet add package RtFlow.Pipelines.Core
dotnet add package RtFlow.Pipelines.Extensions
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

## Resilience with Polly

```csharp
using RtFlow.Pipelines.Extensions;

// Add retry policy to a function
Func<Order, Task<ProcessedOrder>> processOrder = /* ... */;
var resilientProcessor = processOrder.WithRetry(retries: 3);

// Use in pipeline
FluentPipeline.Create<Order>()
    .TransformAsync(order => resilientProcessor(order))
    .ToPipeline();
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

---

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Testing

A built-in smoke test pushes **1 000 000** items through a sample pipeline and checks accuracy:

```bash
dotnet test RtFlow.Pipelines.Tests
```

---

## Contributing

1. Fork & create a branch.  
2. Commit your feature/fix **with tests**.  
3. Open a PR.  

Follow the `.editorconfig`, keep the CI green, and add meaningful documentation.

---

## License

RtFlow.Pipelines is released under the **MIT License** – feel free to use, modify, and distribute!
