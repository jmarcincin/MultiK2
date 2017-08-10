using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        private ConcurrentQueue<INetworkCommandAsync> _sendCommandQueue = new ConcurrentQueue<INetworkCommandAsync>();
        private ConcurrentQueue<FramePacket> _sendFrameQueue = new ConcurrentQueue<FramePacket>();
        private ConcurrentQueue<FramePacket> _prioritySendFrameQueue = new ConcurrentQueue<FramePacket>();

        private ConcurrentDictionary<INetworkCommandAsync, int> _activeRequests = new ConcurrentDictionary<INetworkCommandAsync, int>();
        protected Dictionary<ReaderType, FramePacket> _activeFrameReceives = new Dictionary<ReaderType, FramePacket>();
        
        protected Socket _connection;
        private ReadBuffer _receiveBuffer;
        private WriteBuffer _sendBuffer;
        private AutoResetEvent _sendEvent = new AutoResetEvent(false);
        
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
            // if operation executed synchronously
            while(_connection.ReceiveAsync(receiveArgs) == false)
            {
                // process receive
                ProcessReceive(receiveArgs);
            }
        }
        
        private void SendData(SocketAsyncEventArgs sendArgs)
        {
            do
            {
                PrepareDataToSend(sendArgs);
            }
            while (_connection.SendAsync(sendArgs) == false);            
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
            //receiveArgs.BytesTransferred

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
                // priority frames should be less than 64kB
                else if (_prioritySendFrameQueue.TryDequeue(out activeFrameTransfer))
                {
                    activeFrameTransfer.WriteData(_sendBuffer);
                    _sendBuffer.FinalizePacket();
                }

                else if (_sendFrameQueue.TryPeek(out activeFrameTransfer))
                {
                    if (activeFrameTransfer.WriteData(_sendBuffer))
                    {
                        // all frame data were written
                        _sendFrameQueue.TryDequeue(out activeFrameTransfer);
                    }
                    _sendBuffer.FinalizePacket();
                }

                if (_sendBuffer.FinalizedPacket)
                {
                    sendArgs.SetBuffer(_sendBuffer.ReadOffset, _sendBuffer.WriteOffset - _sendBuffer.ReadOffset);
                    return;
                }
                _sendEvent.WaitOne();
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
            _sendCommandQueue.Enqueue(command);
            _sendEvent.Set();
        }

        protected void SendFrameData(FramePacket packet, bool highPriority = false)
        {
            if (highPriority)
            {
                _prioritySendFrameQueue.Enqueue(packet);
            }
            else
            {
                _sendFrameQueue.Enqueue(packet);
            }
            _sendEvent.Set();
        }
    }
}
