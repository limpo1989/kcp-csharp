using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class UdpSocket {

    private static readonly DateTime utc_time = new DateTime(1970, 1, 1);

    public static UInt32 iclock() {
        return (UInt32)(Convert.ToInt64(DateTime.UtcNow.Subtract(utc_time).TotalMilliseconds) & 0xffffffff);
    }

    public enum cliEvent { 
        Connected = 0,
        ConnectFailed = 1,
        Disconnect = 2,
        RcvMsg = 3,
    }

    private const UInt32 CONNECT_TIMEOUT = 5000;
    private const UInt32 RESEND_CONNECT = 500;

    private UdpClient mUdpClient;
    private IPEndPoint mIPEndPoint;
    private IPEndPoint mSvrEndPoint;
    private Action<cliEvent, byte[], string> evHandler;
    private KCP mKcp;
    private bool mNeedUpdateFlag;
    private UInt32 mNextUpdateTime;

    private bool mInConnectStage;
    private bool mConnectSucceed;
    private UInt32 mConnectStartTime;
    private UInt32 mLastSendConnectTime;

    private SwitchQueue<byte[]> mRecvQueue = new SwitchQueue<byte[]>(128);

    public UdpSocket(Action<cliEvent, byte[], string> handler)
    {
        evHandler = handler;
    }

    public void Connect(string host, UInt16 port)
    {
        mSvrEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
        mUdpClient = new UdpClient(host, port);
        mUdpClient.Connect(mSvrEndPoint);

        reset_state();
        
        mInConnectStage = true;
        mConnectStartTime = iclock();
        
        mUdpClient.BeginReceive(ReceiveCallback, this);
    }

    void ReceiveCallback(IAsyncResult ar)
    {
        Byte[] data = (mIPEndPoint == null) ?
            mUdpClient.Receive(ref mIPEndPoint) :
            mUdpClient.EndReceive(ar, ref mIPEndPoint);

        if (null != data)
            OnData(data);

        if (mUdpClient != null)
        {
            // try to receive again.
            mUdpClient.BeginReceive(ReceiveCallback, this);
        }
    }

    void OnData(byte[] buf)
    {
        mRecvQueue.Push(buf);
    }

    void reset_state()
    {
        mNeedUpdateFlag = false;
        mNextUpdateTime = 0;

        mInConnectStage = false;
        mConnectSucceed = false;
        mConnectStartTime = 0;
        mLastSendConnectTime = 0;
        mRecvQueue.Clear();
        mKcp = null;
    }

    string dump_bytes(byte[] buf, int size)
    {
        var sb = new StringBuilder(size * 2);
        for (var i = 0; i < size; i++)
        {
            sb.Append(buf[i]);
            sb.Append(" ");
        }
        return sb.ToString();
    }

    void init_kcp(UInt32 conv) 
    {
        mKcp = new KCP(conv, (byte[] buf, int size) => 
        {
            mUdpClient.Send(buf, size);
        });

        mKcp.NoDelay(1, 10, 2, 1);
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
        evHandler(cliEvent.Disconnect, null, "Closed");
    }

    void process_connect_packet()
    {
        mRecvQueue.Switch();

        if (!mRecvQueue.Empty())
        {
            var buf = mRecvQueue.Pop();
              
            UInt32 conv = 0;
            KCP.ikcp_decode32u(buf, 0, ref conv);

            if (conv <= 0)
                throw new Exception("inlvaid connect back packet");

            init_kcp(conv);

            mInConnectStage = false;
            mConnectSucceed = true;

            evHandler(cliEvent.Connected, null, null);
        }
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
                if (mKcp.Recv(buffer) > 0) {
                    evHandler(cliEvent.RcvMsg, buffer, null);
                }
            }
        }
    }

    bool connect_timeout(UInt32 current) 
    {
        return current - mConnectStartTime > CONNECT_TIMEOUT;
    }

    bool need_send_connect_packet(UInt32 current)
    {
        return current - mLastSendConnectTime > RESEND_CONNECT;
    }

    void update(UInt32 current)
    {
        if (mInConnectStage) 
        {
            if (connect_timeout(current))
            { 
                evHandler(cliEvent.ConnectFailed, null, "Timeout");
                mInConnectStage = false;
                return;
            }

            if (need_send_connect_packet(current))
            {
                mLastSendConnectTime = current;
                mUdpClient.Send(new byte[4]{0, 0, 0, 0}, 4);
            }

            process_connect_packet();

            return;
        }

        if (mConnectSucceed)
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