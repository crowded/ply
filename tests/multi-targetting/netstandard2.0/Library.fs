namespace netstandard2

open FSharp.Control.Tasks.Builders
open System.Threading.Tasks

module Say =
    let hello name = task {
        let! x = Task.FromResult(name)
        printfn "Hello %s" x
    }
