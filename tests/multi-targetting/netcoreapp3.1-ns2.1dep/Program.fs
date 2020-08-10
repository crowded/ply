// Learn more about F# at http://fsharp.org

open netstandard21.Say

[<EntryPoint>]
let main argv =
    
    (hello "ply").GetAwaiter().GetResult()
    (helloVtask "ply").GetAwaiter().GetResult()
    0 // return an integer exit code
