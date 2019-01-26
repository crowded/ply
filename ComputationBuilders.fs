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
    type TaskBuilder<'TResult>() =
        inherit AwaitableBuilder<'TResult>()
        member inline this.Run(f : unit -> Ply<'TResult>) : Task<'TResult> = 
#if NETSTANDARD2_0      
            this.DoRun(f)
#else     
            this.DoRun(f).AsTask()
#endif

    let task<'TResult> = TaskBuilder<'TResult>()

#if !NETSTANDARD2_0
    type ValueTaskBuilder<'TResult>() =
        inherit AwaitableBuilder<'TResult>()
        member inline this.Run(f : unit -> Ply<'TResult>) = this.DoRun(f)
    
    let vtask<'TResult> = ValueTaskBuilder<'TResult>()

// module AdvancedBuilders =
//     type UnsafeValueTaskBuilder<'TResult>() =
//         inherit AwaitableBuilder<'TResult>()
//         member inline this.Run(f : unit -> Ply<'TResult>) = runUnwrapped this f

//     let uvtask<'TResult> = UnsafeValueTaskBuilder<'TResult>()

//     type PlyBuilder<'TResult>() =
//         inherit AwaitableBuilder<'TResult>()
//         member inline __.Run(f : unit -> Ply<'TResult>) = f()

//     let ply<'TResult> = PlyBuilder<'TResult>()
#endif

