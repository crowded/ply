// Learn more about F# at http://fsharp.org

open netstandard2.Say

[<EntryPoint>]
let main argv =
    
    (hello "ply").GetAwaiter().GetResult()
    0 // return an integer exit code
