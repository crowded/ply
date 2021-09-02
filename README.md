# Ply

[![NuGet Version](https://img.shields.io/nuget/v/Ply.svg)](https://www.nuget.org/packages/Ply)

## Ply is a high performance TPL library for F#.    
The goal of Ply is to be a very low overhead Task abstraction like it is in C#. 

### Benchmark
[see benchmark code](https://github.com/crowded/ply/blob/master/Benchmarks/Benchmarks.fs#L33)

|                  Method |     Mean |     Error |    StdDev | Ratio | RatioSD | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | 
|------------------------ |---------:|----------:|----------:|------:|--------:|------------:|------------:|------------:|
|          C# Async Await | 24.59 us | 0.8028 us | 0.8923 us |  1.00 |    0.00 |      0.2136 |           - |           - |
|                     Ply | 24.60 us | 1.1610 us | 1.3371 us |  1.00 |    0.07 |      0.3052 |           - |           - |
|   TaskBuilder.fs v2.1.0 | 26.86 us | 0.6751 us | 0.6932 us |  1.09 |    0.03 |      0.5798 |           - |           - |

*Allocated Memory/Op is removed as it isn't correct on .NET Core, Gen 0/1k Op is the relevant metric.*

### Builders
Ply comes bundled with these builders: 

| builder          | return type   | tfm                           | namespace                            |
|---------------|---------------|-------------------------------|--------------------------------------|
| `task`        | Task<'T>      | netstandard2.0, netcoreapp2.1 | FSharp.Control.Tasks.Builders        |
| `vtask`       | ValueTask<'T> | netcoreapp2.1                 | FSharp.Control.Tasks.Builders        |
| `unitTask`    | Task          | netstandard2.0, netcoreapp2.1 | FSharp.Control.Tasks.Builders        |
| `unitVtask`   | ValueTask     | netcoreapp2.1                 | FSharp.Control.Tasks.Builders        |
| `uvtask`      | ValueTask<'T> | netcoreapp2.1                 | FSharp.Control.Tasks.Builders.Unsafe |
| `uunitTask`   | Task          | netcoreapp2.1                 | FSharp.Control.Tasks.Builders.Unsafe |
| `uunintVtask` | ValueTask     | netcoreapp2.1                 | FSharp.Control.Tasks.Builders.Unsafe |
| `uply`        | Ply<'T>       | netstandard2.0,netcoreapp2.1  | FSharp.Control.Tasks.Builders.Unsafe |


More information on when to use which builder:

| builder         | description                                                                                                                                                                                                                  |
|--------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `vtask`      | Near zero allocation CE, allocates one object for the [execution bubble](#execution-bubble) at the start, and two objects per bind if the Task we bind to isn't completed yet.                                                                         |
| `unitTask`   |  As the TPL doesn't know about F#'s unit type `Task.FromResult(())` won't ever return a cached task.   On `netcoreapp` we can check for a succesful completion instead to directly return a CompletedTask removing the task allocation. |
| `unitVtask`  | CE shorthand for doing `if vtask.IsCompletedSuccessfully then ValueTask() else ValueTask(vtask.AsTask() :> Task)`                                                                                                            |
| `uvtask`     | An unsafe version of `vtask` and one of the few zero allocation* CEs Ply comes with. Read about the trade-off under [execution bubble](#execution-bubble)                                                                                           |
| `uunitTask`  | An unsafe version of `unitTask` and one of the few zero allocation* CEs Ply comes with. Read about the trade-off under [execution bubble](#execution-bubble)                                                                                        |
| `uunitVtask` | An unsafe version of `unitVtask` and one of the few zero allocation* CEs Ply comes with. Read about the trade-off under [execution bubble](#execution-bubble)                                                                                       |
| `uply`       | Can be enqueued directly onto the caller's state machine, skips `Task` and [execution bubble](#execution-bubble).                                                                                                                                  |

**zero allocation only when any Task (or Task-like) you bind against is already completed.*

### Execution bubble
An execution bubble is made by any C# async-await method for capturing and restoring async local and synchronization context changes. Any changes would otherwise escape onto the caller context. 

It's rare that methods do anything with async locals or synchronization contexts, even in C#. 
So if you know anything you use doesn't do that either then there's nothing inherently unsafe about using the Unsafe CEs as you don't need any execution bubble for correctness.

## Special Thanks
Thanks to @gusty for very valuable SRTP advice, it helped me tremendously to narrow down what specifically was wrong about an earlier approach I took.

Thanks to @rspeele TaskBuilder.fs was a great inspiration in developing Ply.

## Next Steps and Improvements
- Finish up the experimental branch.
- On master we are at 2 allocations per bind, which are the focus of upcoming work. Then there are the few constant factor allocations which are inevitable and equivalent to C# semantics for async methods.
