using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace KcpProject.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var connection = new UDPSession();
            connection.AckNoDelay = true;
            connection.WriteDelay = false;

            connection.Connect("127.0.0.1", 4444);

            var stopSend = false;
            var buffer = new byte[1500];
            var counter = 0;
            var sendBytes = 0;
            var recvBytes = 0;

            while (true) {
                connection.Update();

                if (!stopSend)  {
                    //firstSend = false;
                    // Console.WriteLine("Write Message...");
                    //var text = Encoding.UTF8.GetBytes(string.Format("Hello KCP: {0}", ++counter));
                    var sent = connection.Send(buffer, 0, buffer.Length);
                    if (sent < 0) {
                        Console.WriteLine("Write message failed.");
                        break;
                    }

                    if (sent > 0)
                    {
                        counter++;
                        sendBytes += buffer.Length;
                        if (counter >= 500)
                            stopSend = true;
                    }
                }

                var n = connection.Recv(buffer, 0, buffer.Length);
                if (n == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }
                else if (n < 0)
                {
                    Console.WriteLine("Receive Message failed.");
                    break;
                }
                else {
                    recvBytes += n;
                    Console.WriteLine($"{recvBytes} / {sendBytes}");
                }

                //var resp = Encoding.UTF8.GetString(buffer, 0, n);
                //Console.WriteLine("Received Message: " + resp);
            }
        }
    }
}
