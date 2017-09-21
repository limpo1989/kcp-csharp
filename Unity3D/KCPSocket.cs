//#define BigEndian
#define LittleEndian
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
#if BigEndian
using KCPProxy = Network_Kcp.KCPProxy_BE;
#else
using KCPProxy = Network_Kcp.KCPProxy_LE;
#endif
namespace Network_Kcp
{
    public class KCPSocket
    {



        public string LOG_TAG = "KCPSocket";

        private bool m_IsRunning = false;
        private Socket m_SystemSocket;
        private IPEndPoint m_LocalEndPoint;
        private AddressFamily m_AddrFamily;
        private Thread m_ThreadRecv;
        private byte[] m_RecvBufferTemp = new byte[4096];

        //KCP参数
        private List<KCPProxy> m_ListKcp;
        private uint m_KcpKey = 0;
        private KCPReceiveListener m_AnyEPListener;

        //=================================================================================
        #region 构造和析构

        public KCPSocket(int bindPort, uint kcpKey, AddressFamily family = AddressFamily.InterNetwork) {
            m_AddrFamily = family;
            m_KcpKey = kcpKey;
            m_ListKcp = new List<KCPProxy>();

            m_SystemSocket = new Socket(m_AddrFamily, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ipep = KCPProxy.GetIPEndPointAny(m_AddrFamily, bindPort);
            m_SystemSocket.Bind(ipep);

            bindPort = (m_SystemSocket.LocalEndPoint as IPEndPoint).Port;
            LOG_TAG = "KCPSocket[" + bindPort + "-" + kcpKey + "]";

            m_IsRunning = true;
            m_ThreadRecv = new Thread(Thread_Recv) { IsBackground = true };
            m_ThreadRecv.Start();



#if UNITY_EDITOR_WIN
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            m_SystemSocket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
#endif


#if UNITY_EDITOR
            UnityEditor.EditorApplication.playmodeStateChanged -= OnEditorPlayModeChanged;
            UnityEditor.EditorApplication.playmodeStateChanged += OnEditorPlayModeChanged;
#endif
        }


#if UNITY_EDITOR
        private void OnEditorPlayModeChanged()
        {
            if (Application.isPlaying == false)
            {
                this.Log("OnEditorPlayModeChanged()");
                UnityEditor.EditorApplication.playmodeStateChanged -= OnEditorPlayModeChanged;
                Dispose();
            }
        }
#endif

        public void Dispose() {
            m_IsRunning = false;
            m_AnyEPListener = null;

            if (m_ThreadRecv != null) {
                m_ThreadRecv.Interrupt();
                m_ThreadRecv = null;
            }

            int cnt = m_ListKcp.Count;
            for (int i = 0; i < cnt; i++) {
                m_ListKcp[i].Dispose();
            }
            m_ListKcp.Clear();

            if (m_SystemSocket != null) {
                try {
                    m_SystemSocket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception e) {
                    NetworkDebuger.LogWarning("Close() " + e.Message + e.StackTrace);
                }

                m_SystemSocket.Close();
                m_SystemSocket = null;
            }
        }


        public int LocalPort {
            get { return (m_SystemSocket.LocalEndPoint as IPEndPoint).Port; }
        }

        public string LocalIP {
            get { return UnityEngine.Network.player.ipAddress; }
        }

        public IPEndPoint LocalEndPoint {
            get {
                if (m_LocalEndPoint == null ||
                    m_LocalEndPoint.Address.ToString() != UnityEngine.Network.player.ipAddress) {
                    IPAddress ip = IPAddress.Parse(LocalIP);
                    m_LocalEndPoint = new IPEndPoint(ip, LocalPort);
                }

                return m_LocalEndPoint;
            }
        }

        public Socket SystemSocket { get { return m_SystemSocket; } }

        #endregion

        //=================================================================================

        public bool EnableBroadcast {
            get { return m_SystemSocket.EnableBroadcast; }
            set { m_SystemSocket.EnableBroadcast = value; }
        }

        //=================================================================================
        #region 管理KCP

        private KCPProxy GetKcp(IPEndPoint ipep) {
            if (ipep == null || ipep.Port == 0 ||
                ipep.Address.Equals(IPAddress.Any) ||
                ipep.Address.Equals(IPAddress.IPv6Any)) {
                return null;
            }

            KCPProxy proxy;
            int cnt = m_ListKcp.Count;
            for (int i = 0; i < cnt; i++) {
                proxy = m_ListKcp[i];
                if (proxy.RemotePoint.Equals(ipep)) {
                    return proxy;
                }
            }

            proxy = new KCPProxy(m_KcpKey, ipep, m_SystemSocket);
            proxy.AddReceiveListener(OnReceiveAny);
            m_ListKcp.Add(proxy);
            return proxy;
        }

        #endregion

        //=================================================================================
        #region 发送逻辑
        public bool SendTo(byte[] buffer, int size, IPEndPoint remotePoint) {
            if (remotePoint.Address == IPAddress.Broadcast) {
                int cnt = m_SystemSocket.SendTo(buffer, size, SocketFlags.None, remotePoint);
                return cnt > 0;
            }
            else {
                KCPProxy proxy = GetKcp(remotePoint);
                if (proxy != null) {
                    return proxy.DoSend(buffer, size);
                }
            }

            return false;
        }

        public bool SendTo(string message, IPEndPoint remotePoint) {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            return SendTo(buffer, buffer.Length, remotePoint);
        }

        #endregion


        //=================================================================================
        #region 主线程驱动
        ushort keepHeartbeat = 0;
        const string HeartbeatMsg = "Heartbeat";
        byte[] HeartbeatMsgBuffer = Encoding.UTF8.GetBytes(HeartbeatMsg);
        public void SendKeepHeartbeat(IPEndPoint remotePoint) {
            keepHeartbeat++;
            if (keepHeartbeat > 500) {
                keepHeartbeat = 0;
                SendTo(HeartbeatMsgBuffer, HeartbeatMsgBuffer.Length, remotePoint);
            }
        }
        public void Update() {

            if (m_IsRunning) {
                //获取时钟
                long current = KCPProxy.GetClockMS();

                int cnt = m_ListKcp.Count;
                for (int i = 0; i < cnt; i++) {
                    KCPProxy proxy = m_ListKcp[i];
                    proxy.Update(current);
                }
            }
        }

        #endregion

        //=================================================================================
        #region 接收逻辑

        public void AddReceiveListener(IPEndPoint remotePoint, KCPReceiveListener listener) {
            KCPProxy proxy = GetKcp(remotePoint);
            if (proxy != null) {
                proxy.AddReceiveListener(listener);
            }
            else {
                m_AnyEPListener += listener;
            }
        }

        public void RemoveReceiveListener(IPEndPoint remotePoint, KCPReceiveListener listener) {
            KCPProxy proxy = GetKcp(remotePoint);
            if (proxy != null) {
                proxy.RemoveReceiveListener(listener);
            }
            else {
                m_AnyEPListener -= listener;
            }
        }

        public void AddReceiveListener(KCPReceiveListener listener) {
            m_AnyEPListener += listener;
        }

        public void RemoveReceiveListener(KCPReceiveListener listener) {
            m_AnyEPListener -= listener;
        }


        private void OnReceiveAny(byte[] buffer, int size, IPEndPoint remotePoint) {
            if (m_AnyEPListener != null) {
                m_AnyEPListener(buffer, size, remotePoint);
            }
        }

        #endregion

        //=================================================================================
        #region 接收线程

        private void Thread_Recv() {
            NetworkDebuger.Log("Thread_Recv() Begin ......");

            while (m_IsRunning) {
                try {
                    DoReceive();
                }
                catch (Exception e) {
                    NetworkDebuger.LogError("Thread_Recv() " + e.Message + "\n" + e.StackTrace);
                    Thread.Sleep(10);
                }
            }

            NetworkDebuger.Log("Thread_Recv() End!");
        }

        private void DoReceive() {
            if (m_SystemSocket.Available <= 0) {
                return;
            }

            EndPoint remotePoint = new IPEndPoint(IPAddress.Any, 0);
            int cnt = m_SystemSocket.ReceiveFrom(m_RecvBufferTemp, m_RecvBufferTemp.Length,
                SocketFlags.None, ref remotePoint);

            if (cnt > 0) {
                KCPProxy proxy = GetKcp((IPEndPoint)remotePoint);
                if (proxy != null) {
                    proxy.DoReceiveInThread(m_RecvBufferTemp, cnt);
                }
            }

        }

        #endregion
    }





}