using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MultiK2;

namespace MultiK2DesktopClient
{
    class Program
    {
        static int lastBodyCount = -1;

        static void Main(string[] args)
        {
            var clientSensor = Sensor.CreateNetworkSensor("127.0.0.1", 8599);

            Console.WriteLine($"Kinect sensor 2 set-up in {clientSensor.Type} mode");
            clientSensor.OpenAsync().AsTask().Wait();
            Console.WriteLine($"Kinect sensor 2 sensor opened");

            //var bodyreader = clientSensor.OpenBodyFrameReaderAsync().AsTask().Result;
            Console.WriteLine($"Kinect sensor 2 IsActive: {clientSensor.IsActive}");
            //bodyreader.FrameArrived += Bodyreader_FrameArrived;

            var depthreader = clientSensor.OpenDepthFrameReaderAsync().AsTask().Result;
            depthreader.FrameArrived += Depthreader_FrameArrived;

            Console.ReadLine();
            Console.ReadLine();
        }

        private static void Depthreader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            // nop?
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
