# gRPC-Concept-App

This app is a concept example of the gRPC usage with a .NET of 6.0 version.

This app is a console application that provides a functionality for user to:
- Check online status of the server
- Transfer in/out the server 
- Use a live chat
- Test the approximate speed of communication

The following facilities are demonstrated:
- Usage of different communication approaches
- Creation of Protobuf files
- Use Auth with gRPC
- Send and read the gRPC request headers
- Configure gRPC Server
- Configure gRPC channel
- Configure debug Logging of gRPC flow
- Performance best-practice with streams

The 4 gRPC Services were provisioned to demonstrate a different communication methods.
The following communication approaches are demonstrated: 
- Unary requests (Check Online request)
- Client stream (Upload file)
- Server stream (Download file)
- Bi-directional stream (Live chat)

## Application consists of gRPC Client and gRPC Server. 
GrpcService (gRPC Server) is a special designated Grpc.AspNetCore project.

GrpcClient (gRPC Client) is a console app that requires the following packages:
- Google.Protobuf
- Grpc.Net.Client
- Grpc.Tools

## Provisioned gRPC Services description
Each distinct protobuf is a distinct gRPC service.

Onliner - demonstrates a simple Unary request. Has only one "CheckOnline" method.

Transfer - demonstrates both client and server streams in "UploadFile" and "DownloadFile" methods. You can select any file on your PC to upload to the server and then download this file back by the given name. UploadFile and DownloadFile methods require the authorization.

Chatter - demonstrates a bi-directional (duplex) stream in "Chat" method. You can run any number of clients you want. The clients are able to see the each other messages. Chat require authorization.

TestTransfer - contains a test methods. 

## About gRPC
gRPC - is a cross-platform open source high performance remote procedure call (RPC) framework that built on HTTP/2.
It's all true, though gRPC isn't a solution for every problem. Think about the gRPC as about an instrument that you can use to solve some specific problems. 

gRPC pros:
- Built on HTTP/2, sending the binary data makes it faster (throughput) than all the HTTP/1 approaches. 
- Protobufs are provide a strictness to requests, that prevents you from loosing your traffic. Traffic loss often can happen in a large projects.
- HTTP/2 streams are fast, simple and reliable when the messages order is required.
- Multiplexing. gRPC allows multiple requests and responses to be sent concurrently over a single connection.
- HTTP/2 won't work without a TLS, its forces us to use the TLS certificates, that makes it safer.

gRPC cons:
- HTTP/2 difficult to be used from Browser apps, you need an additional layer that will convert HTTP/1 to HTTP/2. 
- gRPC features works only with HTTP/2 protocol.
- gRPC is more complex and less known as for example REST. gRPC requires additional infrastructure for both clients and servers.
- HTTP/2 won't work without a TLS, that forces us to take care of TLS certificate configuration.
- Protobuf messages are not human-readable, that makes it difficult to debug. 
- Interoperability. gRPC supports Named Pipes, but only from .NET 8.0. Using gRPC without HTTP/2, obviously, will bypass all the HTTP/2 features (streams and multiplexing).

## When to use gRPC? 
- gRPC is fair to be used in microservices architecture, especially, in multi-languages complex systems where strictness is required. 
- gRPC streams are a handful alternative for Websockets or WCF, gRPC's bi-directional streaming often considered better than WCF communication and it has its facilities over the Websockets, though Websockets still a useful solution for streaming tasks. 
- If your app requires a high troughput of a huge data messages, gRPC's binary data encryption is a suitable choice for this case.

## Protobuf
Protobufs are like the Interfaces for classes in OOP. 

Each Protobuf file represents the distinct Service on the gRPC Server side.

gRPC library does generates a code from your specified Protobufs. 
gRPC Library generates the whole objects and the namespaces for them. For example, if your Protobuf name is "Transfer", then Transfer.TrasferClient and Transfer.TransferBase objects will be generated for you.

To use a Protobuf you must create a ProtoBufNameService.cs file and inherit it from the Base generated object as it shown in TransferService.cs. 
Then you can "Map" your Protobuf service to a project routes in the same way as it shown in GrpcService Program.cs

Protobuf specification is quiet simple, take a look at the transfer.proto.
Firstly, you must specify the protobuf syntax, our examples uses "proto3". 
Secondly, you can see a "package" keyword, that's how we create a namespace for our proto. 
At last we came to create a "Transfer" Protobuf, definition looks the same as any interface. Protobuf should start with a key "service" and each its method must return an "rpc" type. 
In case of Transfer you can see 2 methods are specified "UploadFile" and "DownloadFile", take a look at their arguments fields, here's a "stream" keyword, that's how we define Client stream, Server stream or Bi-directional stream. 
In this case UploadFile is a Client Stream and DownloadFile is a Server stream.

Protobuf requires messages to be defined as the request/response attributes. There's nothing complex in the messages structure, consider the transfer.proto example.
Numbers like "FileMetadata metadata = 1" and "bytes data = 2" are just an order of a fields specification, these numbers are used for the fields identification. You can set that the field "data" is the first field and the "metadata" is a second one, these numbers isn't so meaningful for developers, but are crucial for Protobufs.

Read a Protobuf documentation to learn more: 
- https://protobuf.dev/overview/
- https://learn.microsoft.com/en-us/aspnet/core/grpc/protobuf?view=aspnetcore-6.0

## Interceptors 
Use Interceptors if you need kind of a middleware agent. 
For example, Interceptors can be used to accomplish the following tasks:
- logging
- authentication and authorization
- exception handling
- request and response transformation
- performance monitoring
- caching

You can setup as much interceptors as your applicaiton needed. There's no limit on amount of interceptors to configure.
You can setup Client side and Server side interceptors.

https://learn.microsoft.com/en-us/aspnet/core/grpc/interceptors?view=aspnetcore-6.0

## Performance 
Consider performance best practices with gRPC: 
https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0

Pay attention to [gRPC services and large binary payloads](https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#grpc-services-and-large-binary-payloads)
**gRPC is a message-based RPC framework, which means:**
- The entire message is loaded into memory before gRPC can send it.
- When the message is received, the entire message is deserialized into memory.

Additional information:
- [gRPC vs Websocket](https://www.frontendmag.com/insights/grpc-vs-websocket/)
- [gRPC vs REST](https://www.baeldung.com/rest-vs-grpc)
- [gRPC vs WCF](https://github.com/dotnet-architecture/eBooks/blob/1ed30275281b9060964fcb2a4c363fe7797fe3f3/current/grpc-for-wcf-developers/gRPC-for-WCF-Developers.pdf)

## Getting started
Protos is a shared folder between Client and Server. Each file in there must be shared between Client and Server, so pay attention.

TestTransfer methods require a "bigdata.txt" file in root folder of both Client and Server. 

Configure the GrpcService url for client in appsettings.json

gRPC requires TLS certificate, ensure that you configured the right one in GrpcService appsettings.json Kestrel configuration.
Make sure that you use HTTPS to communicate with gRPC Server.

## Settings
- Uncomment the LoggerFactory in "CreateChannel" method at GrpcClient if you want to see the gRPC communication flow
- Uncomment the "Grpc" setting in appsettings.json if you want to view a gRPC flow (See more about logging and diagnostics https://learn.microsoft.com/en-us/aspnet/core/grpc/diagnostics?view=aspnetcore-6.0)
- Increase/Decrease MaxReceiveMessageSize for both Client and Server regarding the files size that you intent to transfer.
- Increase/Decrease InitialConnectionWindowSize and InitialStreamWindowSize settings on GrpcService side regarding the chunk size that you intent to transfer with stream.
 
## Useful examples
https://github.com/AwesomeYuer/grpc-dotnet-examples