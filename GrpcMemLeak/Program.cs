using Etcdserverpb;
using Grpc.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcMemLeak
{
    class Program
    {
        static void Main(string[] args)
        {
            TestAsync().Wait();
        }

        static async Task TestAsync()
        {
            var channel = new Channel("192.168.2.13", 2379, ChannelCredentials.Insecure);
            var leaseClient = new Lease.LeaseClient(channel);

            Console.WriteLine("Start");
            Console.ReadLine();

            for (int i = 0; i < 100; i++)
            {
                using (var shutdown = CancellationTokenSource.CreateLinkedTokenSource(channel.ShutdownToken))
                {
                    var leaseGrantReq = new LeaseGrantRequest()
                    {
                        TTL = 100,
                    };
                    var leaseGrantRes = await leaseClient.LeaseGrantAsync(leaseGrantReq, cancellationToken: shutdown.Token);

                    using (var leaser = leaseClient.LeaseKeepAlive(cancellationToken: shutdown.Token))
                    {
                        var leaseKeepAliveReq = new LeaseKeepAliveRequest() { ID = leaseGrantRes.ID };
                        await leaser.RequestStream.WriteAsync(leaseKeepAliveReq);
                        await leaser.RequestStream.CompleteAsync();

                        while (await leaser.ResponseStream.MoveNext(shutdown.Token))
                        {
                            var leaseKeepAliveRes = leaser.ResponseStream.Current;
                            if (leaseKeepAliveRes.ID == leaseKeepAliveReq.ID)
                                break;
                        }
                    }
                }
            }

            Console.WriteLine("Done");
            Console.ReadLine();

            GC.Collect();
            Console.WriteLine("GC");
            Console.ReadLine();
        }
    }
}
