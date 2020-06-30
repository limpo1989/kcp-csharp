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
            connection.Connect("127.0.0.1", 4444);

            var firstSend = true;
            var buffer = new byte[1024];
            var counter = 0;

            while (true) {
                connection.Update();

                if (firstSend)  {
                    //firstSend = false;
                    // Console.WriteLine("Write Message...");
                    var text = Encoding.UTF8.GetBytes(string.Format("Hello KCP: {0}", ++counter));
                    if (connection.Send(text, 0, text.Length) < 0) {
                        Console.WriteLine("Write message failed.");
                        break;
                    }
                }

                var n = connection.Recv(buffer, 0, buffer.Length);
                if (n == 0) {
                    Thread.Sleep(10);
                    continue;
                } else if (n < 0) {
                    Console.WriteLine("Receive Message failed.");
                    break;
                }

                var resp = Encoding.UTF8.GetString(buffer, 0, n);
                Console.WriteLine("Received Message: " + resp);


            }
        }
    }
}
