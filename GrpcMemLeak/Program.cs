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
            TestMemLeakAsync().Wait();
        }

        static async Task TestMemLeakAsync()
        {
            var channel = new Channel("192.168.2.13", 2379, ChannelCredentials.Insecure);
            var leaseClient = new Lease.LeaseClient(channel);

            Console.WriteLine("Start");
            Console.ReadLine();

            for (int i = 0; i < 100; i++)
            {
                var leaseGrantReq = new LeaseGrantRequest()
                {
                    TTL = 100,
                };
                var leaseGrantRes = await leaseClient.LeaseGrantAsync(leaseGrantReq, cancellationToken: channel.ShutdownToken);

                using (var leaser = leaseClient.LeaseKeepAlive(cancellationToken: channel.ShutdownToken))
                {
                    var leaseKeepAliveReq = new LeaseKeepAliveRequest() { ID = leaseGrantRes.ID };
                    await leaser.RequestStream.WriteAsync(leaseKeepAliveReq);
                    await leaser.RequestStream.CompleteAsync();

                    while (await leaser.ResponseStream.MoveNext(channel.ShutdownToken))
                    {
                        var leaseKeepAliveRes = leaser.ResponseStream.Current;
                        if (leaseKeepAliveRes.ID == leaseKeepAliveReq.ID)
                            break;
                    }
                }
            }

            Console.WriteLine("Done");
            Console.ReadLine();

            GC.Collect();
            Console.WriteLine("GC");
            Console.ReadLine();

            await channel.ShutdownAsync();
        }
    }
}
