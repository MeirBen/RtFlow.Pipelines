# RtFlow.Pipelines.Core

[![NuGet](https://img.shields.io/nuget/v/RtFlow.Pipelines.Core.svg)](https://www.nuget.org/packages/RtFlow.Pipelines.Core)

**RtFlow.Pipelines.Core** - A powerful, fluent API for building high-throughput, resilient data processing pipelines using .NET's TPL Dataflow library.

## 📦 Installation

```bash
dotnet add package RtFlow.Pipelines.Core
```

## 📚 Documentation

For complete documentation, examples, API reference, and best practices, please see the [main project README](../README.md).

## 🚀 Quick Start

```csharp
using RtFlow.Pipelines.Core;

// Create a simple processing pipeline
var pipeline = FluentPipeline
    .Create<int>()
    .Transform(x => x * 2)
    .ToSink(x => Console.WriteLine($"Result: {x}"));

// Process data
await pipeline.SendAsync(5);  // Output: Result: 10
await pipeline.SendAsync(10); // Output: Result: 20
pipeline.Complete();
await pipeline.Completion;
```

```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/MeirBen/RtFlow.Pipelines/blob/main/LICENSE) file for details.

---

For the complete documentation and examples, visit the [main project repository](https://github.com/MeirBen/RtFlow.Pipelines).
