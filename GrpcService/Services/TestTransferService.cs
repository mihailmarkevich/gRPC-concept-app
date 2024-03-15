using Google.Protobuf;
using Grpc.Core;
using Tests;

namespace GrpcService.Services
{
    public class TestTransferService : TestTransfer.TestTransferBase
    {
        private readonly ILogger<OnlinerService> _logger;
        private readonly IConfiguration _config;

        public TestTransferService(ILogger<OnlinerService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public override Task<TransferMessage> TransferUnaryTest(TransferMessage request, ServerCallContext context)
        {
            /*Console.WriteLine("Message received");
            Console.WriteLine(request.Text);*/

            return Task.FromResult(new TransferMessage
            {
                Text = String.Concat("Message received: ", request.Text),
            });
        }

        public override async Task TransferDuplexStreamTest(IAsyncStreamReader<TransferMessage> requestStream, IServerStreamWriter<TransferMessage> responseStream, ServerCallContext context)
        {
            try
            {
                //The loop will continue until the client closes the stream.
                await foreach (var requetsMessage in requestStream.ReadAllAsync())
                {
                    /*Console.WriteLine("Message Received");
                    Console.WriteLine(requetsMessage.Text);*/

                    await responseStream.WriteAsync(new TransferMessage
                    {
                        Text = String.Concat("Message received: ", requetsMessage.Text),
                    });
                }
            }
            catch (Exception ex)
            {
                //The stream is probably closed by client
            }
        }

        public override Task<UploadFileResponse> UploadUnaryTest(UploadFileRequest request, ServerCallContext context)
        {
            //Console.WriteLine("File received");

            /*byte[] fileContent = request.Data.ToByteArray();
            Console.WriteLine(Encoding.UTF8.GetString(fileContent));*/

            return Task.FromResult(new UploadFileResponse());
        }

        public override Task<DownloadFileResponse> DownloadUnaryTest(DownloadFileRequest request, ServerCallContext context)
        {
            string filepath = Path.Combine(_config["StoredFilesPath"], "bigdata.txt");

            byte[] fileContent = File.ReadAllBytes(filepath);

            return Task.FromResult(new DownloadFileResponse
            {
                Data = UnsafeByteOperations.UnsafeWrap(fileContent)
            });
        }

        public override async Task<UploadFileResponse> UploadStreamTest(IAsyncStreamReader<UploadFileRequest> requestStream, ServerCallContext context)
        {
            string fileContent = string.Empty;

            //The loop will continue until the client closes the stream.
            await foreach (var message in requestStream.ReadAllAsync())
            {
                //byte[] bytes = message.Data.ToByteArray();
                //fileContent += Encoding.UTF8.GetString(bytes);
            }

            //Console.WriteLine("File received");

            //Console.WriteLine(fileContent);

            return new UploadFileResponse();
        }

        public override async Task DownloadStreamTest(DownloadFileRequest request, IServerStreamWriter<DownloadFileResponse> responseStream, ServerCallContext context)
        {
            const int ChunkSize = 1024 * 1024 * 2; // 2 MB

            string filepath = Path.Combine(_config["StoredFilesPath"], "bigdata.txt");

            var buffer = new byte[ChunkSize];

            await using var fileStream = File.OpenRead(filepath);

            while (true)
            {
                //Continue sending the file piece by piece until the end of the file's data
                var numBytesRead = await fileStream.ReadAsync(buffer);
                if (numBytesRead == 0)
                    break;

                await responseStream.WriteAsync(new DownloadFileResponse
                {
                    Data = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, numBytesRead))
                });
            }
        }

        public override async Task UploadDuplexStreamTest(IAsyncStreamReader<UploadFileRequest> requestStream, IServerStreamWriter<UploadFileResponse> responseStream, ServerCallContext context)
        {
            try
            {
                string fileContent = string.Empty;

                //The loop will continue until the client closes the stream.
                await foreach (var requestMessage in requestStream.ReadAllAsync())
                {
                    if (requestMessage.Metadata != null)
                    {
                        if (requestMessage.Metadata.Start)
                        {
                            //Console.WriteLine("New file came");

                            //fileContent = string.Empty;
                        }

                        if (requestMessage.Metadata.End)
                        {
                            //Console.WriteLine("File received fully");
                            //Console.WriteLine(fileContent);

                            await responseStream.WriteAsync(new UploadFileResponse());
                        }
                    }

                    if (requestMessage.Data != null)
                    {
                        /*byte[] bytes = requestMessage.Data.ToByteArray();
                        fileContent += Encoding.UTF8.GetString(bytes);*/
                    }
                }
            }
            catch (Exception ex)
            {
                //The stream is probably closed by client
            }
        }

        public override async Task UploadNoChunkDuplexStreamTest(IAsyncStreamReader<UploadFileRequest> requestStream, IServerStreamWriter<UploadFileResponse> responseStream, ServerCallContext context)
        {
            try
            {
                await foreach (var requestMessage in requestStream.ReadAllAsync())
                {
                    /*Console.WriteLine("New file came");
                    byte[] bytes = requestMessage.Data.ToByteArray();
                    string fileContent = Encoding.UTF8.GetString(bytes);
                    Console.WriteLine("File received fully");
                    Console.WriteLine(fileContent);*/

                    await responseStream.WriteAsync(new UploadFileResponse());
                }

            }
            catch (Exception ex)
            {
                //The stream is probably closed by client
            }
        }

    }
}
