
//KCP_NATIVE 使用P/Invoke调用KCP协议相关处理方法，这种方式尽量规避了KCP源代码C->C#改写过程中造成的问题。
//目前C#版KCP在长时间运行后，IOPS会下降，原因尚不明确。
//简单测试阶段，可以使用C#版

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace IRobotQ.Net.Common {
    class kcp_native {
#if UNITY_IOS
        const string KCPDLL="__Internal";
#elif UNITY_ANDROID
        const string KCPDLL="libikcp";
#elif  UNITY_EDITOR || UNITY_STANDALONE
        const string KCPDLL = "ikcp";
#else
        const string KCPDLL = "ikcp";
#endif
        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ikcp_create(int conv, IntPtr user);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ikcp_release(IntPtr kcp);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ikcp_setoutput(IntPtr kcp, Delegate output);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_recv(IntPtr kcp, IntPtr buffer, int len);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_send(IntPtr kcp, IntPtr buffer, int len);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ikcp_update(IntPtr kcp, uint current);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint ikcp_check(IntPtr kcp, uint current);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_input(IntPtr kcp, IntPtr data, long size);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_peeksize(IntPtr kcp);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_setmtu(IntPtr kcp, int mtu);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint ikcp_getconv(IntPtr buf);


        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_get_interval(IntPtr buf);
        /// <summary>
        /// 设置内存分配和释放方法
        /// </summary>
        /// <param name="bufalloc_callback"></param>
        /// <param name="buffree_callbak"></param>
        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ikcp_allocator(Delegate bufalloc_callback, Delegate buffree_callbak);


        //参考：https://github.com/skywind3000/kcp/wiki/Flow-Control-for-Users
        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_waitsnd(IntPtr kcp);


        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_wndsize(IntPtr kcp, int sndwnd, int rcvwnd);



        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_nodelay(IntPtr kcp, int nodelay, int interval, int resend, int nc);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_flush(IntPtr kcp);

        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ikcp_setminrto(IntPtr kcp, int minrto);
        [DllImport(KCPDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ikcp_getminrto(IntPtr kcp);
    }

    public partial class KCPLib {
        // encode 8 bits unsigned int
        public static int ikcp_encode8u(byte[] p, int offset, byte c) {
            p[0 + offset] = c;
            return 1;
        }

        // decode 8 bits unsigned int
        public static int ikcp_decode8u(byte[] p, int offset, ref byte c) {
            c = p[0 + offset];
            return 1;
        }

        /* encode 16 bits unsigned int (lsb) */
        public static int ikcp_encode16u(byte[] p, int offset, UInt16 w) {
            p[0 + offset] = (byte)(w >> 0);
            p[1 + offset] = (byte)(w >> 8);
            return 2;
        }

        /* decode 16 bits unsigned int (lsb) */
        public static int ikcp_decode16u(byte[] p, int offset, ref UInt16 c) {
            UInt16 result = 0;
            result |= (UInt16)p[0 + offset];
            result |= (UInt16)(p[1 + offset] << 8);
            c = result;
            return 2;
        }

        /* encode 32 bits unsigned int (lsb) */
        public static int ikcp_encode32u(byte[] p, int offset, UInt32 l) {
            p[0 + offset] = (byte)(l >> 0);
            p[1 + offset] = (byte)(l >> 8);
            p[2 + offset] = (byte)(l >> 16);
            p[3 + offset] = (byte)(l >> 24);
            return 4;
        }

        /* decode 32 bits unsigned int (lsb) */
        public static int ikcp_decode32u(byte[] p, int offset, ref UInt32 c) {
            UInt32 result = 0;
            result |= (UInt32)p[0 + offset];
            result |= (UInt32)(p[1 + offset] << 8);
            result |= (UInt32)(p[2 + offset] << 16);
            result |= (UInt32)(p[3 + offset] << 24);
            c = result;
            return 4;
        }


        //BufferChunk m_bufChunk;// = new BufferChunk(16 * 1024, 64 * 1024 * 1024);
        IntPtr BufAlloc(int size) {
            if(size > 16 * 1024) {
                throw new Exception("BufAlloc size过大 size:" + size.ToString());
            }
            return BufferPoolForNative.GetBuf();
        }
        void BufFree(IntPtr p) {
            BufferPoolForNative.Return(p);
        }
        internal delegate IntPtr BufAllocCallback(int size);
        internal delegate void BufFreeCallback(IntPtr p);
        public delegate void OutputCallback(IntPtr buf, int len, IntPtr kcp, IntPtr user);

        BufAllocCallback m_BufAlloc;
        BufFreeCallback m_BufFree;

        IntPtr m_kcp;
        bool m_useNativeBufpool = false;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="conv_"></param>
        /// <param name="user"></param>
        /// <param name="output_"></param>
        /// <param name="useBufPool">是否使用内存池,如果true,则</param>
        /// <param name="bufpoolUnitSize">内存池单元尺寸，默认为4K</param>
        /// <param name="bufpoolSize">内存池总尺寸，默认8MB</param>
        public KCPLib(uint conv_, object user, OutputCallback output_, bool useNativeBufPool = false) {
            if(useNativeBufPool) {
                if(BufferPoolForNative.m_inited == false) {
                    throw new Exception("内存池BufferChunk尚未初始化，请先调用BufferChunk.Init");
                }
                m_BufAlloc = BufAlloc;
                m_BufFree = BufFree;
                kcp_native.ikcp_allocator(m_BufAlloc, m_BufFree);
            }
            m_useNativeBufpool = useNativeBufPool;
            //kcp_native.ikcp_allocator(m_BufAlloc, m_BufFree);
            m_kcp = kcp_native.ikcp_create((int)conv_, IntPtr.Zero);
            if(m_kcp == IntPtr.Zero) {
                throw new Exception("初始化KCP失败");
            }
            kcp_native.ikcp_setoutput(m_kcp, output_);
        }

        public unsafe int Input(byte[] buffer, int offset, int size) {
            fixed (byte* p = buffer) {
                return kcp_native.ikcp_input(m_kcp, new IntPtr(p + offset), size);
            }
            //IntPtr p = BufAlloc(size);
            //Marshal.Copy(buffer, offset, p, size);
            //int ret = kcp_native.ikcp_input(m_kcp, p, size);
            //BufFree(p);
            //return ret;
        }

        // check the size of next message in the recv queue
        public int PeekSize() {
            return kcp_native.ikcp_peeksize(m_kcp);
        }

        // user/upper level recv: returns size, returns below zero for EAGAIN
        public unsafe int Recv(byte[] buffer, int offset, int size) {

            fixed (byte* p = buffer) {
                int recvbytes = kcp_native.ikcp_recv(m_kcp, (IntPtr)(p + offset), size);
                return recvbytes;
            }

            //IntPtr p = BufAlloc(size);
            //int recvbytes = kcp_native.ikcp_recv(m_kcp, p, size);
            //Marshal.Copy(p, buffer, offset, size);
            //BufFree(p);
            //return recvbytes;
        }



        public unsafe int Send(byte[] buffer, int offset, int bufsize) {

            //IntPtr p = BufAlloc(bufsize);
            //Marshal.Copy(buffer, offset, p, bufsize);

            //int ret = kcp_native.ikcp_send(m_kcp, p, bufsize);
            //BufFree(p);
            //return ret;

            int send = 0;
            fixed (byte* p = buffer) {
                send = kcp_native.ikcp_send(m_kcp, new IntPtr(p + offset), bufsize);
            }
            //https://github.com/skywind3000/kcp/issues/10
            //https://github.com/skywind3000/kcp/wiki/Flow-Control-for-Users
            //https://github.com/skywind3000/kcp/issues/4
            kcp_native.ikcp_flush(m_kcp);
            return send;
        }



        // user/upper level send, returns below zero for error
        public int Send(byte[] buffer) {
            return Send(buffer, 0, buffer.Length);
        }

        public void Update(UInt32 current_) {
            kcp_native.ikcp_update(m_kcp, current_);
        }
        public UInt32 Check(UInt32 current_) {
            return kcp_native.ikcp_check(m_kcp, current_);
        }

        public int NoDelay(int nodelay, int interval, int resend, int nc) {
            return kcp_native.ikcp_nodelay(m_kcp, nodelay, interval, resend, nc);
        }
        public int GetInterval() {
            return kcp_native.ikcp_get_interval(m_kcp);
        }

        public int WaitSnd() {
            //https://github.com/skywind3000/kcp/wiki/Flow-Control-for-Users
            return kcp_native.ikcp_waitsnd(m_kcp);
        }
        public int WndSize(int sndwnd, int rcvwnd) {
            return kcp_native.ikcp_wndsize(m_kcp, sndwnd, rcvwnd);
        }
        public void SetMtu(int mtu) {
            kcp_native.ikcp_setmtu(m_kcp, mtu);//ikcp_setmtu
        }
        public void SetMinRto(int minrto) {
            kcp_native.ikcp_setminrto(m_kcp, minrto);
        }
        public int GetMinRto() {
            return kcp_native.ikcp_getminrto(m_kcp);
        }
        public void Dispose() {
            kcp_native.ikcp_release(m_kcp);
        }
    }

    public static class BufferPoolForNative {
        public static string Name;
        public static Byte[] Buffer;
        public static Stack<long> AddrIndex;
        public static GCHandle BufferHandle;
        internal static IntPtr BufferAddr;
        private static object m_locker = new object();
        internal static bool m_inited = false;
        public static IntPtr GetBuf() {
            lock(m_locker) {
                if(AddrIndex.Count == 0) {
                    throw new Exception("没有充足的空间.ChunkName:" + Name);
                }
                long addr = AddrIndex.Pop();
                Interlocked.Increment(ref InUse);
                return new IntPtr(addr);
            }
        }
        public static int InUse = 0;
        private static int m_unitBytes;
        public static void Init(int unitBytes, int totalSize) {
            Buffer = new byte[totalSize];
            BufferHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            BufferAddr = BufferHandle.AddrOfPinnedObject();
            AddrIndex = new Stack<long>();
            long start = BufferAddr.ToInt64();
            for(int j = 0; j < totalSize; j += unitBytes) {
                long add = start + j;
                AddrIndex.Push(add);
            }
            m_unitBytes = unitBytes;
            m_inited = true;
        }
        public static void Return(IntPtr buf) {
            long p = buf.ToInt64();
            long l = p % m_unitBytes;

            long addr = p - l;// (int)((buf.ToInt64() - BufferAddr.ToInt64()) / m_unitBytes);
            lock(m_locker) {
                AddrIndex.Push(addr);
            }
            Interlocked.Decrement(ref InUse);
        }


        public static void Close() {
            BufferHandle.Free();
        }


    }

}

