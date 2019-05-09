using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KcpProject
{
    class UDPSession
    {
        private Socket mSocket = null;
        private KCP mKCP = null;

        private ByteBuffer mRecvBuffer = ByteBuffer.Allocate(1024 * 32);
        private UInt32 mNextUpdateTime = 0;

        public bool IsConnected { get { return mSocket != null && mSocket.Connected; } }
        public bool WriteDelay { get; set; }

        public void Connect(string host, int port)
        {
            var endpoint = IPAddress.Parse(host);
            mSocket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            mSocket.Connect(endpoint, port);
            mKCP = new KCP((uint)(new Random().Next(1, Int32.MaxValue)), rawSend);
            // normal:  0, 40, 2, 1
            // fast:    0, 30, 2, 1
            // fast2:   1, 20, 2, 1
            // fast3:   1, 10, 2, 1
            mKCP.NoDelay(0, 30, 2, 1);
            mRecvBuffer.Clear();
        }

        public void Close()
        {
            if (mSocket != null) {
                mSocket.Close();
                mSocket = null;
                mRecvBuffer.Clear();
            }
        }

        private void rawSend(byte[] data, int length)
        {
            if (mSocket != null) {
                mSocket.Send(data, length, SocketFlags.None);
            }
        }

        public int Send(byte[] data, int index, int length)
        {
            if (mSocket == null)
                return -1;

            if (mKCP.WaitSnd >= mKCP.SndWnd) {
                return 0;
            }

            mNextUpdateTime = 0;

            var n = mKCP.Send(data, index, length);

            if (mKCP.WaitSnd >= mKCP.SndWnd || !WriteDelay) {
                mKCP.Flush(false);
            }
            return n;
        }

        public int Recv(byte[] data, int index, int length)
        {
            // 上次剩下的部分
            if (mRecvBuffer.ReadableBytes > 0) {
                var recvBytes = Math.Min(mRecvBuffer.ReadableBytes, length);
                Buffer.BlockCopy(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, data, index, recvBytes);
                mRecvBuffer.ReaderIndex += recvBytes;
                // 读完重置读写指针
                if (mRecvBuffer.ReaderIndex == mRecvBuffer.WriterIndex) {
                    mRecvBuffer.Clear();
                }
                return recvBytes;
            }

            if (mSocket == null)
                return -1;

            if (!mSocket.Poll(0, SelectMode.SelectRead)) {
                return 0;
            }

            var rn = 0;
            try {
                rn = mSocket.Receive(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, mRecvBuffer.WritableBytes, SocketFlags.None);
            } catch(Exception ex) {
                Console.WriteLine(ex.Message);
                rn = -1;
            }
            
            if (rn <= 0) {
                return rn;
            }
            mRecvBuffer.WriterIndex += rn;

            var inputN = mKCP.Input(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, mRecvBuffer.ReadableBytes, true, true);
            if (inputN < 0) {
                mRecvBuffer.Clear();
                return inputN;
            }
            mRecvBuffer.Clear();

            var size = mKCP.PeekSize();
            if (size > 0) {
                // 外部缓存足够时， 直接写入
                if (length > size) {
                    return mKCP.Recv(data, index, length);
                }

                // 使用内部缓存
                mRecvBuffer.EnsureWritableBytes(size);
                var n = mKCP.Recv(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, size);
                if (n > 0) {
                    mRecvBuffer.WriterIndex += n;
                }

                // 余下部分，下次接受
                Buffer.BlockCopy(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, data, index, length);
                mRecvBuffer.ReaderIndex += length;
                return length;
            }

            return 0;
        }

        public void Update()
        {
            if (mSocket == null)
                return;

            if (0 == mNextUpdateTime || mKCP.CurrentMS >= mNextUpdateTime)
            {
                mKCP.Update();
                mNextUpdateTime = mKCP.Check();
            }
        }
    }
}
