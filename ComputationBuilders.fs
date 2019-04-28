// Optimized (Value)Task computation expressions for F#
// Author: Nino Floris - mail@ninofloris.com
// Copyright (c) 2019 Crowded B.V.
// Distributed under the MIT License (https://opensource.org/licenses/MIT).

#nowarn "44"
namespace FSharp.Control.Tasks

open Ply
open Ply.TplPrimitives
open System.Threading.Tasks

[<AutoOpen>]
module Builders = 
    type TaskBuilder() =
        inherit AwaitableBuilder()
        member inline __.Run(f : unit -> Ply<'u>) : Task<'u> = runAsTask f

    let task = TaskBuilder()

    type UnitTaskBuilder() =
        inherit AwaitableBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = 
#if NETSTANDARD2_0      
            (runAsTask f) :> Task
#else     
            let t = run f
            if t.IsCompletedSuccessfully then Task.CompletedTask else t.AsTask() :> Task
#endif

    let unitTask = UnitTaskBuilder()

#if !NETSTANDARD2_0
    type ValueTaskBuilder() =
        inherit AwaitableBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = run f
    
    let vtask = ValueTaskBuilder()

    type UnitValueTaskBuilder() =
        inherit AwaitableBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = 
            let t = run f
            if t.IsCompletedSuccessfully then ValueTask() else ValueTask(t.AsTask() :> Task)

    let unitVtask = UnitValueTaskBuilder()
#endif

    module Unsafe = 
        type UnsafePlyBuilder() =
            inherit AwaitableBuilder()
            member inline __.Run(f : unit -> Ply<'u>) = f()

        let uply = UnsafePlyBuilder()

        type UnsafeUnitTaskBuilder() =
            inherit AwaitableBuilder()
            member inline __.Run(f : unit -> Ply<'u>) = 
#if NETSTANDARD2_0      
                (runUnwrappedAsTask f) :> Task
#else     
                let t = runUnwrapped f
                if t.IsCompletedSuccessfully then Task.CompletedTask else t.AsTask() :> Task
#endif

        let uunitTask = UnsafeUnitTaskBuilder()

#if !NETSTANDARD2_0
        type UnsafeValueTaskBuilder() =
            inherit AwaitableBuilder()
            member inline __.Run(f : unit -> Ply<'u>) = runUnwrapped f

        let uvtask = UnsafeValueTaskBuilder()
        
        type UnsafeUnitValueTaskBuilder() =
            inherit AwaitableBuilder()
            member inline __.Run(f : unit -> Ply<'u>) = 
                let t = runUnwrapped f
                if t.IsCompletedSuccessfully then ValueTask() else ValueTask(t.AsTask() :> Task)

        let uunitVtask = UnsafeUnitValueTaskBuilder()
#endif
