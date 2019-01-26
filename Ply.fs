// Optimized (Value)Task computation expressions for F#
// Author: Nino Floris - mail@ninofloris.com
// Copyright (c) 2019 Crowded B.V.
// Distributed under the MIT License (https://opensource.org/licenses/MIT).

namespace rec Ply
open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading.Tasks

module Internal =
    type [<AbstractClass;AllowNullLiteral>] Awaitable<'u>() =
        abstract member Await<'t when 't :> IAwaitingMachine> : machine: byref<'t> -> bool
        abstract member GetNext: unit -> Ply<'u>
    and IAwaitingMachine = 
        abstract member AwaitUnsafeOnCompleted<'awt when 'awt :> ICriticalNotifyCompletion> : awt: byref<'awt> -> unit
    
type [<IsReadOnly; Struct>] Ply<'u> =
    val value : 'u
    val awaiting : bool
    new(result: 'u) = { value = result; awaiting = false }
    new(await: bool) = { value = Unchecked.defaultof<_>; awaiting = await }
    member this.IsCompletedSuccessfully = not this.awaiting
    member this.Result = if this.IsCompletedSuccessfully then this.value else failwith "not completed"

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

    let ret (it: ContinueIt<_>) x = 
        it.SetResult(x)
        Ply(result = x)

    let zero (it: ContinueIt<'Y>) = 
        Ply(result = ())

    // let run (this: AwaitableBuilder<'TResult>) (f: unit -> Ply<'TResult>) =
    //     // Struct contains a mutable struct — Async(Value)TaskMethodBuilder — so we need to prevent copies.
    //     // Cannot have the StateMachine be the one to start the builder as it won't have a byref this   
    //     this.DoRun(f)
        // let mutable x = ContinuationStateMachine<_>(f)
        // x.Builder.Start(&x)
        // x.Builder.Task

    // let runPly (this: AwaitableBuilder<'u>) (ply: Ply<'u>) =
    //     let mutable x = ContinuationStateMachine<_>(ply)
    //     x.Builder.Start(&x)
    //     x.Builder.Task

    // // This won't correctly protect against AsyncLocal leakage or SyncContext switches but it does save us the closure alloc
    // // Making only this version completely alloc free for the fast path...
    // // Read more here https://github.com/dotnet/coreclr/blob/027a9105/src/System.Private.CoreLib/src/System/Runtime/CompilerServices/AsyncMethodBuilder.cs#L954
    // let inline runUnwrapped (this: AwaitableBuilder<'u>) (f: unit -> Ply<'u>) =
    //     let next = f()
    //     if next.IsCompletedSuccessfully then 
    //         let mutable b = createBuilder()
    //         b.SetResult(next.Result)
    //         b.Task
    //     else 
    //         runPly this next

    // let combine (it: ContinueIt<'TResult>) (ply : Ply<unit>) (continuation : unit -> Ply<'b>)  =
    //     if ply.IsCompletedSuccessfully then 
    //         continuation() 
    //     else 
    //         it.Await()
    //         Ply(await = true)

    // let whileLoop (cond : unit -> bool) (body : unit -> Ply<unit>) =
    //     if cond() then
    //         let mutable awaitable: ReusableSideEffectingAwaitable<unit> = defaultof<_>
    //         let rec repeat () =
    //             if cond() then
    //                 let next = body()
    //                 if next.IsCompletedSuccessfully then 
    //                     repeat()
    //                 else 
    //                     awaitable.Reset(awaiting)
    //                     Ply(await = awaitable)
    //             else zero
    //         let next = body()
    //         if next.IsCompletedSuccessfully then 
    //             awaitable <- ReusableSideEffectingAwaitable(defaultof<_>, repeat)
    //             repeat() 
    //         else 
    //             awaitable <- ReusableSideEffectingAwaitable(awaiting, repeat)
    //             Ply(await = awaitable)
    //     else zero

    type ContinueIt<'TResult> = 
#if NETSTANDARD2_0        
        val Builder : AsyncTaskMethodBuilder<'TResult> 
#else
        val Builder : AsyncValueTaskMethodBuilder<'TResult> 
#endif
        val mutable private continuation: unit -> unit

        new(continuation) = { 
            Builder = createBuilder()
            continuation = continuation
        }
    
        member this.SetResult(x) = this.Builder.SetResult(x)

        member this.Await(awt: byref<'awt>, continuation: unit -> unit) = 
            this.continuation <- continuation
            let mutable this = this
            // (this :> IAsyncStateMachine).MoveNext()
            this.Builder.AwaitUnsafeOnCompleted(&awt, &this)

        interface IAsyncStateMachine with
            // This method is effectively deprecated on .NET Core so only .NET Fx will still call this.
            member this.SetStateMachine(csm) = 
                this.Builder.SetStateMachine(csm)
            
            member this.MoveNext() =
                try 
                    this.continuation()
                with ex -> this.Builder.SetException(ex)

    type Binder<'u, 'TResult>() =
        // Each Bind method here has an extraneous fun x -> cont x in its body for optimization purposes.
        // It does not actually allocate an extra closure as it's seen as an alias by the compiler -
        // but it does help delay 'cont' from allocating until we really need it as an FSharpFunc.
        // The IsCompleted branch works fine without the alloc because it inlines all the way up the CE.
        // It's a mess really...
        
        // Secondly, for every GetResult — because all calls to bind overloads are wrapped by TaskBuilder.Run — we are
        // already running within our own Excecution context bubble. No need to be careful calling GetResult. 

        // We keep Await non inline to protect internals to maximize binary compatibility.
        static member Await<'awt, 't when 'awt :> ICriticalNotifyCompletion>(awt: byref<'awt>, cont: unit -> unit, state: ContinueIt<'TResult>) = 
            state.Await(&awt, cont)
            Ply(await = true)

        static member inline Specialized< ^awt, 't 
                                when ^awt :> ICriticalNotifyCompletion
                                and ^awt : (member get_IsCompleted: unit -> bool)
                                and ^awt : (member GetResult: unit -> 't) >  
            (awt: ^awt, cont: 't -> Ply<'u>, state) = 
            if (^awt : (member get_IsCompleted: unit -> bool) (awt)) then  
                cont (^awt : (member GetResult: unit -> 't) (awt))
            else
                // leave original awt symbol immutable, otherwise it'll cost us an FsharpRef due to the capture.
                let mutable mutAwt = awt
                Binder<'u,'TResult>.Await<_,_>(&mutAwt, (fun () -> cont (^awt : (member GetResult : unit -> 't) (awt)) |> ignore), state)

        // We have special treatment for unknown taskLike types where we wrap the continuation in a unit func
        // This allows us to use a single GenericAwaiterMethods type (zero alloc, small drop in perf) instead of an object expression.
        static member inline Generic(task: ^taskLike, cont: 't -> Ply<'u>, state) =
            let awt = (^taskLike : (member GetAwaiter: unit -> ^awt) (task))
            if (^awt : (member get_IsCompleted: unit -> bool) (awt)) then  
                cont (^awt : (member GetResult: unit -> 't) (awt))
            else
                // leave original awt symbol immutable, otherwise it'll cost us an FsharpRef due to the capture.
                let mutable mutAwt = awt
                // This continuation closure is actually also just one alloc as the compiler simplifies the 'would be' cont into this one.
                Binder<'u,'TResult>.Await<_,_>(&mutAwt, (fun () -> cont (^awt : (member GetResult : unit -> 't) (awt)) |> ignore), state)    

    // Supporting types to have the compiler do what we want with respect to overload resolution.
    type Id<'t> = class end
    type Default3() = class end
    type Default2() = inherit Default3()
    type Default1() = inherit Default2()

    type Bind<'TResult>() = 
        inherit Default1()

        static member inline Invoke (task, cont: 't -> Ply<'u>, state: ContinueIt<'TResult>) = 
            let inline call_2 (task: ^b, cont, state, a: ^a) = ((^a or ^b) : (static member T : _*_*_*_ -> Ply<'u>) task, cont, state, a)
            let inline call (task: 'b, cont, state, a: 'a) = call_2 (task, cont, state, a)
            call(task, cont, state, defaultof<Bind<_>>)

        static member inline T(task: ^taskLike, cont: 't -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default3) = 
            Binder<'u,'TResult>.Generic(task, cont, state)

        static member inline T(task: Task, cont: unit -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default2) = 
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state)

        static member inline T(task: Task<'t>, cont: 't -> Ply<'u>, state: ContinueIt<'TResult>,  [<Optional>]_impl:Default1) = 
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state)
     
        static member inline T(task: ConfiguredTaskAwaitable<'t>, cont: 't -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default1) = 
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state)
    
        static member inline T(task: ConfiguredTaskAwaitable, cont: unit -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default1) =
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state) 

        static member inline T(task: YieldAwaitable, cont: unit -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default1) =
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state) 

        static member inline T(async: Async<'t>, cont: 't -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default1) = 
            Binder<'u,'TResult>.Specialized((Async.StartAsTask async).GetAwaiter(), cont, state)

        static member inline T(_: Id<'t>, _: 't -> Ply<'u>, _: ContinueIt<'TResult>, [<Optional>]_impl:Default1) = 
            failwith "Used for forcing delayed resolution."

#if !NETSTANDARD2_0   
        static member inline T(task: ValueTask<'t>, cont: 't -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default1) = 
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state)
        
        static member inline T(task: ValueTask, cont: unit -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default1) = 
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state)

        static member inline T(task: ConfiguredValueTaskAwaitable<'t>, cont: 't -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default1) = 
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state)

        static member inline Bind(task: ConfiguredValueTaskAwaitable, cont: unit -> Ply<'u>, state: ContinueIt<'TResult>, [<Optional>]_impl:Default1) = 
            Binder<'u,'TResult>.Specialized(task.GetAwaiter(), cont, state)
#endif

    type AwaitableBuilder<'TResult> =
        val mutable c: ContinueIt<'TResult>

        new () = { 
            c = defaultof<_>
        }

        member this.DoRun(f: unit -> Ply<'TResult>) =
            this.c <- ContinueIt<_>(fun () -> f() |> ignore)
            this.c.Builder.Start(&this.c)
            this.c.Builder.Task

        member inline __.Delay(f : unit -> Ply<'u>) = f
        member inline this.Return(x)                  = ret this.c x
        member inline this.Zero()                     = zero this.c

        member inline this.ReturnFrom(task: ^taskLike)                         = Bind<'TResult>.Invoke(task, ret this.c, this.c)
        member inline this.Bind(task: ^taskLike, continuation: 't -> Ply<'u>)  = Bind<'TResult>.Invoke(task, continuation, this.c)

        // member inline this.Combine(ts : Ply<unit>, continuation)               = combine this.c ts continuation 
        // member inline __.While(condition : unit -> bool, body : unit -> Ply<unit>) = whileLoop condition body