# dos-fall2021-project4-part2

Group Members:
Mahsan Nourani - Shaghayegh "Shae" Esmaeili

## Final Project - Part 2 (Project 4)

In this project, we implemented a WebSocket interface for our clone of the Twitter App in part 1 using the Suave web framework. Our project includes:
    (1) an engine actor, 
    (2) a client actor which can communicate with the engine, 
    (3) a handler actor connecting the WebSocket interface to clients and engine, and (4) an interface.
The project is implemented using Suave for WebSocket interface, F#, Akka.NET API for actor modeling, HTML, CSS, and JavaScript to design our web interface.

### How to Execute the Program

To run the project, you simply would run the command below in your console in the project directory to run the webserver:

```
dotnet run

```

Then, you would need to open a browser and type: 127.0.0.1:8080 OR localhost:8080, and the Bird App would run on your local server and listen for web requests on the 8080 port.

### Terminology

We call this new system Bird App. Coo is the low, sweet sound that a pigeon or dove makes, which inspired our application terms. In our program, each post is called a coo. In the spirit of coo-ing, a coo-er is a person who posts a coo. Recoo-ing is the act of re-posting a coo initially made by another coo-er. We call this new post a Re-coo.

### What is working?

We implemented all the required pieces in the project description. All functionalities from part 1 have been connected to the web interface.

The supported functionalities are, but not limited to:
- Register a new user
- Login
- Logout
- Coo
    - Coo's can include mentions and hashtags.
- Re-coo
- Subscribe to other users
- Search
    - a hashtag
    - coo's including a mentioned user
    - all coo's by a specific user
- Actively updating newsfeed
- Logging messages, replies, and errors between handler and WebSocket   

Our program, called *Bird App*, consists of the following parts: 

### Web Interface

We designed and implemented the *Bird App* user interface using HTML, CSS, and JavaScript. More details on the functionalities and how to use the interface can be found in the project demo.

### Suave Web Server and Handler Actor

The Program.fs contains the Suave Web server and the Handler actor. Whenever a user registers, a Client Actor is spawned, and a WebSocket is created, and this client actor handles all the user’s operations. Each corresponding user WebSocket sends the response messages to the UI of the interface. 
We are using REST API for handling all the GET requests for Registration and Login purposes. We are using WebSockets for communications between the client interface and the server.

### Engine Actor

The engine actor is at the heart of the program. We implemented this to handle all the functionalities that a Twitter engine should include, while it also served as the database storage for all the information about the users (e.g., users, subscriptions, coos, and live users). We made sure that other actors cannot change these values directly and have to send messages to the engine actor to access the stored data. The Engine actor mainly includes twitter functionalities.

### Client Actor

A client actor represents each registered user with a unique username (string), referred to as cid. For all the client functionalities, such as login, postACoo, etc., the client sends messages to the Engine actor with the request.

For more information, please refer to the project demo video.