using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


class Program
{
    static void Main(string[] args)
    {
        bool beginSend = false;

        var udp = new UdpSocket((UdpSocket.cliEvent ev, byte[] buf, string err) => 
        {
            Console.WriteLine("{0} => {1} bytes, err => {2}", ev, buf != null ? buf.Length : 0, err);

            if (UdpSocket.cliEvent.Connected == ev)
            {
                beginSend = true;
            }
        });

        udp.Connect("192.168.1.2", 4444);


        while (true)
        {
            udp.Update();
            System.Threading.Thread.Sleep(50);

            if (beginSend) udp.Send("KCP");
        }
    }
}

