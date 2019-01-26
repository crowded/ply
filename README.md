# Ply
Ply is a high performance TPL library for F#. 

The goal of Ply is to be a near zero overhead abstraction like it is in C#. On master we are at 2 allocations per bind, which are the focus of upcoming work. Then there are the few constant factor allocations which are inevitable and equivalent to C# semantics for async methods.

Compared to C#'s async-await Ply requires — for a regular method of a handful of awaitables — 20 to 30% more Gen0 GCs. This all against a comparable execution time of async-await. Only slowly moving towards TaskBuilder.fs execution time once functions grow larger.

|                  Method |     Mean |     Error |    StdDev | Ratio | RatioSD | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
|------------------------ |---------:|----------:|----------:|------:|--------:|------------:|------------:|------------:|--------------------:|
|                     Ply | 24.60 us | 1.1610 us | 1.3371 us |  1.00 |    0.07 |      0.3052 |           - |           - |
| &#39;TaskBuilder.fs v2.1.0&#39; | 26.86 us | 0.6751 us | 0.6932 us |  1.09 |    0.03 |      0.5798 |           - |           - |
|        &#39;C# Async Await&#39; | 24.59 us | 0.8028 us | 0.8923 us |  1.00 |    0.00 |      0.2136 |           - |           - |

*Allocated Memory/Op is removed as it isn't correct on .NET Core.*

Ply comes with 4 builders: 
- `task (Task<'T>)` the only builder inside the netstandard build of Ply.
- `vtask (ValueTask<'T>)`
- `uvtask (ValueTask<'T>)`    
**u**nsafe**v**alue**task** is the only allocation free TPL CE Ply comes with. This does come with a trade-off as it skips construction of an execution bubble. An execution bubble is made by any C# async-await method for capturing and restoring async local and synchronization context changes. These potential changes would otherwise escape onto the caller context. If you know however that you and any other synchronous methods you call don't make any changes there's nothing inherently unsafe about uvtask.

- `ply (Ply<'T>)` Can be enqueued directly onto the binding callsite's state machine, skips `Task` and safety wrapping

More docs are coming.

## Special Thanks
Thanks to @gusty for very valuable SRTP advice, it helped me tremendously to narrow down what specifically was wrong about an earlier approach I took.

Thanks to @rspeele TaskBuilder.fs was a great inspiration in developing Ply.

## Next Steps and Improvements
- Complete the last few CE constructs
    - For
    - TryWith
    - TryFinally
    - Using
- Finish up the experimental branch.
- Pester @cartermp about struct computation expressions until F# has them ;) This is the last hurdle we need to overcome for Ply to be zero alloc per bind.