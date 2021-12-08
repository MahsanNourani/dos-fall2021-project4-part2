open System
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

// open Suave.Http
open Suave.Files
// open Suave.RequestErrors
// open Suave.Logging
// open Suave.Utils
// open Suave.WebSocket

open System.Threading
open Akka.Actor
open Akka.FSharp
open Engine
open Client

let app =
    let test = "gooz"

    choose [ GET
             >=> choose [ path "/" >=> file "index.html"
                          path "/register"
                          >=> OK "TODO: this should call register function!" ] // Mahsan
             POST
             >=> choose [ path "/login"
                          >=> OK "TODO: this should call loging function!" ] ] // Shae

[<EntryPoint>]
let main argv =
    startWebServer defaultConfig app
    0
