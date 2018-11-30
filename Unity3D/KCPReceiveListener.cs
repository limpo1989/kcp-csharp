using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Network_Kcp
{
    public delegate void KCPReceiveListener(byte[] buff, int size, IPEndPoint remotePoint);
   
}
