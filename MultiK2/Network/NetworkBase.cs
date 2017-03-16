using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        private ConcurrentQueue<FramePacket> _frameQueue = new ConcurrentQueue<FramePacket>();
        private ConcurrentQueue<FramePacket> _priorityFrameQueue = new ConcurrentQueue<FramePacket>();

        private ConcurrentDictionary<INetworkCommandAsync, int> _activeRequests = new ConcurrentDictionary<INetworkCommandAsync, int>();
        private int _sendDataState;

        protected Dictionary<ReaderType, FramePacket> _activeFrameReceives = new Dictionary<ReaderType, FramePacket>();
        protected StreamSocket _sensorConnection;
        protected DataReader _dataReader;
        protected DataWriter _dataWriter;
        
        protected void SendRequestCommand(INetworkCommandAsync command)
        {
            _sendCommandQueue.Enqueue(command);
            Task.Run(SendDataLoopAsync);
        }

        protected void HandleResponse(INetworkCommandAsync response)
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

        protected void SendData(FramePacket packet, bool highPriority = false)
        {
            if (highPriority)
            {
                _priorityFrameQueue.Enqueue(packet);
            }
            else
            {
                _frameQueue.Enqueue(packet);
            }
            Task.Run(SendDataLoopAsync);
        }

        private async Task SendDataLoopAsync()
        {
            if (Interlocked.CompareExchange(ref _sendDataState, (int)TransferState.Busy, (int)TransferState.Idle) == (int)TransferState.Idle)
            {
                try
                {
                    do
                    {                        
                        INetworkCommandAsync command;
                        while (_sendCommandQueue.TryDequeue(out command))
                        {
                            _activeRequests.TryAdd(command, 0);
                            command.WriteRequest(_dataWriter);
                            await _dataWriter.StoreAsync();
                        }

                        FramePacket activeFrameTransfer;
                        while (_priorityFrameQueue.TryDequeue(out activeFrameTransfer))
                        {
                            activeFrameTransfer.WriteData(_dataWriter);
                            await _dataWriter.StoreAsync();                            
                        }

                        if (_frameQueue.TryPeek(out activeFrameTransfer))
                        {
                            // all data were written
                            if (activeFrameTransfer.WriteData(_dataWriter))
                            {
                                _frameQueue.TryDequeue(out activeFrameTransfer);
                            }
                            await _dataWriter.StoreAsync();
                        }
                        await _dataWriter.FlushAsync();

                        // miniscule change to miss data to send - TODO: evaluate refactoring effort
                    } while ((_sendCommandQueue.IsEmpty || _priorityFrameQueue.IsEmpty || _frameQueue.IsEmpty) == false); 
                }
                finally
                {
                    Interlocked.Exchange(ref _sendDataState, (int)TransferState.Idle);
                }

                // TODO: refactor "loop" -- high cpu usage if enabled -- investigate CompilerServices
                //Task.Run(SendDataLoopAsync);
            }
        }
    }
}
