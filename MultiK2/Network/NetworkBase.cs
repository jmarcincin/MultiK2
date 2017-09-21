using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace MultiK2.Network
{
    abstract class NetworkBase
    {
        private object _connectionLock = new object();

        private ConcurrentQueue<INetworkCommandAsync> _sendCommandQueue;
        private ConcurrentQueue<FramePacket> _sendQueue;
        private ConcurrentQueue<FramePacket> _prioritySendQueue;
        private ConcurrentDictionary<INetworkCommandAsync, int> _activeRequests;

        protected Dictionary<ReaderType, FramePacket> _activeFrameReceives;
                
        private ReadBuffer _receiveBuffer;
        private WriteBuffer _sendBuffer;
        private AutoResetEvent _sendEvent = new AutoResetEvent(false);
        protected Socket _connection;

        public event EventHandler<bool> ConnectionClosed;

        public event EventHandler<IPEndPoint> ConnectionEstablished;

        public event EventHandler<byte[]> CustomDataReceived;

        protected void Init(IPEndPoint remoteAddress)
        {
            _sendCommandQueue = new ConcurrentQueue<INetworkCommandAsync>();
            _sendQueue = new ConcurrentQueue<FramePacket>();
            _prioritySendQueue = new ConcurrentQueue<FramePacket>();
            _activeRequests = new ConcurrentDictionary<INetworkCommandAsync, int>();
            _activeFrameReceives = new Dictionary<ReaderType, FramePacket>();

            ConnectionEstablished?.Invoke(this, remoteAddress);
        }

        protected void StartSendingData()
        {
            _sendBuffer = new WriteBuffer(ushort.MaxValue);

            var sendArgs = new SocketAsyncEventArgs();
            sendArgs.SetBuffer(_sendBuffer.GetBuffer(), _sendBuffer.WriteOffset, ushort.MaxValue);
            sendArgs.Completed += SocketOperationCompleted;

            Task.Run(() => SendData(sendArgs));
        }

        protected void StartReceivingData()
        {
            _receiveBuffer = new ReadBuffer(ushort.MaxValue);

            var receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.SetBuffer(_receiveBuffer.GetBuffer(), _receiveBuffer.WriteOffset, ushort.MaxValue);
            receiveArgs.Completed += SocketOperationCompleted;

            Task.Run(() => ReceiveData(receiveArgs));
        }

        private void ReceiveData(SocketAsyncEventArgs receiveArgs)
        {
            if (receiveArgs.SocketError != System.Net.Sockets.SocketError.Success)
            {
                CloseConnection(true);
            }

            // if operation executed synchronously
            while(_connection != null && _connection.ReceiveAsync(receiveArgs) == false)
            {   
                // process receive
                ProcessReceive(receiveArgs);
            }
        }
        
        private void SendData(SocketAsyncEventArgs sendArgs)
        {            
            do
            {
                if (sendArgs.SocketError != System.Net.Sockets.SocketError.Success)
                {
                    CloseConnection(true);
                    return;
                }
                PrepareDataToSend(sendArgs);
            }
            while (_connection != null && _connection.SendAsync(sendArgs) == false);            
        }

        public void CloseConnection()
        {
            CloseConnection(false);
        }

        public void SendCustomFrameData(byte[] data)
        {
            var frame = new CustomFramePacket(data);
            SendFrameData(frame);
        }

        protected void OnCustomDataReceived(CustomFramePacket customDataPacket)
        {
            var subs = CustomDataReceived;
            if (subs != null)
            {
                Task.Run(() => subs(this, customDataPacket.Data));
            }
        }

        private void CloseConnection(bool remoteCause)
        {
            lock (_connectionLock)
            {
                if (_connection != null)
                {
                    var con = _connection;
                    _connection = null;
                    con.Dispose();
                }
                else
                {
                    return;
                }
            }
            // send notification
            ConnectionClosed?.Invoke(this, remoteCause);
        }
        private void SocketOperationCompleted(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    {
                        // process receive
                        ProcessReceive(e);
                        ReceiveData(e);
                        break;
                    }
                case SocketAsyncOperation.Send:
                    {
                        SendData(e);
                        break;
                    }
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs receiveArgs)
        {
            // TODO: check for errors / connection closing
            if (receiveArgs.SocketError != System.Net.Sockets.SocketError.Success)
            {
                CloseConnection(true);
                return;
            }

            // update write offset
            _receiveBuffer.UpdateWritePointer(receiveArgs.BytesTransferred);

            int remainingSize;
            while(_receiveBuffer.IsPacketReceiveCompleted(out remainingSize))
            {
                // process receive
                ProcessReceivedData(_receiveBuffer);
                _receiveBuffer.EndReadingPacket();
            }

            _receiveBuffer.ResetBuffer();
            receiveArgs.SetBuffer(_receiveBuffer.WriteOffset, remainingSize);           
        }

        private void PrepareDataToSend(SocketAsyncEventArgs sendArgs)
        {
            // TODO: check for errors / connection closing
            // check if all data were send

            // TODO: validate - blocking queue impl

            while (true)
            {
                _sendBuffer.StartPacket();

                INetworkCommandAsync command;
                FramePacket activeFrameTransfer;
                if (_sendCommandQueue.TryDequeue(out command))
                {
                    if (command.Status == OperationStatus.Request)
                    {
                        _activeRequests.TryAdd(command, 0);
                    }
                    command.WriteCommand(_sendBuffer);
                    _sendBuffer.FinalizePacket();
                }
                // priority frames should be less than 64kB - otherwise same algorithm as for "normal" priority frames must apply
                else if (_prioritySendQueue.TryPeek(out activeFrameTransfer))
                {
                    if (activeFrameTransfer.WriteData(_sendBuffer))
                    {
                        // all frame data were written
                        _prioritySendQueue.TryDequeue(out activeFrameTransfer);
                    }
                    _sendBuffer.FinalizePacket();
                }

                else if (_sendQueue.TryPeek(out activeFrameTransfer))
                {
                    if (activeFrameTransfer.WriteData(_sendBuffer))
                    {
                        // all frame data were written
                        _sendQueue.TryDequeue(out activeFrameTransfer);
                    }
                    _sendBuffer.FinalizePacket();
                }

                if (_sendBuffer.FinalizedPacket)
                {
                    sendArgs.SetBuffer(_sendBuffer.ReadOffset, _sendBuffer.WriteOffset - _sendBuffer.ReadOffset);
                    return;
                }

                if (_connection == null)
                {
                    return;
                }

                _sendEvent.WaitOne();

                if (_connection == null)
                {
                    return;
                }
            }
        }        

        protected void HandleCommandResponse(INetworkCommandAsync response)
        {
            int dummyValue;
            var matchingRequest = _activeRequests.Keys.FirstOrDefault(request => request.MatchingResponse(response));

            // shouldn't happen that it is not present
            if (matchingRequest != null)
            {
                _activeRequests.TryRemove(matchingRequest, out dummyValue);
                matchingRequest.SetResponse(response);
                matchingRequest.Dispose();
            }
        }

        protected abstract void ProcessReceivedData(ReadBuffer receiveBuffer);

        protected void SendRequestCommand(INetworkCommandAsync command)
        {
            if (_connection == null)
            {
                return;
            }

            _sendCommandQueue.Enqueue(command);
            _sendEvent.Set();
        }

        protected void SendFrameData(FramePacket packet, bool highPriority = false)
        {
            if (_connection == null)
            {
                return;
            }

            if (highPriority)
            {
                _prioritySendQueue.Enqueue(packet);
            }
            else
            {
                _sendQueue.Enqueue(packet);
            }
            _sendEvent.Set();
        }
    }
}
