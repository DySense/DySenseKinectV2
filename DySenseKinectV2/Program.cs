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
            string sensorID = "1";
            Dictionary<string, object> settings = new Dictionary<string, object>() { { "out_directory", @"C:\Users\WheatUser\Documents\DySenseKinectV2\DySenseKinectV2Solution\DySenseKinectV2\test_output"},
                                                                                    { "color_capture_period", 2.0 },
                                                                                    { "depth_capture_period", 2.0 },
                                                                                    { "ir_capture_period", 2.0 } };
            string connectEndpoint = "tcp://127.0.0.1:60110";

            //string sensorID = args[1];
            //Dictionary<string, object> settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(args[2]);
            //string connectEndpoint = args[3];

            KinectV2 kinect = new KinectV2(sensorID, settings, connectEndpoint);

            kinect.Run();
        }
        
    }
}
