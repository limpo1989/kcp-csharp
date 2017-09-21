using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace Network_Kcp
{
    /// <summary>
    /// 小端数据模式
    /// </summary>
    public partial class KCP_LE
    {
        //上下文地址
        //public class IQUEUEHEAD
        //{
        //    public IQUEUEHEAD next;
        //    public IQUEUEHEAD prev;
        //}
        //public IQUEUEHEAD iqueue_head;
        #region 自己加的方法
        public void Dispose() {
            output = null;
        }
        public void FastSet() {
            rx_minrto = 10;
            fastresend = 1;
        }
        public UInt32 GetConv() {
            return conv;
        }
        public void ResetData() {
            Segment[] snd_queue = new Segment[0];
            Segment[] rcv_queue = new Segment[0];
            Segment[] snd_buf = new Segment[0];
            Segment[] rcv_buf = new Segment[0];

            UInt32[] acklist = new UInt32[0];
        }
        /// <summary>
        /// reset array buffer size
        /// </summary>
        /// <param name="self"></param>
        /// <param name="length"></param>
        /// <param name="copyData"></param>
        /// <returns></returns>
        public static byte[] Recapacity(byte[] self, int length, bool copyData = false) {
            byte[] newBytes = new byte[length];
            //if (self.Length != length) {
            //    newBytes = new byte[length];
            if (copyData) {
                Array.Copy(self, 0, newBytes, 0, length <= self.Length ? length : self.Length);
            }
            //}
            return newBytes;
        }
        #endregion
    }
    /// <summary>
    /// 大端数据模式
    /// </summary>
    public partial class KCP_BE {
        #region 自己添加的方法
        public void Dispose() {
            output = null;
        }
        #endregion
    }
}
