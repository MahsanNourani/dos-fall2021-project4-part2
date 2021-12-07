# dos-fall2021-project4-part2

Group Members:
Mahsan Nourani - Shaghayegh "Shae" Esmaeili

## Final Project - Part 2 (Project 4)

**TODO: Need to update for project 2**

In this project, we implemented a clone of Twitter, including (1) an engine actor, (2) a client actor which can communicate with the engine, and (3) a simulator actor to test functionalities of our system and run controlled experiments. In this report, we have thoroughly described each actor and their functionality, followed by details of our controlled experiments, performance numbers, and interesting observations.
The project is implemented using F# and Akka.NET API for actor modeling.

### How to Execute the Program

To run the project, you simply would run the command below in your console, where:

- `numUsers` is the total number of registered users

```
dotnet run numUsers
// e.g., dotnet run 100, to run the simulation for 100 registered users.
```

Note that we store numUsers in an integer, which can store up to 2^32, and hence, the maximum number of users this program supports is 2^32.

### Terminology

We call this new system Bird App. Coo is the low, sweet sound that a pigeon or dove makes, which inspired our application terms. In our program, each post is called a coo. In the spirit of coo-ing, a coo-er is a person who posts a coo. Recoo-ing is the act of re-posting a coo that was originally made by another coo-er. We call this new post a Re-coo.

### What is working?

We implemented all the required pieces in the project description. Our program works concurrently based on actor models in Akka.NET. We support three actors that are designed to achieve different tasks. Here, we briefly describe each actor and include some design choices. Please refer to the project report for the full list of functionalities for the actors.

### Engine Actor

The engine actor is at the heart of the program. We implemented this to handle all the functionalities that a twitter engine should include, while it also served as the database storage for all the information about the users (e.g., users, subscriptions, coos, and live users). We made sure that other actors cannot change these values directly and have to send messages to the engine actor to access the stored data. The Engine actor mainly includes twitter functionalities, but we also added two functions that would solely be used for simulation purposes. We had to make a decision here to whether include these functionalities in the Engine actor or not, and our choice came down to 1. including these functionalities with simulator and access the database directly from Simulator and bypassing the Engine as the entry point, or 2. allowing the Engine to be the gateway to the database and adding two simulation features to the Engine. We chose the latter to avoid concurrency issues and stay consistent.

### Client Actor

Each registered user is represented by a client actor with a unique username (string), which we refer to cid. When a new user wants to register, we first spawn a client actor (for simulation purposes, we generate a random cid `'client' + number` where `number` is an integer from 0 to `numOfUsers`). For all the other client functionalities, such as login, postACoo, etc., we have the client send messages to the Engine actor with the request. So for the most part, Client actor serves as an API to connect to the server.

### Simulator Actor

This actor focuses on simulating a situation where many users register, login, tweet and retweet, follow each other, and so on. So the Simulator actor serves mostly as a way for us to test and simulate the actual twitter (in this case, our Bird Engine). You can easily bypass using a simulator and use the client and Engine actors directly as you wish.
Below are the steps we take in simulation:

- Spawn Client actors based on `numOfUsers`.
- Register the new Clients
- Using a Zipf distribution, we make sure some users subscribe to the others.
- Clients that are registered start posting Coos. Each client would post Coos that match the number of their subscribers (following the Zipf distribution). For this step, we wrote a function `createRndCoo`, that generates a body of text that includes:

```
let mutable cooText = "Coo Simulation: " + first random word of size 4--8 + ", " + second random word of size 4--8 + " ".

// generate two random booleans, *includesHashtag* and **includesMention**, that decide whether the tweet text should include hashtags and mentions.

// if the tweet includes hashtags, then we generate at least 1 up to 4 hashtags randomly, adding it to `cooText`.

// if the tweet includes mentions, then we generate at least 1 up to 4 user IDs randomly, adding it to `cooText`. Note that here, we know user names because they are 'client' + [0..numOfUsers].

```

- Clients would then start Re-cooing. We first designed this so that the number of re-coos would correspond to the number of coos (Zipf distribution). However, this increased the performance time exponentially. For simplicity, we then opted for each user randomly selecting 2 of the coos in their timeline and re-coo them.

For more information, please refer to the project report.
