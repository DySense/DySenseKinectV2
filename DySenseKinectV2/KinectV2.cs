using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using DySenseKinectV2;
using DySense;

namespace DySenseKinectV2
{
    public class KinectV2 : SensorBase
    {
        // How often to save the streams (in seconds). If negative then don't save. 0 captures at max rate.
        double colorCapturePeriod = 0;
        double depthCapturePeriod = 0;
        double irCapturePeriod = 0;

        // Set to false if the stream shouldn't be saved at all.
        bool colorEnabled = true;
        bool depthEnabled = true;
        bool infraredEnabled = true;

        // Microsoft SDK object references.
        KinectSensor sensor;
        MultiSourceFrameReader reader;

        // Save depth data array so we can re-use it.
        ushort[] depthData;

        // Time stamps of last read in images/data.
        double lastColorTime = 0;
        double lastDepthTime = 0;
        double lastInfraredTime = 0;

        // Where to save output images to.
        string outDirectory;

        public KinectV2(string sensorID, Dictionary<string, object> settings, string connectEndpoint)
            : base(sensorID, connectEndpoint)
        {
            this.outDirectory = Convert.ToString(settings["out_directory"]);
            this.colorCapturePeriod = Convert.ToDouble(settings["color_period"]);
            this.depthCapturePeriod = Convert.ToDouble(settings["depth_period"]);
            this.irCapturePeriod = Convert.ToDouble(settings["ir_period"]);

            this.colorEnabled = colorCapturePeriod >= 0;
            this.depthEnabled = depthCapturePeriod >= 0;
            this.infraredEnabled = irCapturePeriod >= 0;

            base.MaxClosingTime = 4.0; // seconds
        }

        protected override void Setup()
        {
            sensor = KinectSensor.GetDefault();

            if (sensor != null)
            {
                sensor.Open();

                reader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared);
                reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
            }

            if (!Directory.Exists(outDirectory))
            {
                Directory.CreateDirectory(outDirectory);
            }
        }

        protected override void Close()
        {
            if (reader != null)
            {
                reader.Dispose();
            }

            if (sensor != null)
            {
                sensor.Close();
            }
        }

        protected override bool IsClosed()
        {
            return (reader == null) && (sensor == null);
        }

        protected override void ReadNewData()
        {
            // Sensor data is recorded in the new frame event callback. This just checks if data is still coming in.
            bool receivingOk = VerifyReceivingData();

            Health = receivingOk ? "good" : "bad";

            WaitForNextLoop();
        }

        bool VerifyReceivingData()
        {
            // Need to use Math.Max() since the period could be 0 to signify max rate.
            bool receivingOk = true;
            if (colorEnabled)
            {
                double timeSinceLastReceived = Time - lastColorTime;
                receivingOk = timeSinceLastReceived < (2 * Math.Max(colorCapturePeriod, 0.1));
            }
            if (depthEnabled)
            {
                double timeSinceLastReceived = Time - lastDepthTime;
                receivingOk = timeSinceLastReceived < (2 *  Math.Max(depthCapturePeriod, 0.1));
            }
            if (infraredEnabled)
            {
                double timeSinceLastReceived = Time - lastInfraredTime;
                receivingOk = timeSinceLastReceived < (2 * Math.Max(irCapturePeriod, 0.1));
            }

            return receivingOk;
        }

        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // Grab a time reference right away to minimize the delay between capturing an image and timestamping it.
            // Doing this once at the beginning will also make it so processing one type image won't affect the time of another.
            double captureTime = Time;

            var reference = e.FrameReference.AcquireFrame();
            
            // Color
            bool needToSaveColor = (Time - lastColorTime) > colorCapturePeriod;
            if (colorEnabled && needToSaveColor)
            {
                using (var frame = reference.ColorFrameReference.AcquireFrame())
                {
                    if (frame != null)
                    {
                        lastColorTime = captureTime;

                        if (ShouldRecordData()) 
                        {
                            string fileName = uniqueFileName(reference, "COLOR", "jpg");
                            SaveColorImage(frame, fileName);
                            HandleData(new Dictionary<string, object>() { { "utc_time", lastColorTime }, { "sys_time", SysTime }, { "type", "color" }, { "file_name", fileName } });
                        }
                    }
                }
            }

            // Depth
            bool needToSaveDepth = (Time - lastDepthTime) > depthCapturePeriod;
            if (depthEnabled && needToSaveDepth)
            {
                using (var frame = reference.DepthFrameReference.AcquireFrame())
                {
                    if (frame != null)
                    {
                        lastDepthTime = captureTime;
                        // If this is the first depth data we've gotten then initialize our array since we know how big the width/height is. More flexible than hardcoding.
                        if (depthData == null)
                        {
                            int width = frame.FrameDescription.Width;
                            int height = frame.FrameDescription.Height;
                            depthData = new ushort[width * height];
                        }

                        if (ShouldRecordData())
                        {
                            frame.CopyFrameDataToArray(depthData);
                            string fileName = uniqueFileName(reference, "DEPTH", "bin");
                            SaveDepthData(depthData, fileName);
                            HandleData(new Dictionary<string, object>() { { "utc_time", lastDepthTime }, { "sys_time", SysTime }, { "type", "depth" }, { "file_name", fileName } });
                        }
                    }
                }
            }

            // Infrared
            bool needToSaveInfrared = (Time - lastInfraredTime) > irCapturePeriod;
            if (infraredEnabled && needToSaveInfrared)
            {
                using (var frame = reference.InfraredFrameReference.AcquireFrame())
                {
                    if (frame != null)
                    {
                        lastInfraredTime = captureTime;

                        if (ShouldRecordData())
                        {
                            string fileName = uniqueFileName(reference, "IR", "jpg");
                            SaveInfraredImage(frame, fileName);
                            HandleData(new Dictionary<string, object>() { { "utc_time", lastInfraredTime }, { "sys_time", SysTime }, { "type", "infrared" }, { "file_name", fileName } });
                        }
                    }
                }
            }
        }

        string SaveColorImage(ColorFrame frame, string fileName)
        {
            ImageSource image = frame.ToBitmap();
            string filePath = System.IO.Path.Combine(outDirectory, fileName);
            Extensions.WriteJpeg(filePath, 90, (BitmapSource)image);
            return filePath;
        }

        string SaveInfraredImage(InfraredFrame frame, string fileName)
        {
            ImageSource image = frame.ToBitmap();
            string filePath = System.IO.Path.Combine(outDirectory, fileName);
            Extensions.WriteJpeg(filePath, 90, (BitmapSource)image);
            return filePath;
        }

        string SaveDepthData(ushort[] depthData, string fileName)
        {
            // write out depth data as binary file
            string depthDataPath = System.IO.Path.Combine(outDirectory, fileName);
            using (FileStream fs = new FileStream(depthDataPath, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    foreach (short depthValue in depthData)
                    {
                        bw.Write(depthValue);
                    }
                }
            }
            return depthDataPath;
        }

        string uniqueFileName(MultiSourceFrame reference, string streamType, string fileExtension)
        {
            string sensorID = reference.KinectSensor.UniqueKinectId;
            string formattedTime = DateTime.UtcNow.ToString("yyyyMMdd_hhmmss_fff");
            return String.Format("KIN_{0}_{1}_{2}.{3}", sensorID, formattedTime, streamType, fileExtension); 
        }
    }
}
