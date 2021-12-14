module Client

open System
open Akka.Actor
open MathNet.Numerics.Distributions
open System.Collections.Generic
open Akka.FSharp
open Suave.WebSocket
open Engine

//////////////////////////////////// Variables ////////////////////////////////////
// Add Variables here
let engineActor =
    select @"akka://BirdApp/user/boss" system

////////////////////////////////////End Variables ////////////////////////////////////

//////////////////////////////////// Functions ////////////////////////////////////
// Add Function here
// TODO: Re-write this function??
// let makeTweet (tweet: string) (username: string) (tweetId: string) (isRetweet: bool) =
//     let mutable  str = ""
//     if isRetweet then
//         str <- "[RETWEET] [" + username + "] [" + tweetId + "] --- " + tweet
//     else
//         str <- "[TWEET] [" + username + "] [" + tweetId + "] --- " + tweet
//     str

////////////////////////////////////End Functions ////////////////////////////////////


//////////////////////////////////// Client Actor ////////////////////////////////////
// Add Client Actor here
let ClientActor (cid: string) (cSocket: WebSocket) (mailbox: Actor<_>) =
    let newsFeed = new List<string>()

    let rec loop () =
        actor {

            let! message = mailbox.Receive()
            let engine = engineActor

            match message with

            | Register password -> engine <! RegisterNewUser(cid, password)
            | Login password -> engine <! LoginUser(cid, password)
            | Logout -> engine <! LogoutUser(cid)
            | SubscribeTo (subscribee) -> engine <! SubscribeToUser(cid, subscribee)
            | Coo (cooContent) -> engine <! PostCoo(cid, cooContent, false)
            | ReCoo cooID -> // TODO: this assumes there's already a bunch of tweets in the lists???
                // Select random Coo from the news Feed
                if (newsFeed.Count = 0) then
                    printfn "Nothing to recoo!!"
                    printfn "%A" newsFeed
                elif (cooID >= newsFeed.Count) then
                    printfn "Your selected Coo does not exist!"
                else
                    printfn "*********DEBUG********* List of coos: %A" listOfCoos
                    let cooContent = listOfCoos.TryFind(cooID).Value

                    // let rndVal = Random()
                    // let zipfDist = Zipf(0.9, newsFeed.Count) //80%-20% distribution    //totUsers - 1, since every user can follow all the other users, except themselves.
                    // // let numOfReCoos = zipfDist.Sample()
                    // let numOfReCoos = 1
                    // // printfn "num of retweets for <%s> is <%d>" cid numOfReCoos

                    // for i in [ 0 .. numOfReCoos - 1 ] do
                    //     let randCoo =
                    //         newsFeed |> Seq.item (rndVal.Next(newsFeed.Count))
                    printfn "in client tweetID: <%d>" cooID
                    printfn "in Client Retweet: <%s>" cooContent
                    engine <! PostCoo(cid, cooContent, true)

            // simActor <! AckReCoo(numOfReCoos)

            | UpdateNewsFeed cooContent ->
                // printfn "updated news feed was: %A" newsFeed
                newsFeed.Insert(0, cooContent) // Adds the new coo to the top of the news feed
                printfn "updated news feed for %s is: %A" cid newsFeed
            // let subscribers =
            //     ListOfSubscribersToUser.TryFind(cid).Value

            // if (isOrignalCooer) then
            //     for item in subscribers do
            //         findClientActor (item)
            //         <! UpdateNewsFeed(cooContent)
            // TODO: Here, need to update news feed of all those who follow you!
            | Ack (ackMessage, actionType) ->
                printfn "Client Msg: <%s>" ackMessage
                // TODO: Fix acks from simulator or printfn to show on screen?? Console should be fine :)
                if (actionType <> "") then
                    if (actionType = "Login") then
                        handlerActor <! AckLogin(cid, newsFeed)

                    elif (actionType = "Subscribe") then
                        let subscribeeIndex = ackMessage.IndexOf('@') + 1

                        let subscribee =
                            ackMessage.[subscribeeIndex..String.length (ackMessage) - 2]

                        handlerActor <! AckSubscribe(cid, subscribee)

                    elif (actionType = "PostRecoo" || actionType = "PostCoo") then
                        let mutable isRecoo = false

                        if (actionType = "PostRecoo") then
                            isRecoo <- true

                        let cooIDIndex = ackMessage.IndexOf('/') + 1
                        let cooContentIndex = ackMessage.LastIndexOf('/')
                        let cooerIndex = ackMessage.IndexOf('@') + 1
                        let cooerIndexEnd = ackMessage.IndexOf(',')

                        let cooer =
                            ackMessage.[cooerIndex..cooerIndexEnd - 1]

                        let cooID =
                            ackMessage.[cooIDIndex..cooContentIndex - 1]
                            |> int

                        let cooContent =
                            ackMessage.[cooContentIndex + 1..ackMessage.Length - 1]

                        handlerActor
                        <! AckCoo(cid, cooer, cooID, cooContent, isRecoo)
                    else
                        handlerActor <! ActionDone(actionType, cid)

            | Error (errorMessage) -> printfn "Error Msg: <%s>" errorMessage

            /// When searcheing, if searching based on mentions, user needs to add @ in front of search term.
            /// if searching for hashtags, user needs to add # in the beginning of the search term
            /// if searching for username, (e.g., a subscriber of theirs) then no additional characters are needed.
            | Search (searchTerm) ->
                printfn "****DEBUG***** in search - search term: %s" searchTerm

                let mention =
                    findRegexPattern (searchTerm, mentionRegex)

                let hashtag =
                    findRegexPattern (searchTerm, hashtagRegex)

                if (mention.Count <> 0) then
                    engine
                    <! QueryMentionedCoosFor(cid, (mention.[0] |> string).Substring(1))
                elif (hashtag.Count <> 0) then
                    printfn "here in hashtag!"

                    engine
                    <! QueryHashtagCoosFor(cid, (hashtag.[0] |> string).Substring(1))
                else
                    engine <! QuerySubscribersCoos(cid, searchTerm)
            | SearchResults (searchTerm, res, searchTermExtras) ->
                handlerActor
                <! AckQuery(cid, searchTerm, res, searchTermExtras)

                printfn "Hey, here are the results of your search for <%s>: %A" searchTerm res

            | _ -> printfn "Message not recognized!"

            return! loop ()
        }

    loop ()
////////////////////////////////////End Client Actor ////////////////////////////////////
