using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests;

namespace GrpcClient
{
    /// <summary>
    /// Class holds the methods to run the speed measurement tests with a different approaches of gRPC usage
    /// </summary>
    public class Tests
    {
        private static string _TestText = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.";

        /// <summary>
        /// Speed measurement of messages exchange throughput between client and server, using the Unary gRPC requests
        /// </summary>
        public static async Task<long> TransferUnaryTest(GrpcChannel channel, int attempts)
        {
            var client = new TestTransfer.TestTransferClient(channel);

            int i = 0;

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (i < attempts) 
            {
                var call = client.TransferUnaryTestAsync(new TransferMessage
                {
                    Text = _TestText,
                });

                var response = await call;

                i++;
            }

            stopwatch.Stop();

            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Speed measurement of messages exchange throughput between client and server, using the bi-directional gRPC stream
        /// </summary>
        public static async Task<long> TransferDuplexStreamTest(GrpcChannel channel, int attempts)
        {
            var client = new TestTransfer.TestTransferClient(channel);

            int i = 0;

            // Create duplex chat rpc stream
            using var duplexStream = client.TransferDuplexStreamTest();

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (i < attempts)
            {
                await duplexStream.RequestStream.WriteAsync(new TransferMessage
                {
                    Text = _TestText,
                });

                var response = await duplexStream.ResponseStream.MoveNext();

                /*if (response)
                {
                    Console.WriteLine("Message received");
                }*/

                i++;
            }

            stopwatch.Stop();

            await duplexStream.RequestStream.CompleteAsync(); //Stream is closed by the Client

            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Speed measurement of upload bigdata file, using the Unary gRPC request
        /// </summary>
        public static async Task<long> UploadUnaryTest(GrpcChannel channel)
        {
            string filepath = "bigdata.txt";

            byte[] fileContent = File.ReadAllBytes(filepath);

            //Start speed measurement
            Stopwatch stopwatch = Stopwatch.StartNew();

            var client = new TestTransfer.TestTransferClient(channel); //Use the same channel on different clients

            var call = client.UploadUnaryTestAsync(new UploadFileRequest
            {
                Data = UnsafeByteOperations.UnsafeWrap(fileContent),
            });

            var response = await call;

            //Stop speed measurement
            stopwatch.Stop();

            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Speed measurement of download bigdata file, using the Unary gRPC request
        /// </summary>
        public static async Task<long> DownloadUnaryTest(GrpcChannel channel)
        {
            //Start speed measurement
            Stopwatch stopwatch = Stopwatch.StartNew();

            var client = new TestTransfer.TestTransferClient(channel);

            var call = client.DownloadUnaryTestAsync(new DownloadFileRequest());

            var response = await call;

            //Console.WriteLine("File received");
            //byte[] fileContent = response.Data.ToByteArray();
            //Console.WriteLine(Encoding.UTF8.GetString(fileContent));

            //Stop speed measurement
            stopwatch.Stop();

            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Speed measurement of upload bigdata file in chunks, using the Client gRPC stream
        /// </summary>
        public static async Task<long> UploadStreamTest(GrpcChannel channel)
        {
            const int ChunkSize = 1024 * 1024 * 2; // 2 MB

            string filepath = "bigdata.txt";

            var buffer = new byte[ChunkSize];

            await using var readStream = File.OpenRead(filepath);

            //Start speed measurement
            Stopwatch stopwatch = Stopwatch.StartNew();

            var client = new TestTransfer.TestTransferClient(channel);

            var call = client.UploadStreamTest();

            while (true)
            {
                var count = await readStream.ReadAsync(buffer);
                if (count == 0)
                    break;

                //Console.WriteLine("Sending file data chunk of length " + count);

                //Sending a file piece by piece
                //gRPC guarantees message ordering within an individual RPC call
                await call.RequestStream.WriteAsync(new UploadFileRequest
                {
                    Data = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, count))
                });
            }

            //Console.WriteLine("Complete request");
            await call.RequestStream.CompleteAsync(); //Stream is closed by the Client

            var response = await call;
            //Console.WriteLine($"File {filepath} is successfully stored at the server side");

            //Stop speed measurement
            stopwatch.Stop();

            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Speed measurement of download bigdata file in chunks, using the Server gRPC stream
        /// </summary>
        public static async Task<long> DownloadStreamTest(GrpcChannel channel)
        {
            string fileContent = string.Empty;

            //Start speed measurement
            Stopwatch stopwatch = Stopwatch.StartNew();

            var client = new TestTransfer.TestTransferClient(channel);

            using var call = client.DownloadStreamTest(new DownloadFileRequest());

            //The loop will continue until the server closes the stream.
            await foreach (var message in call.ResponseStream.ReadAllAsync())
            {
                /*byte[] bytes = message.Data.ToByteArray();
                fileContent += Encoding.UTF8.GetString(bytes);*/
            }

            //Console.WriteLine("File received");
            //Console.WriteLine(fileContent);

            //Stop speed measurement
            stopwatch.Stop();

            return stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Speed measurement of upload bigdata file in chunks, using the bi-directional gRPC stream
        /// </summary>
        public static async Task<long> UploadDuplexStreamTest(GrpcChannel channel)
        {
            Stopwatch stopwatch = null;
            long elapsedMSs = 0;

            int i = 0;

            const int ChunkSize = 1024 * 1024 * 2; // 2 MB

            var client = new TestTransfer.TestTransferClient(channel);

            // Create duplex chat rpc stream
            using var duplexStream = client.UploadDuplexStreamTest();

            //Running only 2 iterations. We need to measure the speed of the 2nd one
            while (i < 2)
            {
                string filepath = "bigdata.txt";

                var buffer = new byte[ChunkSize];

                await using var readStream = File.OpenRead(filepath);

                //Starting the speed measurement only from a 2nd attempt to test a Bi-directional stream reuse approach
                if (i == 1)
                    stopwatch = Stopwatch.StartNew();

                //Send init message
                await duplexStream.RequestStream.WriteAsync(new UploadFileRequest
                {
                    Metadata = new FileMetadata()
                    {
                        Start = true,
                    }
                });

                while (true)
                {
                    var count = await readStream.ReadAsync(buffer);
                    if (count == 0)
                        break;

                    //Console.WriteLine("Sending file data chunk of length " + count);

                    //gRPC guarantees message ordering within an individual RPC call
                    await duplexStream.RequestStream.WriteAsync(new UploadFileRequest
                    {
                        Data = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, count))
                    });
                }

                //Console.WriteLine("All data sent");

                //Send complete message
                await duplexStream.RequestStream.WriteAsync(new UploadFileRequest
                {
                    Metadata = new FileMetadata
                    {
                        End = true,
                    }
                });

                // Wait for the server's response (FileStatus)
                var response = await duplexStream.ResponseStream.MoveNext();

                /*if (response)
                {
                    Console.WriteLine("Message successfully received by the server");
                }
                else
                {
                    Console.WriteLine("Something went wrong");
                }*/

                if (i == 1)
                {
                    //Stop speed measurement
                    stopwatch.Stop();
                    elapsedMSs = stopwatch.ElapsedMilliseconds;

                    await duplexStream.RequestStream.CompleteAsync(); //Stream is closed by the Client
                }

                i++;
            }

            return elapsedMSs;
        }

        /// <summary>
        /// Speed measurement of upload bigdata file, using the bi-directional gRPC stream
        /// </summary>
        public static async Task<long> UploadNoChunkDuplexStreamTest(GrpcChannel channel)
        {
            Stopwatch stopwatch = null;
            long elapsedMSs = 0;

            int i = 0;

            var client = new TestTransfer.TestTransferClient(channel);

            // Create duplex chat rpc stream
            using var duplexStream = client.UploadNoChunkDuplexStreamTest();

            //Running only 2 iterations. We need to measure the speed of the 2nd one
            while (i < 2)
            {
                string filepath = "bigdata.txt";

                byte[] fileContent = File.ReadAllBytes(filepath);

                //Starting the speed measurement only from a 2nd attempt to test a Bi-directional stream reuse approach
                if (i == 1)
                    stopwatch = Stopwatch.StartNew();

                //Sending a file in just a one request
                await duplexStream.RequestStream.WriteAsync(new UploadFileRequest
                {
                    Data = UnsafeByteOperations.UnsafeWrap(fileContent)
                });

                // Wait for the server's response (FileStatus)
                var response = await duplexStream.ResponseStream.MoveNext();

                /*if (response)
                {
                    Console.WriteLine("Message successfully received by the server");
                }*/

                if (i == 1)
                {
                    //Stop speed measurement
                    stopwatch.Stop();
                    elapsedMSs = stopwatch.ElapsedMilliseconds;

                    await duplexStream.RequestStream.CompleteAsync(); //Stream is closed by the Client
                }

                i++;
            }

            return elapsedMSs;
        }

    }
}
