using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Network_Kcp
{
    //=====================================================================
    //
    // KCP - A Better ARQ Protocol Implementation
    // skywind3000 (at) gmail.com, 2010-2011
    //  
    // Features:
    // + Average RTT reduce 30% - 40% vs traditional ARQ like tcp.
    // + Maximum RTT reduce three times vs tcp.
    // + Lightweight, distributed as a single source file.
    //
    //=====================================================================

    /// <summary>
    /// 两个不同的CPU派系。 
    ///Motorola的PowerPC系列CPU采用Big Endian方式存储数据。 
     ///Intel的x86系列CPU采用Little Endian方式存储数据。
    /// 大端写法 适用于与java客户端(安卓) 网络通信
    /// </summary>
    public partial class KCP_BE
    {

        //=====================================================================
        // KCP BASIC
        //=====================================================================
        public const int IKCP_RTO_NDL = 30;   // no delay min rto
        public const int IKCP_RTO_MIN = 100;  // normal min rto
        public const int IKCP_RTO_DEF = 200;
        public const int IKCP_RTO_MAX = 60000;
        public const int IKCP_CMD_PUSH = 81;  // cmd: push data
        public const int IKCP_CMD_ACK = 82;   // cmd: ack
        public const int IKCP_CMD_WASK = 83;  // cmd: window probe (ask)
        public const int IKCP_CMD_WINS = 84;  // cmd: window size (tell)
        public const int IKCP_ASK_SEND = 1;   // need to send IKCP_CMD_WASK
        public const int IKCP_ASK_TELL = 2;   // need to send IKCP_CMD_WINS
        public const int IKCP_WND_SND = 32;
        public const int IKCP_WND_RCV = 32;
        public const int IKCP_MTU_DEF = 1400;
        public const int IKCP_ACK_FAST = 3;
        public const int IKCP_INTERVAL = 100;
        public const int IKCP_OVERHEAD = 24;
        public const int IKCP_DEADLINK = 10;
        public const int IKCP_THRESH_INIT = 2;
        public const int IKCP_THRESH_MIN = 2;
        public const int IKCP_PROBE_INIT = 7000;    // 7 secs to probe window size
        public const int IKCP_PROBE_LIMIT = 120000; // up to 120 secs to probe window

        //protected abstract void output(byte[] buffer, int size); // 需具体实现
        // buffer, size
        Action<byte[], int> output;
        // encode 8 bits unsigned int
        public static void ikcp_encode8u(byte[] p, int offset, byte c) {
            p[0 + offset] = c;
        }

        // decode 8 bits unsigned int
        public static byte ikcp_decode8u(byte[] p, int offset) {
            return p[0 + offset];
        }

        /* encode 16 bits unsigned int (msb) */
        public static void ikcp_encode16u(byte[] p, int offset, int w) {
            p[offset + 0] = (byte)(w >> 8);
            p[offset + 1] = (byte)(w >> 0);
        }

        /* decode 16 bits unsigned int (msb) */
        public static int ikcp_decode16u(byte[] p, int offset) {
            int ret = (p[offset + 0] & 0xFF) << 8
                    | (p[offset + 1] & 0xFF);
            return ret;
        }

        /* encode 32 bits unsigned int (msb) */
        public static void ikcp_encode32u(byte[] p, int offset, long l) {
            p[offset + 0] = (byte)(l >> 24);
            p[offset + 1] = (byte)(l >> 16);
            p[offset + 2] = (byte)(l >> 8);
            p[offset + 3] = (byte)(l >> 0);
        }

        /* decode 32 bits unsigned int (msb) */
        public static long ikcp_decode32u(byte[] p, int offset) {
            long ret = (p[offset + 0] & 0xFFL) << 24
                    | (p[offset + 1] & 0xFFL) << 16
                    | (p[offset + 2] & 0xFFL) << 8
                    | p[offset + 3] & 0xFFL;
            return ret;
        }

        public static void slice(IList list, int start, int stop) {
            int size = list.Count;//.size();                      
            for (int i = 0; i < size; ++i) {
                if (i < stop - start) {
                    //list.set(i, list.get(i + start));
                    list[i] = list[i+start];
                }
                else {
                    //list.remove(stop - start);
                    //移除最后一个
                    list.RemoveAt(stop-start);
                }
            }
        }

        static long _imin_(long a, long b) {
            return a <= b ? a : b;
        }

        static long _imax_(long a, long b) {
            return a >= b ? a : b;
        }

        static long _ibound_(long lower, long middle, long upper) {
            return _imin_(_imax_(lower, middle), upper);
        }

        static int _itimediff(long later, long earlier) {
            return ((int)(later - earlier));
        }

        private class Segment
        {

            internal long conv = 0;
            internal long cmd = 0;
            internal long frg = 0;
            internal long wnd = 0;
            internal long ts = 0;
            internal long sn = 0;
            internal long una = 0;
            internal long resendts = 0;
            internal long rto = 0;
            internal long fastack = 0;
            internal long xmit = 0;
            internal byte[] data;

            internal Segment(int size) {
                this.data = new byte[size];
            }

            //---------------------------------------------------------------------
            // ikcp_encode_seg
            //---------------------------------------------------------------------
            // encode a segment into buffer
            internal int encode(byte[] ptr, int offset) {
                int offset_ = offset;

                ikcp_encode32u(ptr, offset, conv);
                offset += 4;
                ikcp_encode8u(ptr, offset, (byte)cmd);
                offset += 1;
                ikcp_encode8u(ptr, offset, (byte)frg);
                offset += 1;
                ikcp_encode16u(ptr, offset, (int)wnd);
                offset += 2;
                ikcp_encode32u(ptr, offset, ts);
                offset += 4;
                ikcp_encode32u(ptr, offset, sn);
                offset += 4;
                ikcp_encode32u(ptr, offset, una);
                offset += 4;
                ikcp_encode32u(ptr, offset, (long)data.Length);
                offset += 4;

                return offset - offset_;
            }
        }

        long conv = 0;
        //long user = user;
        long snd_una = 0;
        long snd_nxt = 0;
        long rcv_nxt = 0;
        long ts_recent = 0;
        long ts_lastack = 0;
        long ts_probe = 0;
        long probe_wait = 0;
        long snd_wnd = IKCP_WND_SND;
        long rcv_wnd = IKCP_WND_RCV;
        long rmt_wnd = IKCP_WND_RCV;
        long cwnd = 0;
        long incr = 0;
        long probe = 0;
        long mtu = IKCP_MTU_DEF;
        long mss = 0L;// mtu - IKCP_OVERHEAD;
        byte[] buffer = null;//new byte[(int)(mtu + IKCP_OVERHEAD) * 3];
        List<Segment> nrcv_buf = new List<Segment>(128);
        List<Segment> nsnd_buf = new List<Segment>(128);
        List<Segment> nrcv_que = new List<Segment>(128);
        List<Segment> nsnd_que = new List<Segment>(128);
        long state = 0;
        List<long> acklist = new List<long>(128);
        //long ackblock = 0;
        //long ackcount = 0;
        long rx_srtt = 0;
        long rx_rttval = 0;
        long rx_rto = IKCP_RTO_DEF;
        long rx_minrto = IKCP_RTO_MIN;
        long current = 0;
        long interval = IKCP_INTERVAL;
        long ts_flush = IKCP_INTERVAL;
        long nodelay = 0;
        long updated = 0;
        long logmask = 0;
        long ssthresh = IKCP_THRESH_INIT;
        long fastresend = 0;
        long nocwnd = 0;
        long xmit = 0;
        long dead_link = IKCP_DEADLINK;
        //long output = NULL;
        //long writelog = NULL;

        public KCP_BE(long conv_, Action<byte[], int> output_) {
            conv = conv_;
            output = output_;
            //
            snd_wnd = IKCP_WND_SND;
            rcv_wnd = IKCP_WND_RCV;
            rmt_wnd = IKCP_WND_RCV;
            mtu = IKCP_MTU_DEF;
            mss = mtu - IKCP_OVERHEAD;

            rx_rto = IKCP_RTO_DEF;
            rx_minrto = IKCP_RTO_MIN;
            interval = IKCP_INTERVAL;
            ts_flush = IKCP_INTERVAL;
            ssthresh = IKCP_THRESH_INIT;
            dead_link = IKCP_DEADLINK;
            buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
        }

        //---------------------------------------------------------------------
        // user/upper level recv: returns size, returns below zero for EAGAIN
        //---------------------------------------------------------------------
        // 将接收队列中的数据传递给上层引用
        public int Recv(byte[] buffer) {

            if (0 == nrcv_que.Count) {
                return -1;
            }

            int peekSize = PeekSize();
            if (0 > peekSize) {
                return -2;
            }

            if (peekSize > buffer.Length) {
                return -3;
            }

            bool recover = false;
            if (nrcv_que.Count >= rcv_wnd) {
                recover = true;
            }

            // merge fragment.
            int count = 0;
            int n = 0;
            //for (Segment seg : nrcv_que) {
            foreach(var seg in nrcv_que) {
                //System.arraycopy(seg.data, 0, buffer, n, seg.data.length);
                Array.Copy(seg.data, 0, buffer, n, seg.data.Length);
                n += seg.data.Length;
                count++;
                if (0 == seg.frg) {
                    break;
                }
            }

            if (0 < count) {
                slice(nrcv_que, count, nrcv_que.Count);
            }

            // move available data from rcv_buf -> nrcv_que
            count = 0;
            //for (Segment seg : nrcv_buf) {
            foreach(var seg in nrcv_buf) { 
                if (seg.sn == rcv_nxt && nrcv_que.Count < rcv_wnd) {
                    nrcv_que.Add(seg);
                    rcv_nxt++;
                    count++;
                }
                else {
                    break;
                }
            }

            if (0 < count) {
                slice(nrcv_buf, count, nrcv_buf.Count);
            }

            // fast recover
            if (nrcv_que.Count < rcv_wnd && recover) {
                // ready to send back IKCP_CMD_WINS in ikcp_flush
                // tell remote my window size
                probe |= IKCP_ASK_TELL;
            }

            return n;
        }

        //---------------------------------------------------------------------
        // peek data size
        //---------------------------------------------------------------------
        // check the size of next message in the recv queue
        // 计算接收队列中有多少可用的数据
        public int PeekSize() {
            if (0 == nrcv_que.Count) {
                return -1;
            }

            Segment seq = nrcv_que[0];//.get(0);

            if (0 == seq.frg) {
                return seq.data.Length;
            }

            if (nrcv_que.Count < seq.frg + 1) {
                return -1;
            }

            int length = 0;

            //for (Segment item : nrcv_que) {
            foreach(var item in nrcv_que) { 
                length += item.data.Length;
                if (0 == item.frg) {
                    break;
                }
            }

            return length;
        }

        //---------------------------------------------------------------------
        // user/upper level send, returns below zero for error
        //---------------------------------------------------------------------
        // 上层要发送的数据丢给发送队列，发送队列会根据mtu大小分片
        public int Send(byte[] buffer) {

            if (0 == buffer.Length) {
                return -1;
            }

            int count;

            // 根据mss大小分片
            if (buffer.Length < mss) {
                count = 1;
            }
            else {
                count = (int)(buffer.Length + mss - 1) / (int)mss;
            }

            if (255 < count) {
                return -2;
            }

            if (0 == count) {
                count = 1;
            }

            int offset = 0;

            // 分片后加入到发送队列
            int length = buffer.Length;
            for (int i = 0; i < count; i++) {
                int size = (int)(length > mss ? mss : length);
                Segment seg = new Segment(size);
                //System.arraycopy(buffer, offset, seg.data, 0, size);
                Array.Copy(buffer,offset,seg.data,0,size);
                offset += size;
                seg.frg = count - i - 1;
                nsnd_que.Add(seg);
                length -= size;
            }
            return 0;
        }

        //---------------------------------------------------------------------
        // parse ack
        //---------------------------------------------------------------------
        void update_ack(int rtt) {
            if (0 == rx_srtt) {
                rx_srtt = rtt;
                rx_rttval = rtt / 2;
            }
            else {
                int delta = (int)(rtt - rx_srtt);
                if (0 > delta) {
                    delta = -delta;
                }

                rx_rttval = (3 * rx_rttval + delta) / 4;
                rx_srtt = (7 * rx_srtt + rtt) / 8;
                if (rx_srtt < 1) {
                    rx_srtt = 1;
                }
            }

            int rto = (int)(rx_srtt + _imax_(1, 4 * rx_rttval));
            rx_rto = _ibound_(rx_minrto, rto, IKCP_RTO_MAX);
        }

        // 计算本地真实snd_una
        void shrink_buf() {
            if (nsnd_buf.Count > 0) {
                snd_una = nsnd_buf[0].sn;
            }
            else {
                snd_una = snd_nxt;
            }
        }

        // 对端返回的ack, 确认发送成功时，对应包从发送缓存中移除
        void parse_ack(long sn) {
            if (_itimediff(sn, snd_una) < 0 || _itimediff(sn, snd_nxt) >= 0) {
                return;
            }

            int index = 0;
            //for (Segment seg : nsnd_buf) {
            foreach(var seg in nsnd_buf) { 
                if (_itimediff(sn, seg.sn) < 0) {
                    break;
                }

                // 原版ikcp_parse_fastack&ikcp_parse_ack逻辑重复
                seg.fastack++;

                if (sn == seg.sn) {
                    nsnd_buf.RemoveAt(index);
                    break;
                }
                index++;
            }
        }

        // 通过对端传回的una将已经确认发送成功包从发送缓存中移除
        void parse_una(long una) {
            int count = 0;
            //for (Segment seg : nsnd_buf) {
            foreach(var seg in nsnd_buf) { 
                if (_itimediff(una, seg.sn) > 0) {
                    count++;
                }
                else {
                    break;
                }
            }

            if (0 < count) {
                slice(nsnd_buf, count, nsnd_buf.Count);
            }
        }

        //---------------------------------------------------------------------
        // ack append
        //---------------------------------------------------------------------
        // 收数据包后需要给对端回ack，flush时发送出去
        void ack_push(long sn, long ts) {
            // c原版实现中按*2扩大容量
            acklist.Add(sn);
            acklist.Add(ts);
        }

        //---------------------------------------------------------------------
        // parse data
        //---------------------------------------------------------------------
        // 用户数据包解析
        void parse_data(Segment newseg) {
            long sn = newseg.sn;
            bool repeat = false;

            if (_itimediff(sn, rcv_nxt + rcv_wnd) >= 0 || _itimediff(sn, rcv_nxt) < 0) {
                return;
            }

            int n = nrcv_buf.Count - 1;
            int after_idx = -1;

            // 判断是否是重复包，并且计算插入位置
            for (int i = n; i >= 0; i--) {
                Segment seg = nrcv_buf[i];//.get(i);
                if (seg.sn == sn) {
                    repeat = true;
                    break;
                }

                if (_itimediff(sn, seg.sn) > 0) {
                    after_idx = i;
                    break;
                }
            }

            // 如果不是重复包，则插入
            if (!repeat) {
                if (after_idx == -1) {
                    nrcv_buf.Insert(0, newseg);
                }
                else {
                    nrcv_buf.Insert(after_idx + 1, newseg);
                }
            }

            // move available data from nrcv_buf -> nrcv_que
            // 将连续包加入到接收队列
            int count = 0;
            //for (Segment seg : nrcv_buf) {
            foreach(var seg in nrcv_buf) { 
                if (seg.sn == rcv_nxt && nrcv_que.Count < rcv_wnd) {
                    nrcv_que.Add(seg);
                    rcv_nxt++;
                    count++;
                }
                else {
                    break;
                }
            }

            // 从接收缓存中移除
            if (0 < count) {
                slice(nrcv_buf, count, nrcv_buf.Count);
            }
        }

        // when you received a low level packet (eg. UDP packet), call it
        //---------------------------------------------------------------------
        // input data
        //---------------------------------------------------------------------
        // 底层收包后调用，再由上层通过Recv获得处理后的数据
        public int Input(byte[] data) {

            long s_una = snd_una;
            if (data.Length < IKCP_OVERHEAD) {
                return 0;
            }

            int offset = 0;

            while (true) {
                long ts, sn, length, una, conv_;
                int wnd;
                byte cmd, frg;

                if (data.Length - offset < IKCP_OVERHEAD) {
                    break;
                }

                conv_ = ikcp_decode32u(data, offset);
                offset += 4;
                if (conv != conv_) {
                    return -1;
                }

                cmd = ikcp_decode8u(data, offset);
                offset += 1;
                frg = ikcp_decode8u(data, offset);
                offset += 1;
                wnd = ikcp_decode16u(data, offset);
                offset += 2;
                ts = ikcp_decode32u(data, offset);
                offset += 4;
                sn = ikcp_decode32u(data, offset);
                offset += 4;
                una = ikcp_decode32u(data, offset);
                offset += 4;
                length = ikcp_decode32u(data, offset);
                offset += 4;

                if (data.Length - offset < length) {
                    return -2;
                }

                if (cmd != IKCP_CMD_PUSH && cmd != IKCP_CMD_ACK && cmd != IKCP_CMD_WASK && cmd != IKCP_CMD_WINS) {
                    return -3;
                }

                rmt_wnd = (long)wnd;
                parse_una(una);
                shrink_buf();

                if (IKCP_CMD_ACK == cmd) {
                    if (_itimediff(current, ts) >= 0) {
                        update_ack(_itimediff(current, ts));
                    }
                    parse_ack(sn);
                    shrink_buf();
                }
                else if (IKCP_CMD_PUSH == cmd) {
                    if (_itimediff(sn, rcv_nxt + rcv_wnd) < 0) {
                        ack_push(sn, ts);
                        if (_itimediff(sn, rcv_nxt) >= 0) {
                            Segment seg = new Segment((int)length);
                            seg.conv = conv_;
                            seg.cmd = cmd;
                            seg.frg = frg;
                            seg.wnd = wnd;
                            seg.ts = ts;
                            seg.sn = sn;
                            seg.una = una;

                            if (length > 0) {
                                //System.arraycopy(data, offset, seg.data, 0, (int)length);
                                Array.Copy(data,offset,seg.data,0,(int)length);
                            }

                            parse_data(seg);
                        }
                    }
                }
                else if (IKCP_CMD_WASK == cmd) {
                    // ready to send back IKCP_CMD_WINS in Ikcp_flush
                    // tell remote my window size
                    probe |= IKCP_ASK_TELL;
                }
                else if (IKCP_CMD_WINS == cmd) {
                    // do nothing
                }
                else {
                    return -3;
                }

                offset += (int)length;
            }

            if (_itimediff(snd_una, s_una) > 0) {
                if (cwnd < rmt_wnd) {
                    long mss_ = mss;
                    if (cwnd < ssthresh) {
                        cwnd++;
                        incr += mss_;
                    }
                    else {
                        if (incr < mss_) {
                            incr = mss_;
                        }
                        incr += (mss_ * mss_) / incr + (mss_ / 16);
                        if ((cwnd + 1) * mss_ <= incr) {
                            cwnd++;
                        }
                    }
                    if (cwnd > rmt_wnd) {
                        cwnd = rmt_wnd;
                        incr = rmt_wnd * mss_;
                    }
                }
            }

            return 0;
        }

        // 接收窗口可用大小
        int wnd_unused() {
            if (nrcv_que.Count < rcv_wnd) {
                return (int)(int)rcv_wnd - nrcv_que.Count;
            }
            return 0;
        }

        //---------------------------------------------------------------------
        // ikcp_flush
        //---------------------------------------------------------------------
        void flush() {
            long current_ = current;
            byte[] buffer_ = buffer;
            int change = 0;
            int lost = 0;

            // 'ikcp_update' haven't been called. 
            if (0 == updated) {
                return;
            }

            Segment seg = new Segment(0);
            seg.conv = conv;
            seg.cmd = IKCP_CMD_ACK;
            seg.wnd = (long)wnd_unused();
            seg.una = rcv_nxt;

            // flush acknowledges
            // 将acklist中的ack发送出去
            int count = acklist.Count / 2;
            int offset = 0;
            for (int i = 0; i < count; i++) {
                if (offset + IKCP_OVERHEAD > mtu) {
                    output(buffer, offset);
                    offset = 0;
                }
                // ikcp_ack_get
                seg.sn = acklist[i * 2 + 0];//.get(i * 2 + 0);
                seg.ts = acklist[i * 2 + 1];//.get(i * 2 + 1);
                offset += seg.encode(buffer, offset);
            }
            acklist.Clear();

            // probe window size (if remote window size equals zero)
            // rmt_wnd=0时，判断是否需要请求对端接收窗口
            if (0 == rmt_wnd) {
                if (0 == probe_wait) {
                    probe_wait = IKCP_PROBE_INIT;
                    ts_probe = current + probe_wait;
                }
                else {
                    // 逐步扩大请求时间间隔
                    if (_itimediff(current, ts_probe) >= 0) {
                        if (probe_wait < IKCP_PROBE_INIT) {
                            probe_wait = IKCP_PROBE_INIT;
                        }
                        probe_wait += probe_wait / 2;
                        if (probe_wait > IKCP_PROBE_LIMIT) {
                            probe_wait = IKCP_PROBE_LIMIT;
                        }
                        ts_probe = current + probe_wait;
                        probe |= IKCP_ASK_SEND;
                    }
                }
            }
            else {
                ts_probe = 0;
                probe_wait = 0;
            }

            // flush window probing commands
            // 请求对端接收窗口
            if ((probe & IKCP_ASK_SEND) != 0) {
                seg.cmd = IKCP_CMD_WASK;
                if (offset + IKCP_OVERHEAD > mtu) {
                    output(buffer, offset);
                    offset = 0;
                }
                offset += seg.encode(buffer, offset);
            }

            // flush window probing commands(c#)
            // 告诉对端自己的接收窗口
            if ((probe & IKCP_ASK_TELL) != 0) {
                seg.cmd = IKCP_CMD_WINS;
                if (offset + IKCP_OVERHEAD > mtu) {
                    output(buffer, offset);
                    offset = 0;
                }
                offset += seg.encode(buffer, offset);
            }

            probe = 0;

            // calculate window size
            long cwnd_ = _imin_(snd_wnd, rmt_wnd);
            // 如果采用拥塞控制
            if (0 == nocwnd) {
                cwnd_ = _imin_(cwnd, cwnd_);
            }

            count = 0;
            // move data from snd_queue to snd_buf
            //for (Segment nsnd_que1 : nsnd_que) {
            foreach(var nsnd_que1 in nsnd_que) { 
                if (_itimediff(snd_nxt, snd_una + cwnd_) >= 0) {
                    break;
                }
                Segment newseg = nsnd_que1;
                newseg.conv = conv;
                newseg.cmd = IKCP_CMD_PUSH;
                newseg.wnd = seg.wnd;
                newseg.ts = current_;
                newseg.sn = snd_nxt;
                newseg.una = rcv_nxt;
                newseg.resendts = current_;
                newseg.rto = rx_rto;
                newseg.fastack = 0;
                newseg.xmit = 0;
                nsnd_buf.Add(newseg);
                snd_nxt++;
                count++;
            }

            if (0 < count) {
                slice(nsnd_que, count, nsnd_que.Count);
            }

            // calculate resent
            long resent = (fastresend > 0) ? fastresend : 0xffffffff;
            long rtomin = (nodelay == 0) ? (rx_rto >> 3) : 0;

            // flush data segments
            //for (Segment segment : nsnd_buf) {
            foreach(var segment in nsnd_buf) { 
                bool needsend = false;
                if (0 == segment.xmit) {
                    // 第一次传输
                    needsend = true;
                    segment.xmit++;
                    segment.rto = rx_rto;
                    segment.resendts = current_ + segment.rto + rtomin;
                }
                else if (_itimediff(current_, segment.resendts) >= 0) {
                    // 丢包重传
                    needsend = true;
                    segment.xmit++;
                    xmit++;
                    if (0 == nodelay) {
                        segment.rto += rx_rto;
                    }
                    else {
                        segment.rto += rx_rto / 2;
                    }
                    segment.resendts = current_ + segment.rto;
                    lost = 1;
                }
                else if (segment.fastack >= resent) {
                    // 快速重传
                    needsend = true;
                    segment.xmit++;
                    segment.fastack = 0;
                    segment.resendts = current_ + segment.rto;
                    change++;
                }

                if (needsend) {
                    segment.ts = current_;
                    segment.wnd = seg.wnd;
                    segment.una = rcv_nxt;

                    int need = IKCP_OVERHEAD + segment.data.Length;
                    if (offset + need >= mtu) {
                        output(buffer, offset);
                        offset = 0;
                    }

                    offset += segment.encode(buffer, offset);
                    if (segment.data.Length > 0) {
                        //System.arraycopy(segment.data, 0, buffer, offset, segment.data.length);
                        Array.Copy(segment.data, 0, buffer, offset, segment.data.Length);
                        offset += segment.data.Length;
                    }

                    if (segment.xmit >= dead_link) {
                        state = -1; // state = 0(c#)
                    }
                }
            }

            // flash remain segments
            if (offset > 0) {
                output(buffer, offset);
            }

            // update ssthresh
            // 拥塞避免
            if (change != 0) {
                long inflight = snd_nxt - snd_una;
                ssthresh = inflight / 2;
                if (ssthresh < IKCP_THRESH_MIN) {
                    ssthresh = IKCP_THRESH_MIN;
                }
                cwnd = ssthresh + resent;
                incr = cwnd * mss;
            }

            if (lost != 0) {
                ssthresh = cwnd / 2;
                if (ssthresh < IKCP_THRESH_MIN) {
                    ssthresh = IKCP_THRESH_MIN;
                }
                cwnd = 1;
                incr = mss;
            }

            if (cwnd < 1) {
                cwnd = 1;
                incr = mss;
            }
        }

        //---------------------------------------------------------------------
        // update state (call it repeatedly, every 10ms-100ms), or you can ask 
        // ikcp_check when to call it again (without ikcp_input/_send calling).
        // 'current' - current timestamp in millisec. 
        //---------------------------------------------------------------------
        public void Update(long current_) {

            current = current_;

            // 首次调用Update
            if (0 == updated) {
                updated = 1;
                ts_flush = current;
            }

            // 两次更新间隔
            int slap = _itimediff(current, ts_flush);

            // interval设置过大或者Update调用间隔太久
            if (slap >= 10000 || slap < -10000) {
                ts_flush = current;
                slap = 0;
            }

            // flush同时设置下一次更新时间
            if (slap >= 0) {
                ts_flush += interval;
                if (_itimediff(current, ts_flush) >= 0) {
                    ts_flush = current + interval;
                }
                flush();
            }
        }

        //---------------------------------------------------------------------
        // Determine when should you invoke ikcp_update:
        // returns when you should invoke ikcp_update in millisec, if there 
        // is no ikcp_input/_send calling. you can call ikcp_update in that
        // time, instead of call update repeatly.
        // Important to reduce unnacessary ikcp_update invoking. use it to 
        // schedule ikcp_update (eg. implementing an epoll-like mechanism, 
        // or optimize ikcp_update when handling massive kcp connections)
        //---------------------------------------------------------------------
        public long Check(long current_) {

            long ts_flush_ = ts_flush;
            long tm_flush = 0x7fffffff;
            long tm_packet = 0x7fffffff;
            long minimal;

            if (0 == updated) {
                return current_;
            }

            if (_itimediff(current_, ts_flush_) >= 10000 || _itimediff(current_, ts_flush_) < -10000) {
                ts_flush_ = current_;
            }

            if (_itimediff(current_, ts_flush_) >= 0) {
                return current_;
            }

            tm_flush = _itimediff(ts_flush_, current_);

            //for (Segment seg : nsnd_buf) {
            foreach(var seg in nsnd_buf) { 
                int diff = _itimediff(seg.resendts, current_);
                if (diff <= 0) {
                    return current_;
                }
                if (diff < tm_packet) {
                    tm_packet = diff;
                }
            }

            minimal = tm_packet < tm_flush ? tm_packet : tm_flush;
            if (minimal >= interval) {
                minimal = interval;
            }

            return current_ + minimal;
        }

        // change MTU size, default is 1400
        public int SetMtu(int mtu_) {
            if (mtu_ < 50 || mtu_ < (int)IKCP_OVERHEAD) {
                return -1;
            }

            byte[] buffer_ = new byte[(mtu_ + IKCP_OVERHEAD) * 3];
            if (null == buffer_) {
                return -2;
            }

            mtu = (long)mtu_;
            mss = mtu - IKCP_OVERHEAD;
            buffer = buffer_;
            return 0;
        }

        public int Interval(int interval_) {
            if (interval_ > 5000) {
                interval_ = 5000;
            }
            else if (interval_ < 10) {
                interval_ = 10;
            }
            interval = (long)interval_;
            return 0;
        }

        // fastest: ikcp_nodelay(kcp, 1, 20, 2, 1)
        // nodelay: 0:disable(default), 1:enable
        // interval: internal update timer interval in millisec, default is 100ms
        // resend: 0:disable fast resend(default), 1:enable fast resend
        // nc: 0:normal congestion control(default), 1:disable congestion control
        public int NoDelay(int nodelay_, int interval_, int resend_, int nc_) {

            if (nodelay_ >= 0) {
                nodelay = nodelay_;
                if (nodelay_ != 0) {
                    rx_minrto = IKCP_RTO_NDL;
                }
                else {
                    rx_minrto = IKCP_RTO_MIN;
                }
            }

            if (interval_ >= 0) {
                if (interval_ > 5000) {
                    interval_ = 5000;
                }
                else if (interval_ < 10) {
                    interval_ = 10;
                }
                interval = interval_;
            }

            if (resend_ >= 0) {
                fastresend = resend_;
            }

            if (nc_ >= 0) {
                nocwnd = nc_;
            }

            return 0;
        }

        // set maximum window size: sndwnd=32, rcvwnd=32 by default
        public int WndSize(int sndwnd, int rcvwnd) {
            if (sndwnd > 0) {
                snd_wnd = (long)sndwnd;
            }

            if (rcvwnd > 0) {
                rcv_wnd = (long)rcvwnd;
            }
            return 0;
        }

        // get how many packet is waiting to be sent
        public int WaitSnd() {
            return nsnd_buf.Count + nsnd_que.Count;
        }

        
    }


}
