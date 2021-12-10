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
            | ReCoo ->
                // Select random Coo from the news Feed
                if (newsFeed.Count = 0) then
                    printfn "Nothing to recoo!!"
                    printfn "%A" newsFeed
                else
                    let rndVal = Random()
                    let zipfDist = Zipf(0.9, newsFeed.Count) //80%-20% distribution    //totUsers - 1, since every user can follow all the other users, except themselves.
                    // let numOfReCoos = zipfDist.Sample()
                    let numOfReCoos = 2
                    // printfn "num of retweets for <%s> is <%d>" cid numOfReCoos

                    for i in [ 0 .. numOfReCoos - 1 ] do
                        let randCoo =
                            newsFeed |> Seq.item (rndVal.Next(newsFeed.Count))

                        // printfn "Random Retweet: <%s>" randCoo
                        engine <! PostCoo(cid, randCoo, true)

            // simActor <! AckReCoo(numOfReCoos)

            | UpdateNewsFeed cooContent ->
                // printfn "updated news feed was: %A" newsFeed
                newsFeed.Insert(0, cooContent) // Adds the new coo to the top of the news feed
            // printfn "updated news feed is: %A" newsFeed
            | Ack (ackMessage, actionType) -> 
                printfn "Client Msg: <%s>" ackMessage
            // TODO: Fix acks from simulator or printfn to show on screen?? Console should be fine :)
                if (actionType <> "") then
                    if (actionType = "Login") then
                        handlerActor <! AckLogin(cid, newsFeed)
                    else
                        handlerActor <! ActionDone(actionType, cid)

            | Error (errorMessage) -> printfn "Error Msg: <%s>" errorMessage

            /// When searcheing, if searching based on mentions, user needs to add @ in front of search term.
            /// if searching for hashtags, user needs to add # in the beginning of the search term
            /// if searching for username, (e.g., a subscriber of theirs) then no additional characters are needed.
            | Search (searchTerm) ->
                let mention =
                    findRegexPattern (searchTerm, mentionRegex)

                let hashtag =
                    findRegexPattern (searchTerm, hashtagRegex)

                if (mention.Count <> 0) then
                    engine
                    <! QueryMentionedCoosFor(cid, (mention.[0] |> string).Substring(1))
                elif (hashtag.Count <> 0) then
                    engine
                    <! QueryHashtagCoosFor(cid, (hashtag.[0] |> string).Substring(1))
                else
                    engine <! QuerySubscribersCoos(cid, searchTerm)
            | SearchResults (searchTerm, res) ->
                printfn "Hey, here are the results of your search for <%s>: %A" searchTerm res

            | _ -> printfn "Message not recognized!"

            return! loop ()
        }

    loop ()
////////////////////////////////////End Client Actor ////////////////////////////////////
