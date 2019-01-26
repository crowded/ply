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
        member inline __.Run(f : unit -> Ply<'u>) : Task<'u> = 
#if NETSTANDARD2_0      
            run f
#else     
            (run f).AsTask()
#endif

    let task = TaskBuilder()

#if !NETSTANDARD2_0
    type ValueTaskBuilder() =
        inherit AwaitableBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = run f
    
    let vtask = ValueTaskBuilder()

module AdvancedBuilders =
    type UnsafeValueTaskBuilder() =
        inherit AwaitableBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = runUnwrapped f

    let uvtask = UnsafeValueTaskBuilder()

    type PlyBuilder() =
        inherit AwaitableBuilder()
        member inline __.Run(f : unit -> Ply<'u>) = f()

    let ply = PlyBuilder()
#endif

