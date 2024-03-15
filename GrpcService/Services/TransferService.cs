using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Text;
using System.Text.Json;
using Transfering;

namespace GrpcService.Services
{
    // You can use a Base generated class to inherite from. In this case the base generated class is a TransferBase
    public class TransferService : Transfer.TransferBase
    {
        private readonly ILogger<OnlinerService> _logger;
        private readonly IConfiguration _config;

        private const int ChunkSize = 1024 * 32; // 32 KB

        public TransferService(ILogger<OnlinerService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        //UploadFile method is prepared from our Protobuf and generated, we only need to override it. 
        [Authorize]
        public override async Task<UploadFileResponse> UploadFile(IAsyncStreamReader<UploadFileRequest> requestStream, ServerCallContext context)
        {
            string metadata = String.Empty;

            var uploadId = Path.GetRandomFileName();
            var uploadPath = Path.Combine(_config["StoredFilesPath"], uploadId);
            
            Directory.CreateDirectory(uploadPath);
            
            string filePath = Path.Combine(uploadPath, "data.bin");

            await using(var writeStream = File.Create(filePath)){
                //The loop will continue until the client closes the stream.
                await foreach (var message in requestStream.ReadAllAsync())
                {
                    if (message.Metadata != null)
                    {
                        metadata = message.Metadata.ToString();

                        await File.WriteAllTextAsync(Path.Combine(uploadPath, "metadata.json"), metadata);
                    }
                    if (message.Data != null)
                    {
                        await writeStream.WriteAsync(message.Data.Memory);
                    }
                }

            }

            //Convert data.bin into a sent type
            //We won't use the complex type conversion for our example project
            Dictionary<string, string> metadataDict = JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
            string filename = metadataDict?["fileName"] ?? string.Empty;
            if (!string.IsNullOrEmpty(filename))
            {
                string newPath = Path.Combine(_config["StoredFilesPath"], filename);
                File.Move(filePath, newPath);
                //Delete not needed temp upload path
                Directory.Delete(uploadPath, true);
            }

            return new UploadFileResponse { Id = uploadId };
        }

        //DownloadFile method is prepared from our Protobuf and generated, we only need to override it. 
        [Authorize]
        public override async Task DownloadFile(DownloadFileRequest request, IServerStreamWriter<DownloadFileResponse> responseStream, ServerCallContext context)
        {
            string filepath = Path.Combine(_config["StoredFilesPath"], request.FileName);

            if (!File.Exists(filepath))
            {
                //Read more about Error Handling with gRPC https://learn.microsoft.com/en-us/aspnet/core/grpc/error-handling?view=aspnetcore-6.0
                throw new RpcException(new Status(StatusCode.NotFound, $"File {request.FileName} isn't found on the server."));
            } 

            await responseStream.WriteAsync(new DownloadFileResponse
            {
                Metadata = new FileMetadata { FileName = request.FileName },
            });

            var buffer = new byte[ChunkSize];
            await using var fileStream = File.OpenRead(filepath);

            while (true)
            {
                //Continue sending the file piece by piece until the end of the file's data
                var numBytesRead = await fileStream.ReadAsync(buffer);
                if (numBytesRead == 0)
                    break;

                _logger.LogInformation("Sending data chunk of {numBytesRead} bytes", numBytesRead);
                await responseStream.WriteAsync(new DownloadFileResponse
                {
                    Data = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, numBytesRead)) // UnsafeWrap avoid additional allocations, see https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#send-binary-payloads
                });
            }
        }
        

    }
}
