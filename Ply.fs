// Optimized (Value)Task computation expressions for F#
// Author: Nino Floris - mail@ninofloris.com
// Copyright (c) 2019 Crowded B.V.
// Distributed under the MIT License (https://opensource.org/licenses/MIT).

namespace rec Ply
open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading.Tasks
open System.Diagnostics
open System.Runtime.ExceptionServices

module Internal =
    type [<AbstractClass;AllowNullLiteral>] Awaitable<'u>() =
        abstract member Await<'t when 't :> IAwaitingMachine> : machine: byref<'t> -> bool
        abstract member GetNext: unit -> Ply<'u>
    and IAwaitingMachine = 
        abstract member AwaitUnsafeOnCompleted<'awt when 'awt :> ICriticalNotifyCompletion> : awt: byref<'awt> -> unit

#if NETSTANDARD2_0 
type [<Struct>] Ply<'u> =
#else 
type [<IsReadOnly; Struct>] Ply<'u> =
#endif
    val internal value : 'u
    val internal awaitable : Internal.Awaitable<'u> 
    new(result: 'u) = { value = result; awaitable = null }
    new(await: Internal.Awaitable<'u>) = { value = Unchecked.defaultof<_>; awaitable = await }
    member this.IsCompletedSuccessfully = Object.ReferenceEquals(this.awaitable, null)
    member this.Result = if this.IsCompletedSuccessfully then this.value else this.awaitable.GetNext().Result

[<System.Obsolete>]
/// Entrypoint for generated code
module TplPrimitives =
    open Internal
    
    type IAwaiterMethods<'awt, 'res when 'awt :> ICriticalNotifyCompletion> = 
        abstract member IsCompleted: byref<'awt> -> bool
        abstract member GetResult: byref<'awt> -> 'res

    let inline createBuilder() = 
#if NETSTANDARD2_0
        AsyncTaskMethodBuilder<_>()
#else    
        AsyncValueTaskMethodBuilder<_>()
#endif   

    let inline defaultof<'T> = Unchecked.defaultof<'T>

    let inline isNull x = Object.ReferenceEquals(x, null)
    let inline isNotNull x = not (isNull x)
    
    let inline throwPreserve ex = 
        (ExceptionDispatchInfo.Capture ex).Throw()
        Unchecked.defaultof<_>

    let ret x = Ply(result = x)
    let zero = ret ()

    type [<Struct>]ContinuationStateMachine<'u> = 
#if NETSTANDARD2_0        
        val Builder : AsyncTaskMethodBuilder<'u> 
#else
        val Builder : AsyncValueTaskMethodBuilder<'u> 
#endif
        val mutable private next: Ply<'u>
        val mutable private inspect: bool
        val mutable private continuation: unit -> Ply<'u>

        new(continuation) = { 
            Builder = createBuilder()
            continuation = continuation
            next = defaultof<_>
            inspect = true
        }

        new(ply) = { 
            Builder = createBuilder()
            continuation = defaultof<_>
            next = ply
            inspect = true
        }            

        member private this.UnsafeExecuteToFirstYield() =
            this.next <- this.continuation()
            this.continuation <- defaultof<_>
                
        interface IAwaitingMachine with
            [<DebuggerStepThrough>]
            [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
            member this.AwaitUnsafeOnCompleted(awt: byref<'awt>) =
                this.Builder.AwaitUnsafeOnCompleted(&awt, &this)

        interface IAsyncStateMachine with
            // This method is effectively deprecated on .NET Core so only .NET Fx will still call this.
            member this.SetStateMachine(csm) = 
                this.Builder.SetStateMachine(csm)
            
            member this.MoveNext() =                    
                let mutable ex = defaultof<Exception>
                try
                    if isNotNull this.continuation then this.UnsafeExecuteToFirstYield()

                    let mutable fin = false
                    while not fin do
                        if this.inspect then
                            let next = this.next
                            if this.next.IsCompletedSuccessfully then
                                fin <- true
                                this.Builder.SetResult(this.next.value) 
                            else 
                                this.inspect <- false                         
                                let yielded = next.awaitable.Await(&this)
                                // MoveNext will be called again by the builder once await is done.
                                if yielded then
                                    fin <- true
                        else
                            this.inspect <- true
                            this.next <- this.next.awaitable.GetNext()
                with exn ->
                    ex <- exn 
                
                if isNotNull ex then 
                    this.Builder.SetException(ex)

    and [<Sealed>] TplAwaitable<'methods, 'awt, 't, 'u when 'methods :> IAwaiterMethods<'awt, 't> and 'awt :> ICriticalNotifyCompletion> =
        inherit Awaitable<'u>
        
        val private awaiterMethods: 'methods
        val mutable private awaiter: 'awt
        val private continuation: 't -> Ply<'u>
        
        new(awaiterMethods, awaiter, continuation) = {
            awaiterMethods = awaiterMethods
            awaiter = awaiter
            continuation = continuation
        }
        
        override this.Await(csm) =
            if this.awaiterMethods.IsCompleted &this.awaiter then
                false
            else
                csm.AwaitUnsafeOnCompleted(&this.awaiter) |> ignore
                true 

        override this.GetNext() = 
            Debug.Assert(this.awaiterMethods.IsCompleted &this.awaiter || (typeof<'awt> = typeof<YieldAwaitable.YieldAwaiter>), "Forcing an async here")
            this.continuation (this.awaiterMethods.GetResult &this.awaiter)

    and [<Sealed>] PlyAwaitable<'t, 'u> (awaitable: Awaitable<'t>, continuation: 't -> Ply<'u>) = 
        inherit Awaitable<'u>()
        let mutable awaitable = awaitable

        override __.Await(csm) = awaitable.Await(&csm)

        override this.GetNext() = 
            let next = awaitable.GetNext()
            if next.IsCompletedSuccessfully then
                continuation (next.value)
            else
                awaitable <- next.awaitable
                Ply(await = this)

    and [<Sealed>] LoopAwaitable(initialAwaitable : Awaitable<unit>, cond: unit -> bool, body : unit -> Ply<unit>) = 
        inherit Awaitable<unit>()
        let mutable awaitable : Awaitable<unit> = initialAwaitable

        member private this.RepeatBody() =
            if cond() then
                let next = body()
                if next.IsCompletedSuccessfully then 
                    this.RepeatBody()
                else
                    awaitable <- next.awaitable
                    Ply(await = this)
            else zero
            
        override __.Await(csm) = awaitable.Await(&csm)

        override this.GetNext() =
            let next = awaitable.GetNext()
            if next.IsCompletedSuccessfully then
                this.RepeatBody()
            else
                awaitable <- next.awaitable
                Ply(await = this)

#if !NETSTANDARD2_0
    let run (f: unit -> Ply<'u>) : ValueTask<'u> =
        // ContinuationStateMachine contains a mutable struct so we need to prevent struct copies.
        let mutable x = ContinuationStateMachine<_>(f)
        x.Builder.Start(&x)
        x.Builder.Task

    let runPly (ply: Ply<'u>) : ValueTask<'u>  =
        let mutable x = ContinuationStateMachine<_>(ply)
        x.Builder.Start(&x)
        x.Builder.Task

    // This won't correctly prevent AsyncLocal leakage or SyncContext switches but it does save us the closure alloc
    // Making only this version completely alloc free for the fast path...
    // Read more here https://github.com/dotnet/coreclr/blob/027a9105/src/System.Private.CoreLib/src/System/Runtime/CompilerServices/AsyncMethodBuilder.cs#L954
    let inline runUnwrapped (f: unit -> Ply<'u>) : ValueTask<'u>  =
        let next = f()
        if next.IsCompletedSuccessfully then 
            let mutable b = createBuilder()
            b.SetResult(next.Result)
            b.Task
        else 
            runPly next
#endif

    let runAsTask (f: unit -> Ply<'u>) : Task<'u> =
        // ContinuationStateMachine contains a mutable struct so we need to prevent struct copies.
        let mutable x = ContinuationStateMachine<_>(f)
        x.Builder.Start(&x)
#if NETSTANDARD2_0
        x.Builder.Task
#else
        x.Builder.Task.AsTask()
#endif

    let runPlyAsTask (ply: Ply<'u>) : Task<'u>  =
        let mutable x = ContinuationStateMachine<_>(ply)
        x.Builder.Start(&x)
#if NETSTANDARD2_0
        x.Builder.Task
#else
        x.Builder.Task.AsTask()
#endif

    // This won't correctly prevent AsyncLocal leakage or SyncContext switches but it does save us the closure alloc
    // Making only this version completely alloc free for the fast path...
    // Read more here https://github.com/dotnet/coreclr/blob/027a9105/src/System.Private.CoreLib/src/System/Runtime/CompilerServices/AsyncMethodBuilder.cs#L954
    let inline runUnwrappedAsTask (f: unit -> Ply<'u>) : Task<'u> =
        let next = f()
        if next.IsCompletedSuccessfully then 
            let mutable b = createBuilder()
            b.SetResult(next.Result)
#if NETSTANDARD2_0
            b.Task
#else
            b.Task.AsTask()
#endif
        else 
            runPlyAsTask next

    let combine (ply : Ply<unit>) (continuation : unit -> Ply<'b>) =
        if ply.IsCompletedSuccessfully then 
            continuation() 
        else 
            Ply(await = PlyAwaitable<unit, 'b>(ply.awaitable, continuation))

    let rec whileLoop (cond : unit -> bool) (body : unit -> Ply<unit>) =
        // As long as we never yield loops are allocation free
        if cond() then
            let next = body()
            if next.IsCompletedSuccessfully then 
                whileLoop cond body
            else
                Ply(await = LoopAwaitable(next.awaitable, cond, body))
        else zero
 
    let tryWith(continuation : unit -> Ply<'u>) (catch : exn -> Ply<'u>) =
        try
            let next = continuation()
            if next.IsCompletedSuccessfully then next else 
                let mutable awaitable = next.awaitable
                Ply(await = { new Awaitable<'u>() with 
                    override __.Await(csm) = awaitable.Await(&csm)
                    override this.GetNext() = 
                        try 
                            let next = awaitable.GetNext()
                            if next.IsCompletedSuccessfully then next else
                                awaitable <- next.awaitable
                                Ply(await = this)
                        with ex -> catch ex
                })
        with ex -> catch ex

    let tryFinally (continuation : unit -> Ply<'u>) (finallyBody : unit -> unit) =
        let inline withFinally f = 
            try f()
            with ex -> 
                finallyBody()
                throwPreserve ex

        let next = withFinally continuation
        if next.IsCompletedSuccessfully then 
            finallyBody()
            next 
        else 
            let mutable awaitable = next.awaitable
            Ply(await = { new Awaitable<'u>() with 
                override __.Await(csm) = awaitable.Await(&csm)
                override this.GetNext() = 
                    let next = withFinally awaitable.GetNext
                    if next.IsCompletedSuccessfully then 
                        finallyBody()
                        next
                    else
                        awaitable <- next.awaitable
                        Ply(await = this)
            })

    let using (disposable : #IDisposable) (body : #IDisposable -> Ply<'u>) =
        tryFinally 
            (fun () -> body disposable) 
            (fun () -> if isNotNull disposable then disposable.Dispose())

    let forLoop (sequence : 'a seq) (body : 'a -> Ply<unit>) =
        using (sequence.GetEnumerator()) (fun e -> whileLoop e.MoveNext (fun () -> body e.Current))

    type [<Struct>]TaskAwaiterMethods<'t> = 
        interface IAwaiterMethods<TaskAwaiter<'t>, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult()
    and [<Struct>]UnitTaskAwaiterMethods<'t> =
        interface IAwaiterMethods<TaskAwaiter, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult(); defaultof<_> // Always unit

    and [<Struct>]ConfiguredTaskAwaiterMethods<'t> = 
        interface IAwaiterMethods<ConfiguredTaskAwaitable<'t>.ConfiguredTaskAwaiter, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult()
    and [<Struct>]ConfiguredUnitTaskAwaiterMethods<'t> = 
        interface IAwaiterMethods<ConfiguredTaskAwaitable.ConfiguredTaskAwaiter, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult(); defaultof<_> // Always unit

    and [<Struct>]YieldAwaiterMethods<'t> = 
        interface IAwaiterMethods<YieldAwaitable.YieldAwaiter, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult(); defaultof<_> // Always unit

    and [<Struct>]GenericAwaiterMethods<'awt, 't when 'awt :> ICriticalNotifyCompletion> = 
        interface IAwaiterMethods<'awt, 't> with 
            member __.IsCompleted awt = false // Always await, this way we don't have to specialize per awaiter
            member __.GetResult awt = defaultof<_> // Always unit because we wrap this continuation to always be unit -> Ply<'u>

#if !NETSTANDARD2_0        
    and [<IsReadOnly; Struct>]ValueTaskAwaiterMethods<'t> =
        interface IAwaiterMethods<ValueTaskAwaiter<'t>, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult()
    and [<IsReadOnly; Struct>]UnitValueTaskAwaiterMethods<'t> =
        interface IAwaiterMethods<ValueTaskAwaiter, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult(); defaultof<_> // Always unit

    and [<IsReadOnly; Struct>]ConfiguredValueTaskAwaiterMethods<'t> =
        interface IAwaiterMethods<ConfiguredValueTaskAwaitable<'t>.ConfiguredValueTaskAwaiter, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult()
    and [<IsReadOnly; Struct>]ConfiguredUnitValueTaskAwaiterMethods<'t> = 
        interface IAwaiterMethods<ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter, 't> with 
            member __.IsCompleted awt = awt.IsCompleted
            member __.GetResult awt = awt.GetResult(); defaultof<_> // Always unit
#endif

    type Binder<'u>() =
        // Each Bind method here has an extraneous fun x -> cont x in its body for optimization purposes.
        // It does not actually allocate an extra closure as it's seen as an alias by the compiler -
        // but it does help delay 'cont' from allocating until we really need it as an FSharpFunc.
        // The IsCompleted branch works fine without the alloc because it inlines all the way up the CE.
        // It's a mess really...
        
        // Secondly, for every GetResult — because all calls to bind overloads are wrapped by TaskBuilder.Run — we are
        // already running within our own Excecution context bubble. No need to be careful calling GetResult. 

        // We keep Await non inline to protect internals to maximize binary compatibility.
        static member Await<'methods, 'awt, 't when 'methods :> IAwaiterMethods<'awt, 't>>(awt: byref<'awt>, cont: 't -> Ply<'u>) = 
            Ply(await = TplAwaitable(defaultof<'methods>, awt, cont))

        static member inline Specialized<'methods, ^awt, 't 
                                when 'methods :> IAwaiterMethods< ^awt, 't> 
                                and ^awt :> ICriticalNotifyCompletion
                                and ^awt : (member get_IsCompleted: unit -> bool)
                                and ^awt : (member GetResult: unit -> 't) >  
            (awt: ^awt, cont: 't -> Ply<'u>) = 
            if (^awt : (member get_IsCompleted: unit -> bool) (awt)) then  
                cont (^awt : (member GetResult: unit -> 't) (awt))
            else
                let mutable mutAwt = awt
                Binder<'u>.Await<'methods,_,_>(&mutAwt, (fun x -> cont x))

        // We have special treatment for unknown taskLike types where we wrap the continuation in a unit func
        // This allows us to use a single GenericAwaiterMethods type (zero alloc, small drop in perf) instead of an object expression.
        static member inline Generic(task: ^taskLike, cont: 't -> Ply<'u>) =
            let awt = (^taskLike : (member GetAwaiter: unit -> ^awt) (task))
            if (^awt : (member get_IsCompleted: unit -> bool) (awt)) then  
                cont (^awt : (member GetResult: unit -> 't) (awt))
            else
                // Leave original awt symbol immutable, otherwise it'll cost us an FsharpRef due to the capture.
                let mutable mutAwt = awt
                // This continuation closure is actually also just one alloc as the compiler simplifies the 'would be' cont into this one.
                Binder<'u>.Await<GenericAwaiterMethods<_,_>,_,_>(&mutAwt, (fun () -> cont (^awt : (member GetResult : unit -> 't) (awt))))

        static member PlyAwait(ply: Ply<'t>, cont: 't -> Ply<'u>) = 
            Ply(await = PlyAwaitable(ply.awaitable, (fun x -> cont x)))

        static member inline Ply(ply: Ply<'t>, cont: 't -> Ply<'u>) = 
            if ply.IsCompletedSuccessfully then 
                cont ply.Result 
            else 
                Binder<'u>.PlyAwait(ply, (fun x -> cont x))

    // Supporting types to have the compiler do what we want with respect to overload resolution.
    type Id<'t> = class end
    type Default2() = class end
    type Default1() = inherit Default2()

    type Bind() = 
        inherit Default1()

        static member inline Invoke (task, cont: 't -> Ply<'u>) = 
            let inline call_2 (task: ^b, cont, a: ^a) = ((^a or ^b) : (static member Bind : _*_*_ -> Ply<'u>) task, cont, a)
            let inline call (task: 'b, cont, a: 'a) = call_2 (task, cont, a)
            call(task, cont, defaultof<Bind>)

        static member inline Bind(task: ^taskLike, cont: 't -> Ply<'u>, [<Optional>]_impl:Default2) = 
            Binder<'u>.Generic(task, cont)

        static member inline Bind(task: Task, cont: unit -> Ply<'u>, [<Optional>]_impl:Default1) = 
            Binder<'u>.Specialized<UnitTaskAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont)

        static member inline Bind(task: Task<'t>, cont: 't -> Ply<'u>, [<Optional>]_impl:Bind) = 
            Binder<'u>.Specialized<TaskAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont)
     
        static member inline Bind(task: ConfiguredTaskAwaitable<'t>, cont: 't -> Ply<'u>, [<Optional>]_impl:Bind) = 
            Binder<'u>.Specialized<ConfiguredTaskAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont)
    
        static member inline Bind(task: ConfiguredTaskAwaitable, cont: unit -> Ply<'u>, [<Optional>]_impl:Bind) =
            Binder<'u>.Specialized<ConfiguredUnitTaskAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont) 

        static member inline Bind(task: YieldAwaitable, cont: unit -> Ply<'u>, [<Optional>]_impl:Bind) =
            Binder<'u>.Specialized<YieldAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont) 

        static member inline Bind(async: Async<'t>, cont: 't -> Ply<'u>, [<Optional>]_impl:Bind) = 
            Binder<'u>.Specialized<TaskAwaiterMethods<_>,_,_>((Async.StartAsTask async).GetAwaiter(), cont)

        static member inline Bind(ply: Ply<'t>, cont: 't -> Ply<'u>, [<Optional>]_impl:Bind) = 
            Binder<'u>.Ply(ply, cont)

        static member inline Bind(_: Id<'t>, _: 't -> Ply<'u>, [<Optional>]_impl:Bind) = 
            failwith "Used for forcing delayed resolution."

#if !NETSTANDARD2_0   
        static member inline Bind(task: ValueTask<'t>, cont: 't -> Ply<'u>, [<Optional>]_impl:Bind) = 
            Binder<'u>.Specialized<ValueTaskAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont) 
        
        static member inline Bind(task: ValueTask, cont: unit -> Ply<'u>, [<Optional>]_impl:Bind) = 
            Binder<'u>.Specialized<UnitValueTaskAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont) 

        static member inline Bind(task: ConfiguredValueTaskAwaitable<'t>, cont: 't -> Ply<'u>, [<Optional>]_impl:Bind) = 
            Binder<'u>.Specialized<ConfiguredValueTaskAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont) 

        static member inline Bind(task: ConfiguredValueTaskAwaitable, cont: unit -> Ply<'u>, [<Optional>]_impl:Bind) = 
            Binder<'u>.Specialized<ConfiguredUnitValueTaskAwaiterMethods<_>,_,_>(task.GetAwaiter(), cont) 
#endif

    type AwaitableBuilder() =
        member inline __.Delay(body : unit -> Ply<'t>) = body
        member inline __.Return(x)                     = ret x
        member inline __.Zero()                        = zero

        member inline __.ReturnFrom(task: ^taskLike)                        = Bind.Invoke(task, ret)
        member inline __.Bind(task: ^taskLike, continuation: 't -> Ply<'u>) = Bind.Invoke(task, continuation)

        member inline __.Combine(ply : Ply<unit>, continuation: unit -> Ply<'t>)          = combine ply continuation
        member inline __.While(condition : unit -> bool, body : unit -> Ply<unit>)        = whileLoop condition body
        member inline __.TryWith(body : unit -> Ply<'t>, catch : exn -> Ply<'t>)          = tryWith body catch
        member inline __.TryFinally(body : unit -> Ply<'t>, finallyBody : unit -> unit)   = tryFinally body finallyBody
        member inline __.Using(disposable : #IDisposable, body : #IDisposable -> Ply<'u>) = using disposable body
        member inline __.For(sequence : seq<_>, body : _ -> Ply<unit>)                    = forLoop sequence body
