using MultiK2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;

namespace DesktopKinect
{
    class Program
    {
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

            Console.WriteLine($"Kinect sensor 2 IsActive: {clientSensor.IsActive}");
            Console.ReadLine();
            Console.ReadLine();
        }
    }
}
