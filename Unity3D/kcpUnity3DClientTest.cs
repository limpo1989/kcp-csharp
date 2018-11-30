//#define BigEndian
#define LittleEndian
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Network_Kcp;
#if BigEndian
using KCPProxy = Network_Kcp.KCPProxy_BE;
#else
using KCPProxy = Network_Kcp.KCPProxy_LE;
#endif
namespace Assets.Script.Test
{
    public class kcpUnity3DClientTest : MonoBehaviour
    {
        private KCPPlayer p1, p2;

        void Awake() {
          

        }
        private bool m_initKcp = false;
        private bool m_stopKcpSendMsg = true;
        private IEnumerator Start() {
            yield return new WaitForSecondsRealtime(3f);
            NetworkDebuger.EnableLog = true;
            NetworkDebuger.EnableSave = true;
            NetworkDebuger.Log("Awake()");

            p1 = new KCPPlayer();
            p1.Init("Player1", 12345, 12346);

            p2 = new KCPPlayer();
            p2.Init("Player2", 12346, 12345);
            m_initKcp = true;
            //
            StartCoroutine(sendMsgLoop());
        }
        private IEnumerator sendMsgLoop() {
            if (!m_initKcp) yield return null;
            while (true) {                
                if(!m_stopKcpSendMsg) {
                    //for (int i = 0; i < 10; i++) {
                        p1.SendMessage();
                        p2.SendMessage();
                    //}
                    yield return null;
                }
               yield return null;
            }
        }

        void Update() {
            if (m_initKcp) {
                p1.OnUpdate();
                p2.OnUpdate();
            }
        }
        private void FixedUpdate() {
            if (m_initKcp) {
                p1.OnFixedUpdate();
                p2.OnFixedUpdate();
            }
         }
        void OnGUI() {
            if (!m_initKcp) return;
            if (GUILayout.Button("Player1 SendMessage")) {
                p1.SendMessage();
            }

            if (GUILayout.Button("Player2 SendMessage")) {
                p2.SendMessage();
            }
            if (GUILayout.Button("stop/continue SendMessage")) {
                m_stopKcpSendMsg = !m_stopKcpSendMsg;
                //StartCoroutine(sendMsgLoop());
            }

        }
        private void OnApplicationQuit() {
            NetworkDebuger.EnableSave = false;
            p1.Dispose();
            p2.Dispose();
        }

        public class KCPPlayer
        {
            public string LOG_TAG = "KCPPlayer";

            private KCPSocket m_Socket;
            private string m_Name;
            private int m_MsgId = 0;
            private IPEndPoint m_RemotePoint;
            public void Dispose() {
                m_Socket.Dispose();
                m_Socket = null;
            }
            public void Init(string name, int localPort, int remotePort) {
                m_Name = name;
                LOG_TAG = "KCPPlayer[" + m_Name + "]";

                IPAddress ipa = IPAddress.Parse(UnityEngine.Network.player.ipAddress);
                m_RemotePoint = new IPEndPoint(ipa, remotePort);

                m_Socket = new KCPSocket(localPort, 1, AddressFamily.InterNetwork);
                m_Socket.AddReceiveListener(KCPProxy.IPEP_Any, OnReceiveAny);
                m_Socket.AddReceiveListener(m_RemotePoint, OnReceive);

                NetworkDebuger.Log("Init() name:{0}, localPort:{1}, remotePort:{2}", name, localPort, remotePort);
            }

            private void OnReceiveAny(byte[] buffer, int size, IPEndPoint remotePoint) {
                string str = Encoding.UTF8.GetString(buffer, 0, size);
                NetworkDebuger.Log("OnReceiveAny() " + remotePoint + ":" + str);
            }

            private void OnReceive(byte[] buffer, int size, IPEndPoint remotePoint) {
                string str = Encoding.UTF8.GetString(buffer, 0, size);
                NetworkDebuger.Log("OnReceive() " + remotePoint + ":" + str);
            }

            public void OnUpdate() {
                if (m_Socket != null) {                   
                    m_Socket.Update();
                }
            }

            public void OnFixedUpdate() {                
                if (m_Socket != null) {
                    m_Socket.SendKeepHeartbeat(m_RemotePoint);
                }
            }
            string msgContent = @"
LICENSE SYSTEM [2017918 10:58:53] Next license update check is after 2025-06-30T00:00:00

Built from '5.5/release' branch; Version is '5.5.0f3 (38b4efef76f0) revision 3716335'; Using compiler version '160040219'
OS: 'Windows 7 Service Pack 1 (6.1.7601) 64bit' Language: 'zh' Physical Memory: 16224 MB
BatchMode: 0, IsHumanControllingUs: 1, StartBugReporterOnCrash: 1, Is64bit: 1, IsPro: 1
Initialize mono
Mono path[0] = 'C:/Program Files/Unity5.5.0/Editor/Data/Managed'
Mono path[1] = 'C:/Program Files/Unity5.5.0/Editor/Data/Mono/lib/mono/2.0'
Mono path[2] = 'C:/Program Files/Unity5.5.0/Editor/Data/UnityScript'
Mono config path = 'C:/Program Files/Unity5.5.0/Editor/Data/Mono/etc'
Using monoOptions --debugger-agent=transport=dt_socket,embedding=1,defer=y,address=0.0.0.0:56392
IsTimeToCheckForNewEditor: Update time 1505705705 current 1505703540
C:/work/irobotqv2.0_dev/irobotqv2.0_app
Loading GUID <-> Path mappings...0.000281 seconds
Loading Asset Database...0.015599 seconds
Audio: FMOD Profiler initialized on port 54900
AssetDatabase consistency checks...0.019115 seconds
Initialize engine version: 5.5.0f3 (38b4efef76f0)
GfxDevice: creating device client; threaded=1
Direct3D:
    Version:  Direct3D 11.0 [level 11.0]
    Renderer: AMD Radeon HD 6670 (ID=0x6758)
    Vendor:   ATI
    VRAM:     4418 MB
    Driver:   14.100.0.0
Begin MonoManager ReloadAssembly1
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEngine.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEditor.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.Locator.dll (this message is harmless)
Refreshing native plugins compatible for Editor in 9.17 ms, found 3 plugins.
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.CJK.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.DataContract.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Core.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.IvyParser.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Xml.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Configuration.dll (this message is harmless)
Begin MonoManager ReloadAssembly2
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEngine.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEditor.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.Locator.dll (this message is harmless)
Refreshing native plugins compatible for Editor in 9.17 ms, found 3 plugins.
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.CJK.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.DataContract.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Core.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.IvyParser.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Xml.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Configuration.dll (this message is harmless)
Begin MonoManager ReloadAssembly3
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEngine.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEditor.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.Locator.dll (this message is harmless)
Refreshing native plugins compatible for Editor in 9.17 ms, found 3 plugins.
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.CJK.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.DataContract.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Core.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.IvyParser.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Xml.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Configuration.dll (this message is harmless)
Begin MonoManager ReloadAssembly4
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEngine.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEditor.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.Locator.dll (this message is harmless)
Refreshing native plugins compatible for Editor in 9.17 ms, found 3 plugins.
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.CJK.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.DataContract.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Core.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.IvyParser.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Xml.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Configuration.dll (this message is harmless)
Begin MonoManager ReloadAssembly5
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEngine.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\UnityEditor.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.Locator.dll (this message is harmless)
Refreshing native plugins compatible for Editor in 9.17 ms, found 3 plugins.
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\I18N.CJK.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.DataContract.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Core.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Managed\Unity.IvyParser.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Xml.dll (this message is harmless)
Platform assembly: C:\Program Files\Unity5.5.0\Editor\Data\Mono\lib\mono\2.0\System.Configuration.dll (this message is harmless)";


            public void SendMessage() {
                if (m_Socket != null) {
                    m_MsgId++;
                    m_Socket.SendTo(m_Name + "_" + "Message" + m_MsgId+" [size]"+msgContent, m_RemotePoint);
                }
            }
        }
    }

   
}
