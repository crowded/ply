// Learn more about F# at http://fsharp.org

open FSharp.Control.Tasks.Builders
open System.Threading.Tasks

module Say =
    let hello name = task {
        let! x = Task.FromResult(name)
        printfn "Hello %s" x
    }
    
    let helloVtask name = vtask {
        let! x = Task.FromResult(name)
        printfn "Hello %s" x
    }


open Say

[<EntryPoint>]
let main argv =
    (hello "ply").GetAwaiter().GetResult()
    (helloVtask "ply").GetAwaiter().GetResult()
    0 // return an integer exit code
