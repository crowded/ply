open System.Reflection
open BenchmarkDotNet.Running

type private This = class end

[<EntryPoint>]
let main argv =
    let benchmarks = BenchmarkSwitcher.FromAssembly(typeof<This>.GetTypeInfo().Assembly)
    benchmarks.Run(argv) |> ignore
    0 // return an integer exit code
