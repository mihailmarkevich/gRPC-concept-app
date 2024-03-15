using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Online;
using Transfering;
using Chat;
using Google.Protobuf;
using System;
using System.Text.Json;
using System.Threading.Channels;
using System.Net.NetworkInformation;
using System.Web;
using System.Diagnostics;
using System.Text;
using System.Reflection.PortableExecutable;
using GrpcClient;
using Microsoft.Extensions.Configuration;

class Program
{
    private static string? _token = null;

    private static string? _server = null;

    static async Task Main()
    {
        var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        _server = config["AppSettings:GrpcService"];
        
        /*
         * Grpc Channel creation
         * New Grpc Channel can be created without any additional configurations as it shown below commented
         * From performance perspective, it's better to make only one channel and share it between clients
        */

        //using var channel = GrpcChannel.ForAddress(_server);
        using var channel = CreateChannel(); //Create channel with Retry policy and logging

        var exiting = false;

        int executions = 0;

        while (true)
        {
            if (executions == 0)
                Console.WriteLine("Hello, it's gRPC-Concept-App! How can I help you?");
            else
                Console.WriteLine("What you want to do next?");
            Console.WriteLine();
            Console.WriteLine("Press a key:");
            Console.WriteLine("1: Check online");
            Console.WriteLine("2: Authenticate");
            Console.WriteLine("3: Use file transfer");
            Console.WriteLine("4: Use live chat");
            Console.WriteLine("5: Run speed tests");
            Console.WriteLine("6: Exit");
            Console.WriteLine();

            var consoleKeyInfo = Console.ReadKey();

            switch (consoleKeyInfo.KeyChar)
            {
                case '1':
                    await CheckOnline(channel);
                    break;
                case '2':
                    _token = await Authenticate();
                    break;
                case '3':
                    await UseTransfer(channel);
                    break;
                case '4':
                    await UseLiveChat(channel);
                    break;
                case '5':
                    await RunSpeedTests(channel);
                    break;
                case '6':
                    exiting = true;
                    break;
            }

            executions++;

            if (exiting)
                break;
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    private static async Task CheckOnline(GrpcChannel channel)
    {
        //Use Client generated objects to communicate with a gRPC Server.
        //You can use the same channel to avoide costly extra channel creations
        var client = new Onliner.OnlinerClient(channel);

        try
        {
            var call = client.CheckOnlineAsync(new Empty());
            var response = await call;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Greeting: " + response.Message);
            Console.ResetColor();
            Console.Write(" " + await GetRetryCount(call.ResponseHeadersAsync)); // Uses response headers to count retry attempts. Setup them on the Service side as well
        }
        catch (RpcException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Status.Detail);
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Method calls to the distinct signle endpoint that returns the JWT token.
    /// </summary>
    private static async Task<string> Authenticate()
    {
        Console.WriteLine();
        Console.WriteLine($"Authenticating as {Environment.UserName}...");

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage
        {
            RequestUri = new Uri($"{_server}/auth?name={HttpUtility.UrlEncode(Environment.UserName)}"),
            Method = HttpMethod.Get,
            Version = new Version(2, 0)
        };

        using var tokenResponse = await httpClient.SendAsync(request);
        tokenResponse.EnsureSuccessStatusCode();

        var token = await tokenResponse.Content.ReadAsStringAsync();
        Console.WriteLine("Successfully authenticated.");

        return token;
    }

    #region Transfer
    /// <summary>
    /// Method gives a choice to use Uploader or Downloader functionality
    /// </summary>
    private static async Task UseTransfer(GrpcChannel channel)
    {
        Console.WriteLine();

        //TransferClient stores both functionalities, upload and download
        var client = new Transfer.TransferClient(channel); //Use the same channel on different clients

        Console.WriteLine("Press a key:");
        Console.WriteLine("1: Uploader");
        Console.WriteLine("2: Downloader");
        Console.WriteLine("3: Upload unary speed test");
        Console.WriteLine("4: Download unary speed test");
        Console.WriteLine("5: Upload stream speed test");
        Console.WriteLine("6: Download stream speed test");
        Console.WriteLine("7: Upload duplex stream speed test");
        Console.WriteLine("Press any other key to return to main menu");
        Console.WriteLine();

        var consoleKeyInfo = Console.ReadKey();

        switch (consoleKeyInfo.KeyChar)
        {
            case '1':
                await Upload(client);
                break;
            case '2':
                await Download(client);
                break;
        }
    }

    /// <summary>
    /// Upload any file on server using the gRPC Client Stream 
    /// </summary>
    private static async Task Upload(Transfer.TransferClient client)
    {
        Console.WriteLine();
        Console.WriteLine("Starting upload call");

        const int ChunkSize = 1024 * 32; // 32 KB

        try
        {
            //Auth
            Metadata? headers = null;
            if (_token != null)
            {
                headers = new Metadata();
                headers.Add("Authorization", $"Bearer {_token}");
            }

            var call = client.UploadFile(headers);

            Console.WriteLine("Write a file path");
            string filepath = Console.ReadLine();

            if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath))
            {
                Console.WriteLine("File not found");
                return;
            }

            Console.WriteLine("Write a desired name for file. Pay attention, don't mess up the file format");
            string filename = Console.ReadLine();

            Console.WriteLine("Sending file metadata");
            //Send a file metadata and initiate a Client Stream
            await call.RequestStream.WriteAsync(new UploadFileRequest
            {
                Metadata = new FileMetadata
                {
                    FileName = filename
                }
            });

            var buffer = new byte[ChunkSize];

            await using var readStream = File.OpenRead(filepath);

            while (true)
            {
                var count = await readStream.ReadAsync(buffer);
                if (count == 0)
                    break;

                Console.WriteLine("Sending file data chunk of length " + count);

                //Sending a file piece by piece
                //gRPC guarantees message ordering within an individual RPC call
                await call.RequestStream.WriteAsync(new UploadFileRequest
                {
                    Data = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, count)) // UnsafeWrap avoid additional allocations, see https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#send-binary-payloads
                });
            }

            Console.WriteLine("Complete request");
            await call.RequestStream.CompleteAsync(); //Stream is closed by the Client

            var response = await call;
            Console.WriteLine($"File {filename} is successfully stored at the server side");
        }
        catch (RpcException ex)
        {
            //Read more about Error Handling with gRPC https://learn.microsoft.com/en-us/aspnet/core/grpc/error-handling?view=aspnetcore-6.0
            Console.WriteLine("Status code: " + ex.Status.StatusCode);
            Console.WriteLine("Message: " + ex.Status.Detail);
        }
    }

    /// <summary>
    /// Download file from server using the gRPC Server Stream
    /// </summary>
    private static async Task Download(Transfer.TransferClient client)
    {
        Console.WriteLine();
        Console.WriteLine("Starting download call");

        var downloadsPath = Path.Combine(Environment.CurrentDirectory, "downloads"); //Files going to be stored in the bin folder
        var downloadId = Path.GetRandomFileName();
        var downloadIdPath = Path.Combine(downloadsPath, downloadId);
        Directory.CreateDirectory(downloadIdPath); // Prepare a temp dir for file

        Console.WriteLine("Write a filename to download from server");
        string input = Console.ReadLine();

        try
        {
            //Auth
            Metadata? headers = null;
            if (_token != null)
            {
                headers = new Metadata();
                headers.Add("Authorization", $"Bearer {_token}");
            }

            using var call = client.DownloadFile(new DownloadFileRequest
            {
                FileName = input
            }, headers);

            string metadata = String.Empty;

            string filePath = Path.Combine(downloadIdPath, "data.bin");

            await using (var writeStream = File.Create(filePath))
            {
                //The loop will continue until the server closes the stream.
                await foreach (var message in call.ResponseStream.ReadAllAsync())
                {
                    if (message.Metadata != null)
                    {
                        Console.WriteLine("Saving metadata to file");
                        metadata = message.Metadata.ToString();
                        await File.WriteAllTextAsync(Path.Combine(downloadIdPath, "metadata.json"), metadata);
                    }
                    if (message.Data != null)
                    {
                        var bytes = message.Data.Memory;
                        Console.WriteLine($"Saving {bytes.Length} bytes to file");
                        await writeStream.WriteAsync(bytes);
                    }
                }
            }

            //Convert data.bin into a sent type
            //We won't use the complex type conversion for our example project
            Dictionary<string, string> metadataDict = JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
            string filename = metadataDict?["fileName"] ?? string.Empty;
            string newPath = string.Empty;
            if (!string.IsNullOrEmpty(filename))
            {
                newPath = Path.Combine(downloadsPath, filename);
                File.Move(filePath, newPath); // Change file extension from .bin and move it out from the temp folder
                Directory.Delete(downloadIdPath, true); //Delete not needed temp upload path
            }

            Console.WriteLine();
            Console.WriteLine($"File successfully stored at {newPath}");
        }
        catch (RpcException ex)
        {
            //Read more about Error Handling with gRPC https://learn.microsoft.com/en-us/aspnet/core/grpc/error-handling?view=aspnetcore-6.0
            Console.WriteLine("Status code: " + ex.Status.StatusCode);
            Console.WriteLine("Message: " + ex.Status.Detail);
        }
    }

    #endregion

    /// <summary>
    /// Live chat using the gRPC Bi-direcitonal Stream
    /// </summary>
    private static async Task UseLiveChat(GrpcChannel channel)
    {
        Console.WriteLine();

        var client = new Chatter.ChatterClient(channel); //Use the same channel on different clients

        // Enter name
        Console.Write("Enter user name to join chat: ");
        string _userName = Console.ReadLine();

        try
        {
            //Auth
            Metadata? headers = null;
            if (_token != null)
            {
                headers = new Metadata();
                headers.Add("Authorization", $"Bearer {_token}");
            }

            // Create duplex chat rpc stream
            using var chatStream = client.Chat(headers);

            //Start reading stream from server in parallel thread
            var readTask = Task.Run(async () =>
            {
                await foreach (var message in chatStream.ResponseStream.ReadAllAsync())
                {
                    Console.WriteLine($"{message.User}: {message.Text}");
                }
            });

            // Send init-message to join the chat
            await chatStream.RequestStream.WriteAsync(new ChatMessage { User = _userName, Text = string.Empty });

            Console.WriteLine("Welcome to a LiveChat. \r\nType \"exit\" to leave the chat");

            // Run input loop
            while (true)
            {
                string input = Console.ReadLine();

                if (input.ToLower() == "exit")
                    break;

                await chatStream.RequestStream.WriteAsync(new ChatMessage { User = _userName, Text = input });
            }

            Console.WriteLine("You left the chat");
        }
        catch (RpcException ex)
        {
            if (ex.StatusCode == StatusCode.Unauthenticated)
            {
                Console.WriteLine("You aren't allowed to join the chat.");
                Console.WriteLine("Unauthenticated error.");
            }
            else
            {
                //Read more about Error Handling with gRPC https://learn.microsoft.com/en-us/aspnet/core/grpc/error-handling?view=aspnetcore-6.0
                Console.WriteLine("Status code: " + ex.Status.StatusCode);
                Console.WriteLine("Message: " + ex.Status.Detail);
            }
        }
    }

    private static async Task RunSpeedTests(GrpcChannel channel)
    {
        Console.WriteLine();

        int attempts = 100;

        long[] values = new long[attempts];

        int i = 0;

        //Messages exchange throughput tests
        while (i < attempts)
        {
            values[i] = await GrpcClient.Tests.TransferUnaryTest(channel, 100);
            i++;
        }
        Console.WriteLine($"Exchange of 100 message using the unary requests approximately takes {(long)values.Average()} milliseconds to execute");

        Array.Clear(values, 0, values.Length);
        i = 0;
        while (i < attempts)
        {
            values[i] = await GrpcClient.Tests.TransferDuplexStreamTest(channel, 100);
            i++;
        }
        Console.WriteLine($"Exchange of 100 message using the bi-directional stream approximately takes {(long)values.Average()} milliseconds to execute");

        //Upload tests
        Array.Clear(values, 0, values.Length);
        i = 0;
        while (i < attempts)
        {
            values[i] = await GrpcClient.Tests.UploadUnaryTest(channel);
            i++;
        }
        Console.WriteLine($"Upload unary approximately takes {(long)values.Average()} milliseconds to execute");

        Array.Clear(values, 0, values.Length);
        i = 0;
        while (i < attempts)
        {
            values[i] = await GrpcClient.Tests.DownloadUnaryTest(channel);
            i++;
        }
        Console.WriteLine($"Download unary approximately takes {(long)values.Average()} milliseconds to execute");

        Array.Clear(values, 0, values.Length);
        i = 0;
        while (i < attempts)
        {
            values[i] = await GrpcClient.Tests.UploadStreamTest(channel);
            i++;
        }
        Console.WriteLine($"Upload stream approximately takes {(long)values.Average()} milliseconds to execute");

        Array.Clear(values, 0, values.Length);
        i = 0;
        while (i < attempts)
        {
            values[i] = await GrpcClient.Tests.UploadUnaryTest(channel);
            i++;
        }
        Console.WriteLine($"Download stream approximately takes {(long)values.Average()} milliseconds to execute");

        Array.Clear(values, 0, values.Length);
        i = 0;
        while (i < attempts)
        {
            values[i] = await GrpcClient.Tests.UploadDuplexStreamTest(channel);
            i++;
        }
        Console.WriteLine($"Upload in bi-directional stream approximately takes {(long)values.Average()} milliseconds to execute");

        Array.Clear(values, 0, values.Length);
        i = 0;
        while (i < attempts)
        {
            values[i] = await GrpcClient.Tests.UploadNoChunkDuplexStreamTest(channel);
            i++;
        }
        Console.WriteLine($"Upload in bi-directional stream without a chunks approximately takes {(long)values.Average()} milliseconds to execute");

    }

    private static GrpcChannel CreateChannel()
    {
        //See the logging and diagnostics configuration https://learn.microsoft.com/en-us/aspnet/core/grpc/diagnostics?view=aspnetcore-6.0
        /*var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Debug);
        });*/

        var methodConfig = new MethodConfig
        {
            Names = { MethodName.Default },

            //See documentation: https://learn.microsoft.com/en-us/aspnet/core/grpc/retries?view=aspnetcore-6.0
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 5,
                InitialBackoff = TimeSpan.FromSeconds(0.5),
                MaxBackoff = TimeSpan.FromSeconds(0.5),
                BackoffMultiplier = 1,
                RetryableStatusCodes = { StatusCode.Unavailable }
            }
        };

        var handler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(10),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true,
        };

        // The port number must match the port of the gRPC server.
        return GrpcChannel.ForAddress(_server, new GrpcChannelOptions
        {
            // Add a debug logs about gRPC action. You can remove the logs to clear the console output
            //LoggerFactory = loggerFactory, 
            
            //Configure retry policty using the ServiceConfig
            ServiceConfig = new ServiceConfig { MethodConfigs = { methodConfig } },
            
            MaxReceiveMessageSize = 1024 * 1024 * 25, // 25 MB

            //Configure Keep alive pings for bi-dirrectional streams. Read https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#keep-alive-pings
            HttpHandler = handler,  
        });
    }

    /// <summary>
    /// Example of the response Headers usage
    /// Retrieves the retry attempts in case of failure
    /// See documentation: https://learn.microsoft.com/en-us/aspnet/core/grpc/retries?view=aspnetcore-6.0
    /// </summary>
    private static async Task<string> GetRetryCount(Task<Metadata> responseHeadersTask)
    {
        var headers = await responseHeadersTask;
        var previousAttemptCount = headers.GetValue("grpc-previous-rpc-attempts");
        return previousAttemptCount != null ? $"(retry count: {previousAttemptCount})" : string.Empty;
    }
       
}