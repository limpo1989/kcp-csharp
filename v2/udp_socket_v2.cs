using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace KcpProject.v2
{
    // 客户端随机生成conv并作为后续与服务器通信
    public class UdpSocket
    {
        private static readonly DateTime utc_time = new DateTime(1970, 1, 1);

        public static UInt32 iclock()
        {
            return (UInt32)(Convert.ToInt64(DateTime.UtcNow.Subtract(utc_time).TotalMilliseconds) & 0xffffffff);
        }

        private UdpClient mUdpClient;
        private IPEndPoint mIPEndPoint;
        private IPEndPoint mSvrEndPoint;
        private Action<byte[]> evHandler;
        private KCP mKcp;
        private bool mNeedUpdateFlag;
        private UInt32 mNextUpdateTime;

        private SwitchQueue<byte[]> mRecvQueue = new SwitchQueue<byte[]>(128);

        public UdpSocket(Action<byte[]> handler)
        {
            evHandler = handler;
        }

        public void Connect(string host, UInt16 port)
        {
            mSvrEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
            mUdpClient = new UdpClient(host, port);
            mUdpClient.Connect(mSvrEndPoint);

            init_kcp((UInt32)new Random((int)DateTime.Now.Ticks).Next(1, Int32.MaxValue));

            mUdpClient.BeginReceive(ReceiveCallback, this);
        }

        void init_kcp(UInt32 conv)
        {
            mKcp = new KCP(conv, (byte[] buf, int size) =>
            {
                mUdpClient.Send(buf, size);
            });

            // fast mode.
            mKcp.NoDelay(1, 10, 2, 1);
            mKcp.WndSize(128, 128);
        }

        void ReceiveCallback(IAsyncResult ar)
        {
            Byte[] data = (mIPEndPoint == null) ?
                mUdpClient.Receive(ref mIPEndPoint) :
                mUdpClient.EndReceive(ar, ref mIPEndPoint);

            if (null != data)
            {
                // push udp packet to switch queue.
                mRecvQueue.Push(data);
            }

            if (mUdpClient != null)
            {
                // try to receive again.
                mUdpClient.BeginReceive(ReceiveCallback, this);
            }
        } 

        public void Send(byte[] buf)
        {
            mKcp.Send(buf);
            mNeedUpdateFlag = true;
        }

        public void Send(string str)
        {
            Send(System.Text.ASCIIEncoding.ASCII.GetBytes(str));
        }

        public void Update()
        {
            update(iclock());
        }

        public void Close()
        {
            mUdpClient.Close();
        }

        void process_recv_queue()
        {
            mRecvQueue.Switch();

            while (!mRecvQueue.Empty())
            {
                var buf = mRecvQueue.Pop();

                mKcp.Input(buf);
                mNeedUpdateFlag = true;

                for (var size = mKcp.PeekSize(); size > 0; size = mKcp.PeekSize())
                {
                    var buffer = new byte[size];
                    if (mKcp.Recv(buffer) > 0)
                    {
                        evHandler(buffer);
                    }
                }
            }
        }

        void update(UInt32 current)
        {
            process_recv_queue();

            if (mNeedUpdateFlag || current >= mNextUpdateTime)
            {
                mKcp.Update(current);
                mNextUpdateTime = mKcp.Check(current);
                mNeedUpdateFlag = false;
            }
        }
    }
}
