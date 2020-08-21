// Optimized (Value)Task computation expressions for F#
// Author: Nino Floris - mail@ninofloris.com
// Copyright (c) 2019 Crowded B.V.
// Distributed under the MIT License (https://opensource.org/licenses/MIT).

namespace FSharp.Control.Tasks

open System
open System.ComponentModel
open Ply
open Ply.TplPrimitives
open System.Threading.Tasks

[<EditorBrowsable(EditorBrowsableState.Never)>]
module Builders = 
    type TaskBuilder() =
        inherit AffineBuilder()
        member inline __.Run(f : unit -> Ply<'u>) : Task<'u> = runAsTask f

    type UnitTaskBuilder() =
        inherit AffineBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = 
            let t = run f
            if t.IsCompletedSuccessfully then Task.CompletedTask else t.AsTask() :> Task

    type ValueTaskBuilder() =
        inherit AffineBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = run f

    type UnitValueTaskBuilder() =
        inherit AffineBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = 
            let t = run f
            if t.IsCompletedSuccessfully then ValueTask() else ValueTask(t.AsTask() :> Task)

    // Backwards compat.
    [<Obsolete("Please open FSharp.Control.Tasks instead of FSharp.Control.Tasks.Builders")>]
    let task = TaskBuilder()
    [<Obsolete("Please open FSharp.Control.Tasks instead of FSharp.Control.Tasks.Builders")>]
    let unitTask = UnitTaskBuilder()
    [<Obsolete("Please open FSharp.Control.Tasks instead of FSharp.Control.Tasks.Builders")>]
    let vtask = ValueTaskBuilder()
    [<Obsolete("Please open FSharp.Control.Tasks instead of FSharp.Control.Tasks.Builders")>]
    let unitVtask = UnitValueTaskBuilder()

    module Unsafe =
        type UnsafePlyBuilder() =
            inherit AffineBuilder()
            member inline __.Run(f : unit -> Ply<'u>) = runUnwrappedAsPly f

        type UnsafeUnitTaskBuilder() =
            inherit AffineBuilder()
            member inline __.Run(f : unit -> Ply<'u>) = 
                let t = runUnwrapped f
                if t.IsCompletedSuccessfully then Task.CompletedTask else t.AsTask() :> Task

        type UnsafeValueTaskBuilder() =
            inherit AffineBuilder()
            member inline __.Run(f : unit -> Ply<'u>) = runUnwrapped f

        type UnsafeUnitValueTaskBuilder() =
            inherit AffineBuilder()
            member inline __.Run(f : unit -> Ply<'u>) =
                let t = runUnwrapped f
                if t.IsCompletedSuccessfully then ValueTask() else ValueTask(t.AsTask() :> Task)

        // Backwards compat.
        [<Obsolete("Please open FSharp.Control.Tasks.Affine.Unsafe instead of FSharp.Control.Tasks.Builders.Unsafe")>]
        let uply = UnsafePlyBuilder()
        [<Obsolete("Please open FSharp.Control.Tasks.Affine.Unsafe instead of FSharp.Control.Tasks.Builders.Unsafe")>]
        let uunitTask = UnsafeUnitTaskBuilder()
        [<Obsolete("Please open FSharp.Control.Tasks.Affine.Unsafe instead of FSharp.Control.Tasks.Builders.Unsafe")>]
        let uvtask = UnsafeValueTaskBuilder()
        [<Obsolete("Please open FSharp.Control.Tasks.Affine.Unsafe instead of FSharp.Control.Tasks.Builders.Unsafe")>]
        let uunitVtask = UnsafeUnitValueTaskBuilder()

    module NonAffine =
        type TaskBuilder() =
            inherit NonAffineBuilder()
            member inline __.Run(f : unit -> Ply<'u>) : Task<'u> = runAsTask f

        type UnitTaskBuilder() =
            inherit NonAffineBuilder()
            member inline __.Run(f : unit -> Ply<'u>) =
                let t = run f
                if t.IsCompletedSuccessfully then Task.CompletedTask else t.AsTask() :> Task

        type ValueTaskBuilder() =
            inherit NonAffineBuilder()
            member inline __.Run(f : unit -> Ply<'u>) = run f

        type UnitValueTaskBuilder() =
            inherit NonAffineBuilder()
            member inline __.Run(f : unit -> Ply<'u>) =
                let t = run f
                if t.IsCompletedSuccessfully then ValueTask() else ValueTask(t.AsTask() :> Task)

        module Unsafe =
            type UnsafePlyBuilder() =
                inherit NonAffineBuilder()
                member inline __.Run(f : unit -> Ply<'u>) = runUnwrappedAsPly f

            type UnsafeUnitTaskBuilder() =
                inherit NonAffineBuilder()
                member inline __.Run(f : unit -> Ply<'u>) =
                    let t = runUnwrapped f
                    if t.IsCompletedSuccessfully then Task.CompletedTask else t.AsTask() :> Task

            type UnsafeValueTaskBuilder() =
                inherit NonAffineBuilder()
                member inline __.Run(f : unit -> Ply<'u>) = runUnwrapped f

            type UnsafeUnitValueTaskBuilder() =
                inherit NonAffineBuilder()
                member inline __.Run(f : unit -> Ply<'u>) =
                    let t = runUnwrapped f
                    if t.IsCompletedSuccessfully then ValueTask() else ValueTask(t.AsTask() :> Task)

[<AutoOpen>]
/// Defines builders that are scheduler affine, respecting the SynchronizationContext or current TaskScheduler.
/// These match C# async await behavior, when building an application you normally want to use these builders.
module Affine =
    open Builders

    let task = TaskBuilder()
    let unitTask = UnitTaskBuilder()
    let vtask = ValueTaskBuilder()
    let unitVtask = UnitValueTaskBuilder()

    module Unsafe =
        open Unsafe
        let uply = UnsafePlyBuilder()
        let uunitTask = UnsafeUnitTaskBuilder()
        let uvtask = UnsafeValueTaskBuilder()
        let uunitVtask = UnsafeUnitValueTaskBuilder()

/// Defines builders that are free of scheduler affinity, rejecting the SynchronizationContext or current TaskScheduler.
/// Also known as Task.ConfigureAwait(false), when building a library you want to use these builders.
module NonAffine =
    open Builders.NonAffine

    let task = TaskBuilder()
    let unitTask = UnitTaskBuilder()
    let vtask = ValueTaskBuilder()
    let unitVtask = UnitValueTaskBuilder()

    module Unsafe =
        open Unsafe
        let uply = UnsafePlyBuilder()
        let uunitTask = UnsafeUnitTaskBuilder()
        let uvtask = UnsafeValueTaskBuilder()
        let uunitVtask = UnsafeUnitValueTaskBuilder()
