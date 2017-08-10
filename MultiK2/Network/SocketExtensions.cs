using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiK2.Network
{
    internal static class SocketExtensions
    {
        public static Task<byte[]> ReceiveAsync(this Socket socket, int size)
        {
            var completedSignal = new ManualResetEvent(false);
            var args = new SocketAsyncEventArgs();
            var buffer = new byte[size];

            args.Completed += (o, e) =>
            {
                completedSignal.Set();
            };
            args.SetBuffer(buffer, 0, buffer.Length);
            
            return Task.Run(
                () =>
                {
                    if (socket.ReceiveAsync(args))
                    {
                        // wait for operation end
                        completedSignal.WaitOne();
                    }

                    // trivial / incorrect implementation for very small messages 
                    if (args.SocketError != SocketError.Success)
                    {
                        throw new SocketException((int)args.SocketError);
                    }
                    if (args.BytesTransferred != size)
                    {
                        throw new InvalidOperationException("invalid data");
                    }
                    // not needed?
                    buffer = args.Buffer;

                    completedSignal.Dispose();
                    args.Dispose();

                    return buffer;
                });
        }

        public static Task SendAsync(this Socket socket, byte[] dataToSend)
        {
            var completedSignal = new ManualResetEvent(false);
            var args = new SocketAsyncEventArgs();
            
            args.Completed += (o, e) =>
            {
                completedSignal.Set();
            };
            args.SetBuffer(dataToSend, 0, dataToSend.Length);

            return Task.Run(
                () =>
                {
                    if (socket.SendAsync(args))
                    {
                        // wait for operation end
                        completedSignal.WaitOne();
                    }

                    // trivial / incorrect implementation for very small messages 
                    if (args.SocketError != SocketError.Success)
                    {
                        throw new SocketException((int)args.SocketError);
                    }
                    if (args.BytesTransferred != dataToSend.Length)
                    {
                        throw new InvalidOperationException("incomplete operation");
                    }
                                        
                    completedSignal.Dispose();
                    args.Dispose();                    
                });
        }

        public static Task<Socket> ConnectAsync(this Socket socket, IPEndPoint remoteEndPoint, byte[] initialMessage)
        {
            var completedSignal = new ManualResetEvent(false);
            var args = new SocketAsyncEventArgs();

            args.RemoteEndPoint = remoteEndPoint;
            args.Completed += (o, e) =>
            {
                completedSignal.Set();
            };
            if (initialMessage != null)
            {
                args.SetBuffer(initialMessage, 0, initialMessage.Length);
            }

            return Task.Run(
                () =>
                {
                    if (socket.ConnectAsync(args))
                    {
                        // wait for operation end
                        completedSignal.WaitOne();
                    }
                    if (args.SocketError != SocketError.Success)
                    {
                        throw new SocketException((int)args.SocketError);
                    }
                    var connectSocket = args.ConnectSocket;

                    completedSignal.Dispose();
                    args.Dispose();
                    
                    return connectSocket;                   
                });
        }

        public static Task<Socket> AcceptAsync(this Socket socket)
        {
            var completedSignal = new ManualResetEvent(false);
            var args = new SocketAsyncEventArgs();
            args.Completed += (o, e) =>
            {
                completedSignal.Set();
            };

            return Task.Run(
                () =>
                {
                    if (socket.AcceptAsync(args))
                    {
                        // wait for operation end
                        completedSignal.WaitOne();
                    }
                    if (args.SocketError != SocketError.Success)
                    {
                        throw new SocketException((int)args.SocketError);
                    }
                    var acceptSocket = args.AcceptSocket;

                    completedSignal.Dispose();
                    args.Dispose();

                    return acceptSocket;
                });
        }
    }
}
