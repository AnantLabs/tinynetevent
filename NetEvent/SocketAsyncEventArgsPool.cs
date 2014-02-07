using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace TinyNetEvent
{
    class SocketAsyncEventArgsPool : Stack<SocketAsyncEventArgs>
    {
        public SocketAsyncEventArgsPool(int max)
        {

        }
    }
}
