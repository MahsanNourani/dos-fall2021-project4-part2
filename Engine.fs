module Engine

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Akka.Actor
open Akka.FSharp

open Suave.WebSocket
open Suave.Sockets
open Suave.Sockets.Control

let system = ActorSystem.Create("BirdApp")

let handlerActor = select @"akka://BirdApp/user/handlerapi" system

//////////////////////////////////// Variables ////////////////////////////////////
// Add Variables here
let mutable usernames: Set<string> = Set.empty // List of all the usernames in the system
let mutable loginInfo: Map<string, string> = Map.empty // Maps each username to a password - not secure as passwords are not encrypted
let mutable userCoos: Map<string, Set<int>> = Map.empty // maps username to coo ID
let mutable listOfCoos: Map<int, string> = Map.empty // maps coo ID to coo text
let mutable ListOfSubscribersToUser: Map<string, Set<string>> = Map.empty // maps username to set of usernames that follow this user
let mutable userMentionedCoos: Map<string, Set<int>> = Map.empty // maps username to coos IDs they're mentioned in
let mutable userHashtagCoos: Map<string, Set<int>> = Map.empty // maps hashtag string to coos IDs they've been used in
let mutable listOfOnlineUsers: Set<string> = Set.empty // a set of usernames of those online rn

let mentionRegex = "@[A-Za-z0-9]*"
let hashtagRegex = "#[A-Za-z0-9]*"

let rnd = Random(0)


// let simActor =
//     select @"akka://BirdApp/user/simulator" system

////////////////////////////////////End Variables ////////////////////////////////////

//////////////////////////////////// Actor Messages ////////////////////////////////////
// Add Actor Messages here
type ActorMessage =
    | RegisterNewUser of string * string // username and password are both strings
    | LoginUser of string * string // username of the person who wants to connect
    | LogoutUser of string // username that we want to logout
    | SubscribeToUser of string * string // username to subscribe another username
    | PostCoo of string * string * bool
    | QueryMentionedCoosFor of string * string
    | QueryHashtagCoosFor of string * string
    | QuerySubscribersCoos of string * string //subscriber's username
    | FindAllSubscribers of string //username
    | SimulateOnlineUsers
    | SimulateUsersSubscribers of Map<string, Set<string>>
    // Client Messages:
    | Register of string
    | Login of string
    | Logout
    | SubscribeTo of string
    | Coo of string
    | ReCoo
    | Search of string
    | SearchResults of string * Set<string>
    | UpdateNewsFeed of string
    | Ack of string * string
    | Error of string
    //Handler API Messages:
    | RegisterAPI of string * string
    | LoginAPI of string * string
    | AckLogin of string * List<string>
    | AckSubscribe of string * string
    | ActionDone of string * string
    
    // Simulation Messages:
    // | StartSimulation
    // | SimulateSubscribers
    // | SimulateCooing
    // | SimulateReCooing
    // | AckReCoo of int
    // | ActionDone of string
    // | FinishTimer of string
////////////////////////////////////End Actor Messages ////////////////////////////////////


//////////////////////////////////// Functions ////////////////////////////////////
// Add Function here

let findRegexPattern (tweet: string, pattern: string) =
    let r = Regex(pattern)
    let matchCollection = r.Matches tweet
    matchCollection // returns a collection of all the patterns matched!

//This function selects a random subset of size n from a set
let randomSubSet (n: int, set: Set<string>) =
    seq {
        let i =
            set |> Set.toSeq |> Seq.item (rnd.Next(set.Count))

        yield i
        yield! set |> Set.remove i
    }
    |> Seq.take n
    |> Set.ofSeq

let calcNumLiveUsers (liveUsersPerc: int) =
    let totalCountUsers = usernames.Count |> float

    let livePerc =
        ((liveUsersPerc |> float) / (100 |> float))
        |> float

    let result =
        Math.Floor(totalCountUsers * livePerc) |> int
    // printfn "The result of calcNumLiveUsers function is %d" result
    result

let findClientActor (username: string) =
    let path = @"akka://BirdApp/user/client" + username

    select path system

//TODO: if got time, change
let convertByte (text: string) =
     text |> System.Text.Encoding.ASCII.GetBytes |>ByteSegment

let sendResponse (webSocket : WebSocket) (message: string) =
    let msg = convertByte message
    //printfn "Printing msg %A" msg

    let s = socket {
            let! res = webSocket.send Text msg true 
            return res;
            }
    Async.StartAsTask s |> ignore
////////////////////////////////////End Functions ////////////////////////////////////


//////////////////////////////////// Engine Actor ////////////////////////////////////
// Add Engine Actor here
let EngineActor liveUsersPerc (mailbox: Actor<_>) =
    printfn "live users percentage is %d" liveUsersPerc

    // Done?? TODO: Add a function here that based on the liveUsersPerc, randomly selects users from the list of usernames and adds them to onlineusers list.
    let mutable curNumOfLiveUsers = calcNumLiveUsers (liveUsersPerc)


    /// coo is the low, sweet sound that a dove/pigeon makes, which inspired our application!
    /// In our program, each post is called a coo.
    /// In the spirit of coo-ing, a cooer (coo-er) is the person who posts a coo!
    /// Recooing is the act of re-posting a coo that was originally made by another cooer! We call this Recoo.
    /// Below is a series of functions that handle the act of coo-ing.

    let postACoo (cooer, cooContent, isRecoo) =
        // printfn "entered postACoo!"
        let mutable temp = userCoos.TryFind(cooer).Value
        temp <- temp.Add(listOfCoos.Count) // saves ID of Coos
        userCoos <- userCoos.Add(cooer, temp)
        // printfn "This is userCoos %A" userCoos
        listOfCoos <- listOfCoos.Add(listOfCoos.Count, cooContent) // This is basically adding the new coo with the id of the size of all the coos
        // printfn "This is listOfCoos after -- %A" listOfCoos
        if isRecoo then
            findClientActor (cooer)
            <! Ack($"{cooer} successfully re-cooed: {cooContent}!", "PostRecoo")
        else
            findClientActor (cooer)
            <! Ack($"{cooer} successfully posted {cooContent}!", "PostCoo")

    let checkCooForMentions (cooContent: string) =
        let usersMentionedInTheCoo =
            findRegexPattern (cooContent, mentionRegex) // This will return a list of users mentioned in the coo
        // Done! We add an empty username to the mentioned set so all the usernames already exist in the list!!
        // Luxury TODO: check if the mentioned username exists or not. If not, the mention is invalid
        for i in 0 .. usersMentionedInTheCoo.Count - 1 do
            let mentionedUsername =
                (usersMentionedInTheCoo.[i] |> string)
                    .Substring(1) // Remove the @ from the beginning of the mention

            if (userMentionedCoos.ContainsKey(mentionedUsername)) then

                let mutable mentionsSet =
                    userMentionedCoos.TryFind(mentionedUsername).Value

                mentionsSet <- mentionsSet.Add(listOfCoos.Count - 1) // Here, we use -1 because the new tweet is already added and hence, it's ID is -1 than the size.
                // Luxury TODO: Maybe if buggy/confusing, the id can be a global varibale of the latest tweet count?
                userMentionedCoos <- userMentionedCoos.Add(mentionedUsername, mentionsSet)

            // The else portion is for test purposes.
            else
                printfn "Sorry!!Mentioned user <@%s> doesn't exist!!!" mentionedUsername

    // printfn "Map of mentions: %A" userMentionedCoos

    let checkCooForHashtag (cooContent: string) =
        // printfn "entered checkCooForHashtag!"
        let hashtagList =
            findRegexPattern (cooContent, hashtagRegex) //Find all the hashtags in the coo

        //traverese the list of existing hashtags in the coo to check if they already have been used or not
        for i in 0 .. hashtagList.Count - 1 do
            let hashtag = hashtagList.Item i |> string
            let hashtagContent = hashtag.Substring(1) //remove the "#" from the hashtag

            if userHashtagCoos.ContainsKey(hashtagContent) then //if the hashtag already exist, we add the coo ID to the hash map
                let mutable temp =
                    userHashtagCoos.TryFind(hashtagContent).Value

                temp <- temp.Add(listOfCoos.Count - 1)
                userHashtagCoos <- userHashtagCoos.Add(hashtagContent, temp)
            // printfn "Hashtag exists already! Here is the updated userHashtagCoos %A" userHashtagCoos
            else //if the hashtad doesn't exist, we add it to the hashmap
                let mutable temp = Set.empty
                temp <- temp.Add(listOfCoos.Count - 1)
                userHashtagCoos <- userHashtagCoos.Add(hashtagContent, temp)
    // printfn "New Hashtag! Here is the updated userHashtagCoos %A" userHashtagCoos

    let sendCooToSubscribers (cooerUsername, cooContent) =
        // printfn "Updating coo..."

        let listOfSubscribers =
            ListOfSubscribersToUser
                .TryFind(
                    cooerUsername
                )
                .Value

        // printfn "list of subscribers is: <%A>" listOfSubscribers

        if (not (listOfSubscribers.IsEmpty)) then
            for subscriber in listOfSubscribers do
                findClientActor (subscriber)
                <! UpdateNewsFeed(cooContent)

    let rec loop () =
        actor {

            let! message = mailbox.Receive()

            match message with
            | RegisterNewUser (username, password) ->
                if (usernames.Contains(username)) then
                    printfn "Oops!! Username <%s> already exists!!" username
                else
                    usernames <- usernames.Add(username)
                    loginInfo <- loginInfo.Add(username, password) // Project 4.2
                    ListOfSubscribersToUser <- ListOfSubscribersToUser.Add(username, Set.empty)
                    userCoos <- userCoos.Add(username, Set.empty)
                    userMentionedCoos <- userMentionedCoos.Add(username, Set.empty)

                    findClientActor (username)
                    <! Ack($"{username} is now registered.", "Register")

            //create a random subset of online users
            //printfn "In Register: List of live users of size %d is %A" curNumOfLiveUsers listOfOnlineUsers

            //Done! TODO: We need to figure out our login situation AND online/offline newsfeedlist

            | LoginUser (username, password) ->
                if (not (usernames.Contains(username))) then
                    printfn "Sorry! Username <%s> does not exist. please try registering first!" username
                elif (listOfOnlineUsers.Contains(username)) then
                    printfn "Whoops! Username <%s> is already logged in!!" username
                else
                    let onFilePassword = loginInfo.TryFind(username).Value

                    if (onFilePassword = password) then // Line added for Project 4.2
                        //Note: This is not used for the purpose of simulation.
                        //Uncomment if you want to add login functionalities without the randomization.
                        listOfOnlineUsers <- listOfOnlineUsers.Add(username)

                        findClientActor (username)
                        <! Ack($"Yay! {username} is now logged in!!", "Login")
                    else // Line added for Project 4.2
                        printfn
                            "Whoops!! Username and password don't match any registered users. Make sure you entered your information correctly, or try registering for a new account."


            | LogoutUser username ->

                if (not (usernames.Contains(username))) then
                    printfn "Sorry! Username <%s> does not exist. please try registering first!" username
                elif (not (listOfOnlineUsers.Contains(username))) then
                    printfn "Whoops! Username <%s> is not logged in!!" username
                else
                    listOfOnlineUsers <- listOfOnlineUsers.Remove(username)
                    findClientActor (username)
                    <! Ack($"Yay! {username} is now logged out!!", "Logout")

                    printfn "successfully logged out user <%s>" username

            | SubscribeToUser (subscriber, subscribee) -> //subscriber follows subscribee.
                if (usernames.Contains(subscriber)
                    && usernames.Contains(subscribee)
                    && (subscribee <> subscriber)) then
                    let mutable temp =
                        ListOfSubscribersToUser.TryFind(subscribee).Value

                    if (not (temp.Contains(subscriber))) then //only adds this person if subscriber is not already following the subscribee
                        temp <- temp.Add(subscriber)
                        ListOfSubscribersToUser <- ListOfSubscribersToUser.Add(subscribee, temp)

                        findClientActor (subscriber)
                        <! Ack($"{subscriber} now follows @{subscribee}.", "Subscribe")

                        findClientActor (subscribee)
                        <! Ack($"{subscriber} now follow {subscribee}.", "")
                    else
                        printfn "Oops! Username <%s> is already subscribed to user <%s>!!" subscriber subscribee
                else
                    // Done! TODO: send to simulator that these people don't exist
                    printfn
                        "Either the subscriber <%s> or the subscribee <%s> are not registered users!!"
                        subscriber
                        subscribee
            | PostCoo (cooerUsername, cooContent, isRecoo) -> //Done!
                postACoo (cooerUsername, cooContent, isRecoo) // Adds the coo to the server
                checkCooForMentions (cooContent) // searches the coo for mentioned users to update the list
                checkCooForHashtag (cooContent) // searches the coo for hashtags to update the list
                sendCooToSubscribers (cooerUsername, cooContent) // Broadcast the new coo to all of the cooer's subscribers

                findClientActor (cooerUsername)
                <! UpdateNewsFeed(cooContent) // This will show the Coo on the cooers' time line after it is posted to all the other users
            | QueryMentionedCoosFor (querier, username) ->
                let temp =
                    userMentionedCoos.TryFind(username).Value

                if temp.IsEmpty then
                    printfn "User <%s> is not mentioned in any coo-s." username
                else
                    // printfn "list of all tweets: %A" listOfCoos
                    // printfn "temp is %A" temp
                    let result: Set<string> =
                        temp
                        |> Set.map (fun i -> listOfCoos.TryFind(i).Value)

                    findClientActor (querier)
                    <! SearchResults(username, result)

                    printfn "User <%s> has been mentioned in the following coo-s:\n %A" username result
            //Done! TODO: we need to send the result to the client actor

            | QueryHashtagCoosFor (querier, hashtag) ->
                if userHashtagCoos.ContainsKey(hashtag) then
                    let temp = userHashtagCoos.TryFind(hashtag).Value
                    // printfn "list of all tweets: %A" listOfCoos
                    // printfn "temp is %A" temp
                    let result: Set<string> =
                        temp
                        |> Set.map (fun i -> listOfCoos.TryFind(i).Value)

                    findClientActor (querier)
                    <! SearchResults(hashtag, result)
                else
                    findClientActor (querier)
                    <! Error($"Hashtag {hashtag} does not exist yet!")

            | QuerySubscribersCoos (querier, subscriber) ->
                //Assumption: querier always exists.
                let subList =
                    ListOfSubscribersToUser.TryFind(querier).Value

                if (usernames.Contains(subscriber)
                    && subList.Contains(subscriber)) then
                    let temp = userCoos.TryFind(subscriber).Value

                    let result: Set<string> =
                        temp
                        |> Set.map (fun i -> listOfCoos.TryFind(i).Value)

                    findClientActor (querier)
                    <! SearchResults(subscriber, result)
                else
                    findClientActor (querier)
                    <! Error($"{querier} is not subscribed to {subscriber}")

            // findClientActor (querier)
            // <! SearchResults(subscriber, Set.empty) // just to unblock the process on the otherside :D

            | FindAllSubscribers (username) ->
                // Bonus -- Did not need this function yet?
                //Assumption: the one who is querying (username), always exists.
                let temp =
                    ListOfSubscribersToUser.TryFind(username).Value

                if temp.IsEmpty then
                    findClientActor (username)
                    <! Error($"{username} is not subscribed to anyone yet!")
                else
                    findClientActor (username)
                    <! SearchResults("All Subscribers", temp) // sends this even if the set is empty

            | SimulateOnlineUsers ->
                //create a random subset of online users
                curNumOfLiveUsers <- calcNumLiveUsers (liveUsersPerc)

                let temp =
                    randomSubSet (curNumOfLiveUsers, usernames)

                listOfOnlineUsers <- temp
            // simActor <! ActionDone("Login")

            //Is this already resolved?? (the below TODO) -- is this happening in the simulation?? We'll see
            //TODO: we need to send the result to the client actor
            | SimulateUsersSubscribers (subscribersToUser) -> ListOfSubscribersToUser <- subscribersToUser
            // printfn "in engine: ListOfSubscribersToUser is %A" ListOfSubscribersToUser
            // simActor <! ActionDone("Subscribe")
            | _ -> printfn "Message not recognized!"

            return! loop ()
        }

    loop ()
////////////////////////////////////End Engine Actor ////////////////////////////////////
