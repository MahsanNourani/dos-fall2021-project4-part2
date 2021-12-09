open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils

open System
open System.Net

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open System.Threading
open Akka.Actor
open Akka.FSharp
open Engine
open Client

let birdAppEngine = spawn system "boss" (EngineActor 100) // TODO: need to get rid of live user percentage in Engine
// username, and a tuple of websocket and a boolean that represents whether this user just registered or not.
// Here, True means it was just added.
let mutable userSocketMap: Map<string, (WebSocket * bool)> = Map.empty

// For the below code (WS implementation of webSocket is), we started by using this template from Suave.io
// https://github.com/SuaveIO/suave/blob/master/examples/WebSocket/Program.fs --> Here is the link for the complete working example
// https://suave.io/websockets.html --> Here is the link for the example description


// My understanding: each browser tab is a WebSocket (ws) that is yet to be assigned to a user. So What happens is that we create websockets with unique names
// to make sure each websocket we create belongs exactly to one thing only.
// Once user registers, we assign the websocket to that username/client
let ws (webSocket: WebSocket) (context: HttpContext) =
    socket {
        // if `loop` is set to false, the server will stop receiving messages
        let mutable loop = true

        while loop do
            // the server will wait for a message to be received without blocking the thread
            let! msg = webSocket.read ()

            match msg with
            // the message has type (Opcode * byte [] * bool)
            //
            // Opcode type:
            //   type Opcode = Continuation | Text | Binary | Reserved | Close | Ping | Pong
            //
            // byte [] contains the actual message
            //
            // the last element is the FIN byte, explained later
            | (Text, data, true) ->
                // the message can be converted to a string
                let str = UTF8.toString data
                let response = sprintf "response to %s" str
                printfn "%s" response

                //REGISTER
                if str.Contains("register") then
                    printfn "this is the request: %s" str
                    let usernameIdx = str.IndexOf("/") + 1
                    let username = str.[usernameIdx..]
                    // TODO: Why is there a problem here??
                    if (userSocketMap.ContainsKey(username)) then // if username is already taken, send the user response that this is inavlid!
                        printfn "TODO: send response to user that this username is already taken!"
                    else
                        userSocketMap <- userSocketMap.Add(username, (webSocket, true))
                
                //LOGIN
                // elif str.Contains("login") then
                    //TODO: They don't have it for login. Make sure that login is handled in client/server file, AND there is
                    //   no need to have it here

                // the response needs to be converted to a ByteSegment
                let byteResponse =
                    response
                    |> System.Text.Encoding.ASCII.GetBytes
                    |> ByteSegment

                // the `send` function sends a message back to the client
                do! webSocket.send Text byteResponse true

            | (Close, _, _) ->
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true

                // after sending a Close message, stop the loop
                loop <- false

            | _ -> ()
    }

let register =
    request (fun req ->
        let username =
            match req.queryParam "uname" with
            | Choice1Of2 uname -> uname
            | _ -> "argument uname was not found!!"

        let password =
            match req.queryParam "pass" with
            | Choice1Of2 pass -> pass
            | _ -> "argument pass was not found!!"

        Thread.Sleep(1000) // This delay makes sure the handshake is done before moving on. Without this, userWS below would have an error (unassigned object)

        let userWsTuple = userSocketMap.TryFind(username).Value

        if (snd (userWsTuple)) then // this means the user was just added, so go ahead and register them. Else, it means the username is already taken!
            let userWs = fst (userWsTuple)
            printfn $"{userWs}"
            userSocketMap <- userSocketMap.Add(username, (userWs, false))
            printfn $"{userSocketMap.TryFind(username).Value}"
            printfn "Success! We can now add this person!"

        // TODO: create a client actor based on this info - cid = username, need to add websocket to the client
        // TODO: call the worker dude (once we have it) to register this new client
        else
            printfn "user is already registered!!"


        NO_CONTENT) // So that we don't need the OK (DO_SOMETHING_HERE). With OK(..), the content would cover the whole screen instead of inside the page.

let login =
    request (fun req ->
        let username =
            match req.queryParam "uname" with
            | Choice1Of2 uname -> uname
            | _ -> "argument uname was not found!!"

        let password =
            match req.queryParam "pass" with
            | Choice1Of2 pass -> pass
            | _ -> "argument pass was not found!!"

        //Checks whether the username exists or not
        if userSocketMap.ContainsKey(username) then
            // TODO: call the worker dude (once we have it) to login this user
            // For example: selectWorker <? Login(username, pwd) |> ignore
            printfn "Success! You are logged in!"
        else
            printfn "User is not registered!!"

        NO_CONTENT) // So that we don't need the OK (DO_SOMETHING_HERE). With OK(..), the content would cover the whole screen instead of inside the page.


let app =

    choose [ pathScan "/websocket/%s" (fun uID -> path ("/websocket/" + uID) >=> handShake ws) // handles the handShake for this specific websocket
             GET
             >=> choose [ path "/" >=> file "index.html"
                          path "/register" >=> register
                          path "/login" >=> login ]
             POST >=> choose [ path "/" >=> NO_CONTENT ]
             NOT_FOUND "Found no handlers." ]

[<EntryPoint>]
let main argv =
    startWebServer defaultConfig app
    0
