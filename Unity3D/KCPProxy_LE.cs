using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Network_Kcp
{
    
    public class KCPProxy_LE
    {
        #region 工具函数
        public static IPEndPoint IPEP_Any = new IPEndPoint(IPAddress.Any, 0);
        public static IPEndPoint IPEP_IPv6Any = new IPEndPoint(IPAddress.IPv6Any, 0);
        public static IPEndPoint GetIPEndPointAny(AddressFamily family, int port) {
            if (family == AddressFamily.InterNetwork) {
                if (port == 0) {
                    return IPEP_Any;
                }

                return new IPEndPoint(IPAddress.Any, port);
            }
            else if (family == AddressFamily.InterNetworkV6) {
                if (port == 0) {
                    return IPEP_IPv6Any;
                }

                return new IPEndPoint(IPAddress.IPv6Any, port);
            }
            return null;
        }


        private static readonly DateTime UTCTimeBegin = new DateTime(1970, 1, 1);

        public static long GetClockMS() {
            return (Convert.ToInt64(DateTime.UtcNow.Subtract(UTCTimeBegin).TotalMilliseconds) & 0xffffffff);
        }

        

        #endregion


        private KCP_LE m_Kcp;
        //private KCP_BE  m_Kcp;
        private bool m_NeedKcpUpdateFlag = false;
        private long m_NextKcpUpdateTime = 0;
        private SwitchQueue<byte[]> m_RecvQueue = new SwitchQueue<byte[]>(128);

        private IPEndPoint m_RemotePoint;
        private Socket m_Socket;
        private KCPReceiveListener m_Listener;

        public IPEndPoint RemotePoint { get { return m_RemotePoint; } }



        public KCPProxy_LE(uint key, IPEndPoint remotePoint, Socket socket) {
            m_Socket = socket;
            m_RemotePoint = remotePoint;

            m_Kcp = new KCP_LE(key, HandleKcpSend);
            //m_Kcp = new KCP_BE(key, HandleKcpSend);
            m_Kcp.NoDelay(1, 10, 2, 1);
            m_Kcp.WndSize(128, 128);

        }

        public void Dispose() {
            m_Socket = null;

            if (m_Kcp != null) {
                m_Kcp.Dispose();
                m_Kcp = null;
            }

            m_Listener = null;
        }

        //---------------------------------------------
        private void HandleKcpSend(byte[] buff, int size) {
            //KCP输出回调
            if (m_Socket != null) {
                m_Socket.SendTo(buff, 0, size, SocketFlags.None, m_RemotePoint);
            }
        }

        private void HandleKcpSend_Hook(byte[] buff, int size) {
            if (m_Socket != null) {
                m_Socket.SendTo(buff, 0, size, SocketFlags.None, m_RemotePoint);
            }
        }

        public bool DoSend(byte[] buff, int size) {
            m_NeedKcpUpdateFlag = true;
            byte[] dst = new byte[size];
            Buffer.BlockCopy(buff, 0, dst, 0, size);
            return m_Kcp.Send(dst) >= 0;
        }
        public bool DoSend(byte[] buff) {
            m_NeedKcpUpdateFlag = true;
            return m_Kcp.Send(buff) >= 0;
        }
        //---------------------------------------------

        public void AddReceiveListener(KCPReceiveListener listener) {
            m_Listener += listener;
        }

        public void RemoveReceiveListener(KCPReceiveListener listener) {
            m_Listener -= listener;
        }



        public void DoReceiveInThread(byte[] buffer, int size) {
            byte[] dst = new byte[size];
            Buffer.BlockCopy(buffer, 0, dst, 0, size);
            m_RecvQueue.Push(dst);
        }

        private void HandleRecvQueue() {
            m_RecvQueue.Switch();
            while (!m_RecvQueue.Empty()) {
                var recvBufferRaw = m_RecvQueue.Pop();
                int ret = m_Kcp.Input(recvBufferRaw);

                //收到的不是一个正确的KCP包
                if (ret < 0) {
                    if (m_Listener != null) {
                        m_Listener(recvBufferRaw, recvBufferRaw.Length, m_RemotePoint);
                    }
                    return;
                }

                m_NeedKcpUpdateFlag = true;

                for (int size = m_Kcp.PeekSize(); size > 0; size = m_Kcp.PeekSize()) {
                    var recvBuffer = new byte[size];
                    if (m_Kcp.Recv(recvBuffer) > 0) {
                        if (m_Listener != null) {
                            m_Listener(recvBuffer, size, m_RemotePoint);
                        }
                    }
                }
            }
        }

        //---------------------------------------------
        public void Update(long currentTimeMS) {
            HandleRecvQueue();

            if (m_NeedKcpUpdateFlag || currentTimeMS >= m_NextKcpUpdateTime) {
                m_Kcp.Update((uint)currentTimeMS);
                m_NextKcpUpdateTime = m_Kcp.Check((uint)currentTimeMS);
                m_NeedKcpUpdateFlag = false;
            }
        }

        //---------------------------------------------

    }
}
