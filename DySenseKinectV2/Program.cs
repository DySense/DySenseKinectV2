using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DySenseKinectV2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string sensorID = args[1];
            Dictionary<string, object> settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(args[2]);
            string connectEndpoint = args[3];

            KinectV2 kinect = new KinectV2(sensorID, settings, connectEndpoint);

            kinect.Run();
        }
        
    }
}
