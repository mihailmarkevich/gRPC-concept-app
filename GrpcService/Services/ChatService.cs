using Google.Protobuf;
using Grpc.Core;
using Chat;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;

namespace GrpcService.Services
{
    // You can use a Base generated class to inherite from. In this case the base generated class is a ChatterBase
    public class ChatService : Chatter.ChatterBase
    {
        private readonly ChatHub _chatHub; // _chatHub is required here as a Singleton service

        public ChatService(ChatHub chatHub)
        {
            _chatHub = chatHub ?? throw new ArgumentNullException(nameof(chatHub));
        }

        //Chat method is prepared from our Protobuf and generated, we only need to override it. 
        [Authorize]
        public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            //The loop will continue until the client closes the stream.
            await foreach (var requestMessage in requestStream.ReadAllAsync())
            {
                await _chatHub.HandleIncomingMessage(requestMessage, responseStream);
            }
        }

    }
}
