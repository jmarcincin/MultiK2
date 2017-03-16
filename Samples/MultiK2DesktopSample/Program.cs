using MultiK2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;

namespace MultiK2DesktopSample
{
    class Program
    {
        static int lastBodyCount = -1;

        static void Main(string[] args)
        {
            var sensor = Sensor.GetDefaultAsync().AsTask().Result;

            // TODO: rewrite using async/await

            sensor.AllowRemoteClient = true;
            sensor.OpenAsync().AsTask().Wait();

            Console.WriteLine($"Kinect sensor running in {sensor.Type} mode");

            var remoteEndPoint = new EndpointPair(null, string.Empty, new HostName("127.0.0.1"), "8599");
            var clientSensor = Sensor.CreateNetworkSensor(remoteEndPoint);

            Console.WriteLine($"Kinect sensor 2 set-up in {clientSensor.Type} mode");
            clientSensor.OpenAsync().AsTask().Wait();
                        
            var bodyreader = clientSensor.OpenBodyFrameReaderAsync().AsTask().Result;
            Console.WriteLine($"Kinect sensor 2 IsActive: {clientSensor.IsActive}");
            bodyreader.FrameArrived += Bodyreader_FrameArrived;

            
            Console.ReadLine();
            Console.ReadLine();
        }

        private static void Bodyreader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            var bodyCount = e.BodyFrame.Bodies.Count(b => b.IsTracked);
            if (lastBodyCount != bodyCount)
            {
                lastBodyCount = bodyCount;
                Console.WriteLine($"Tracked bodies: {bodyCount}");
            }
        }
    }
}
