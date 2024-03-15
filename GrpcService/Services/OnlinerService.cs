using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Online;
using System;

namespace GrpcService.Services
{
    // You can use a Base generated class to inherite from. In this case the base generated class is a OnlinerBase
    public class OnlinerService : Onliner.OnlinerBase
    {
        //Use random deny to test retry attempts from client
        //private readonly Random _random = new Random();

        private readonly ILogger<OnlinerService> _logger;
        public OnlinerService(ILogger<OnlinerService> logger)
        {
            _logger = logger;
        }

        //CheckOnline method is prepared from our Protobuf and generated, we only need to override it. 
        public override Task<HelloReply> CheckOnline(Empty request, ServerCallContext context) 
        {
            //Use random deny to test retry attempts from client
            /*const double deliveryChance = 0.5;
            if (_random.NextDouble() > deliveryChance)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, $"- {request.Name}"));
            }*/

            return Task.FromResult(new HelloReply
            {
                Message = "Hello World"
            });
        }
    }
}
