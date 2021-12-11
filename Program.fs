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

//TODO: make sure the path and the name is correct, wherever it is spawned

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

                //LOGOUT
                elif str.Contains("logout") then
                    printfn "this is the request: %s" str
                    //Retrieving user name
                    let startIndex = str.IndexOf("/") + 1
                    let endIndex = str.LastIndexOf("/")
                    let username = str.[startIndex .. endIndex-1]
                    printfn $"Debug: retrieved username in logout is: {username}"
                    printfn "list of online users before update: %A" listOfOnlineUsers
                    
                    //TODO: Make sure there is no need to update/empty the newsfeed list
                    findClientActor(username) <! Logout

                //FOLLOW
                elif str.Contains("follow") then   //TODO: Later, change follow to "subscribe" in order to be consistent everywhere
                    // (follow/mahsan/shae)
                    printfn "Follow QUERY: %s" str
                    let subscribeeIndex = str.IndexOf("/") + 1
                    let subscriberIndex = str.LastIndexOf("/")
                    
                    let subscribee = str.[subscribeeIndex..subscriberIndex-1] // Username of the person to follow
                    let subscriber = str.[subscriberIndex+1..] // Username of the person who wishes to follow the other person

                    if (userSocketMap.ContainsKey(subscribee)) then
                        // ToDO: follow the user :P
                        findClientActor(subscriber) <! SubscribeTo(subscribee)
                    else
                        let message = "No matched user with handle @" + subscribee + "found to follow!!"
                        sendResponse webSocket message

                //RECOO
                elif str.Contains("recoo") then  //TODO: Change "retweet" to recoo in index.html
                    printfn "this is the request: %s" str
                    //Retrieving user name and coo ID
                    let startIndex = str.IndexOf("/") + 1
                    let endIndex = str.LastIndexOf("/")
                    let username = str.[startIndex .. endIndex-1]
                    //let cooID = str.[endIndex..]  TODO: use cooID if we decided NOT to randomly retweet, and tweet based on the ID
                    findClientActor(username) <! ReCoo   //Randomly select a coo from the newsfeed and post it


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


//Moved it to Engine
//let handlerActor = select @"akka://BirdApp/user/handlerapi" system  

//////////////////////////////////// Handler API Actor ////////////////////////////////////
// Add Handler Actor here

// This is the actor that serves some of the functionalitlies that we had in the Simulator in Project 4.1
// The HandlerAPI works as a bridge between the sockets and clients (or sometimes, the engine).

let HandlerAPI (mailbox:Actor<_>) =
    
    let rec loop() = actor {
        let! message = mailbox.Receive()
        // printfn "SPAWNING Handler API ACTOR in the loop"
        match message with
        | RegisterAPI (username, pass) ->
            // Client knows its username through cid, so we only pass the password (aka pass)
            findClientActor(username) <! Register pass
        
        | LoginAPI (username, pass) ->
            // Client knows its username through cid, so we only pass the password (aka pass)
            printfn $"******: {username}"
            printfn $"******: {findClientActor(username)}"
            findClientActor(username) <! Login pass

        | AckLogin (username, newsfeed) ->
            let userws = fst(userSocketMap.TryFind(username).Value)
            let successMessage = "Login successful for user: " + username
            //Newsfeed update
            if (newsfeed.Count > 0) then   //TODO: need to be tested later, when the newsfeed is filled with coo's.
                let newsfeedMsg = newsfeed |> String.concat "|"
                sendResponse (userws) (successMessage + "/" + newsfeedMsg)    //TODO: Later change the format based on the updated index.html
            else
                //printfn "in else part of the log in ack"
                sendResponse userws successMessage

        | AckSubscribe (username, subscribee) -> 
            let userws = fst(userSocketMap.TryFind(username).Value)
            let successMessage = "You now follow @" + subscribee + "!"
            sendResponse userws successMessage

        //TODO: If possible, merge all ACKs into one message
        | ActionDone (actionType, username) ->
            let userws = fst(userSocketMap.TryFind(username).Value)
            
            match actionType with
            | "Register" -> 
                //TODO: Question: Do I need to make ws true here? or has it been done somewhereelse? 
                let successMessage = "Registration successful for user: " + username
                sendResponse userws successMessage   //reslut -> after successful registeration, the registeration form goes away, and only log in form will stay.
                //TODO: so far assumption is that the ack message is always successful.
                //   Make sure that the unsuccessful message is not necessary.
                
            | "Logout" ->
                printfn "list of online users after update: %A" listOfOnlineUsers
                let successMessage = "Logout successful for user: " + username
                printfn "%s" successMessage
                sendResponse userws successMessage
                //TODO: Change here if anything is needed to be updated in index.html

            | _ -> printfn "Action type <%s> not recognized!" actionType

        | _ -> printfn "Message not recognized!"

        return! loop()

        }
        
    loop()
////////////////////////////////////End Handler API Actor ////////////////////////////////////

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

        Thread.Sleep(500) // This delay makes sure the handshake is done before moving on. Without this, userWS below would have an error (unassigned object)

        let userWsTuple = userSocketMap.TryFind(username).Value

        if (snd (userWsTuple)) then // this means the user was just added, so go ahead and register them. Else, it means the username is already taken!
            let userWs = fst (userWsTuple)
            printfn $"{userWs}"
            userSocketMap <- userSocketMap.Add(username, (userWs, false))
            printfn $"{userSocketMap.TryFind(username).Value}"
            printfn "Success! We can now add this person!"

            // creates client actor for the new user that's registered
            let testClient = spawn system ("client" + username) (ClientActor username userWs) |> ignore 
            printfn $"--**--> {testClient}"
            let newClient = findClientActor(username)
            printfn $"--**--> {newClient}"
            printfn $"{handlerActor}"
            // Thread.Sleep(1000)
            // tells Socket to tell the client to register to the engine
            handlerActor <! RegisterAPI (username, password)

            printfn "After the fact!"
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
            handlerActor <? LoginAPI (username, password) |> ignore
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
    // startWebServer defaultConfig app
    spawn system "handlerapi" HandlerAPI |> ignore // creates a single instance of the handler API to handle all the messages that are transferred between client and sockets.
    // startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
    startWebServer defaultConfig app
    0
