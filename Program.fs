open System
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

open System.Threading
open Akka.Actor
open Akka.FSharp
open Engine
open Client

let app =
    let test = "gooz"

    choose [ GET
             >=> choose [ path "/" >=> OK test
                          path "/hello" >=> OK "Hello!" ]
             POST
             >=> choose [ path "/hello" >=> OK "Hello POST!" ] ]

[<EntryPoint>]
let main argv =
    startWebServer defaultConfig app
    0
