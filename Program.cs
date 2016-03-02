using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


class Program
{
    static void test_v1(string host, UInt16 port)
    {
        var wait_response = true;

        KcpProject.v1.UdpSocket client = null;

        client = new KcpProject.v1.UdpSocket((KcpProject.v1.UdpSocket.cliEvent ev, byte[] buf, string err) =>
        {
            wait_response = false;

            switch (ev)
            { 
                case KcpProject.v1.UdpSocket.cliEvent.Connected:
                    Console.WriteLine("connected.");
                    client.Send("Hello KCP.");
                    break;
                case KcpProject.v1.UdpSocket.cliEvent.ConnectFailed:
                    Console.WriteLine("connect failed. {0}", err);
                    break;
                case KcpProject.v1.UdpSocket.cliEvent.Disconnect:
                    Console.WriteLine("disconnect. {0}", err);
                    break;
                case KcpProject.v1.UdpSocket.cliEvent.RcvMsg:
                    Console.WriteLine("recv message: {0}", System.Text.ASCIIEncoding.ASCII.GetString(buf) );
                    break;
            }
        });

        client.Connect(host, port);

        while (wait_response)
        {
            client.Update();
            System.Threading.Thread.Sleep(10);
        }
    }

    static void test_v2(string host, UInt16 port)
    {
        var wait_response = true;

        KcpProject.v2.UdpSocket client = null;

        // 创建一个实例
        client = new KcpProject.v2.UdpSocket((byte[] buf) => 
        {
            wait_response = false;
            Console.WriteLine("recv message: {0}", System.Text.ASCIIEncoding.ASCII.GetString(buf));
        });

        // 绑定端口
        client.Connect(host, port);

        // 发送消息
        client.Send("Hello KCP.");

        // update.
        while (wait_response)
        {
            client.Update();
            System.Threading.Thread.Sleep(10);
        }
    }

    static void Main(string[] args)
    {
       // 测试v1版本，有握手过程，服务器决定conv的分配
       //test_v1("192.168.1.2", 4444);

       //Console.WriteLine("**********************************************************");

       // 测试v2版本，没有握手过程，客户端自行决定conv的分配
       // 适合配合 kcp-go
        test_v2("192.168.1.2", 4455);
    }
}

