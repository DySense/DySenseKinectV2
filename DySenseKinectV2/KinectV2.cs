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

        // How much time elapses between when image is captured and when it is received (in seconds).
        double sensorLatency;

        // Microsoft SDK object references.
        KinectSensor sensor;
        MultiSourceFrameReader reader;

        // Save depth data array so we can re-use it.
        ushort[] depthData;

        // Size of depth data reported by sensor.
        int depthDataWidth;
        int depthDataHeight;

        // System time stamps of last read in images/data.
        double lastColorTime = 0;
        double lastDepthTime = 0;
        double lastInfraredTime = 0;

        // Number of each type of stream that's been handled.
        int numColorHandled = 0;
        int numDepthHandled = 0;
        int numInfraredHandled = 0;

        public KinectV2(string sensorID, string instrumentID, Dictionary<string, object> settings, string connectEndpoint)
            : base(sensorID, instrumentID, connectEndpoint, decideTimeout: false)
        {
            this.defaultDataFileDirectory = Convert.ToString(settings["out_directory"]);
            this.colorCapturePeriod = Convert.ToDouble(settings["color_period"]);
            this.depthCapturePeriod = Convert.ToDouble(settings["depth_period"]);
            this.irCapturePeriod = Convert.ToDouble(settings["ir_period"]);
            this.sensorLatency = Convert.ToDouble(settings["sensor_latency"]) / 1000.0;

            this.colorEnabled = colorCapturePeriod >= 0;
            this.depthEnabled = depthCapturePeriod >= 0;
            this.infraredEnabled = irCapturePeriod >= 0;

            // Here the read period is how often the validation check is done on if data is still being received.
            // The actual sensor reading is done in the event handler.
            base.DesiredReadPeriod = 0.5; // seconds
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

            if (!String.IsNullOrWhiteSpace(CurrentDataFileDirectory) && !Directory.Exists(CurrentDataFileDirectory))
            {
                Directory.CreateDirectory(CurrentDataFileDirectory);
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

        protected override string ReadNewData()
        {
            // Sensor data is recorded in the new frame event callback. This just checks if data is still coming in.
            bool receivingOk = VerifyReceivingData();

            if (SecondSinceSensorSetup < 3.0)
            {
                // Give the sensor time to startup at the beginning before complaining that we're not receiving data.
                receivingOk = true;
            }

            return receivingOk ? "normal" : "timed_out";
        }

        protected override void DriverHandleNewSetting(string settingName, object settingValue)
        {
            if (settingName == "data_file_directory")
            {
                // Data file directory might have changed so make sure directory exists.
                if (!String.IsNullOrWhiteSpace(CurrentDataFileDirectory) && !Directory.Exists(CurrentDataFileDirectory))
                {
                    Directory.CreateDirectory(CurrentDataFileDirectory);
                }
            }
        }

        protected override void HandleSpecialCommand(string commandName, object commandArgs)
        {
            if (commandName == "report_distance")
            {
                ReportLastDistance();
            }
        }

        bool VerifyReceivingData()
        {
            // Need to use Math.Max() since the period could be 0 to signify max rate.
            bool receivingOk = true;
            if (colorEnabled)
            {
                double timeSinceLastReceived = SysTime - lastColorTime;
                receivingOk = receivingOk && (timeSinceLastReceived < (2 * Math.Max(colorCapturePeriod, 0.1)));
            }
            if (depthEnabled)
            {
                double timeSinceLastReceived = SysTime - lastDepthTime;
                receivingOk = receivingOk && (timeSinceLastReceived < (2 * Math.Max(depthCapturePeriod, 0.1)));
            }
            if (infraredEnabled)
            {
                double timeSinceLastReceived = SysTime - lastInfraredTime;
                receivingOk = receivingOk && (timeSinceLastReceived < (2 * Math.Max(irCapturePeriod, 0.1)));
            }

            return receivingOk;
        }

        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // Grab a time reference right away to minimize the delay between capturing an image and timestamping it.
            // Doing this once at the beginning will also make it so processing one type image won't affect the time of another.
            // Need to subtract off the estimated amount of time the image took to process and arrive.
            double captureUtcTime = UtcTime - this.sensorLatency;
            double captureSysTime = SysTime - this.sensorLatency;

            var reference = e.FrameReference.AcquireFrame();
            
            // Color
            bool needToSaveColor = (captureSysTime - lastColorTime) > colorCapturePeriod;
            if (colorEnabled && needToSaveColor)
            {
                using (var frame = reference.ColorFrameReference.AcquireFrame())
                {
                    if (frame != null)
                    {
                        lastColorTime = captureSysTime;

                        string fileName = uniqueFileName(InstrumentID, captureUtcTime, "C", numColorHandled, "jpg");
                        if (ShouldRecordData()) 
                        {
                            SaveColorImage(frame, fileName);
                        }
                        HandleData(captureUtcTime, SysTime, new List<object>() { "color", fileName });
                        numColorHandled++;
                    }
                }
            }

            // Depth
            bool needToSaveDepth = (captureSysTime - lastDepthTime) > depthCapturePeriod;
            if (depthEnabled && needToSaveDepth)
            {
                using (var frame = reference.DepthFrameReference.AcquireFrame())
                {
                    if (frame != null)
                    {
                        lastDepthTime = captureSysTime;
                        // If this is the first depth data we've gotten then initialize our array since we know how big the width/height is. More flexible than hardcoding.
                        // Should be 512 x 424
                        if (depthData == null)
                        {
                            depthDataWidth = frame.FrameDescription.Width;
                            depthDataHeight = frame.FrameDescription.Height;
                            depthData = new ushort[depthDataWidth * depthDataHeight];
                        }

                        string fileName = uniqueFileName(InstrumentID, captureUtcTime, "D", numDepthHandled, "bin");
                        if (ShouldRecordData())
                        {
                            SaveDepthData(depthData, fileName);
                        }
                        // Copy data regardless of saving so we can report distance reading if user asks for it.
                        frame.CopyFrameDataToArray(depthData);
                        HandleData(captureUtcTime, SysTime, new List<object>() { "depth", fileName });
                        numDepthHandled++;
                    }
                }
            }

            // Infrared
            bool needToSaveInfrared = (captureSysTime - lastInfraredTime) > irCapturePeriod;
            if (infraredEnabled && needToSaveInfrared)
            {
                using (var frame = reference.InfraredFrameReference.AcquireFrame())
                {
                    if (frame != null)
                    {
                        lastInfraredTime = captureSysTime;

                        string fileName = uniqueFileName(InstrumentID, captureUtcTime, "I", numInfraredHandled, "jpg");
                        if (ShouldRecordData())
                        {
                            SaveInfraredImage(frame, fileName);
                        }
                        HandleData(captureUtcTime, SysTime, new List<object>() { "ir", fileName });
                        numInfraredHandled++;
                    }
                }
            }
        }

        string SaveColorImage(ColorFrame frame, string fileName)
        {
            if (String.IsNullOrWhiteSpace(CurrentDataFileDirectory))
            {
                SendText("Can't save file. No valid directory.");
            }
            ImageSource image = frame.ToBitmap();
            string filePath = System.IO.Path.Combine(CurrentDataFileDirectory, fileName);
            Extensions.WriteJpeg(filePath, 90, (BitmapSource)image);
            return filePath;
        }

        string SaveInfraredImage(InfraredFrame frame, string fileName)
        {
            if (String.IsNullOrWhiteSpace(CurrentDataFileDirectory))
            {
                SendText("Can't save file. No valid directory.");
            }
            ImageSource image = frame.ToBitmap();
            string filePath = System.IO.Path.Combine(CurrentDataFileDirectory, fileName);
            Extensions.WriteJpeg(filePath, 90, (BitmapSource)image);
            return filePath;
        }

        string SaveDepthData(ushort[] depthData, string fileName)
        {
            if (String.IsNullOrWhiteSpace(CurrentDataFileDirectory))
            {
                SendText("Can't save file. No valid directory.");
            }
            // write out depth data as binary file
            string depthDataPath = System.IO.Path.Combine(CurrentDataFileDirectory, fileName);
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

        void ReportLastDistance()
        {
            if (depthData == null)
            {
                SendText("No depth image received yet.");
                return;
            }

            // Define a square patch of readings in the middle of the depth image
            // to sample from.  The side length is the side of this patch.
            int sideLength = (int)(0.1 * Math.Min(depthDataWidth, depthDataHeight));
            int startRow = (depthDataHeight / 2) - (sideLength / 2);
            int endRow = (depthDataHeight / 2) + (sideLength / 2);
            int startColumn = (depthDataWidth / 2) - (sideLength / 2);
            int endColumn = (depthDataWidth / 2) + (sideLength / 2);

            uint depthSum = 0;
            uint numSamples = 0;

            for (int r = startRow; r < endRow; ++r)
            {
                for (int c = startColumn; c < endColumn; ++c)
                {
                    UInt16 depthValue = depthData[r * depthDataWidth + c];
                    if (depthValue == 0)
                    {
                        continue; // No depth data returned for this pixel.
                    }
                    depthSum += depthValue;
                    numSamples++;
                }
            }

            if (depthSum == 0)
            {
                SendText("No valid distance readings found in center of depth image.");
                return;
            }

            double averageDepth = Convert.ToDouble(depthSum) / numSamples;

            // Convert from millimeters to meters.
            averageDepth /= 1000.0;

            SendText(String.Format("Last distance {0:0.000} meters recorded {1:0.00} seconds ago.", averageDepth, SysTime - lastDepthTime));
        }

        string uniqueFileName(string id, double utcTime, string streamType, int fileNumber, string fileExtension)
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(utcTime);
            string formattedTime = dateTime.ToString("yyyyMMdd_hhmmss"); 

            return String.Format("{0}_{1}_{2}_{3}.{4}", id, formattedTime, streamType, fileNumber, fileExtension); 
        }
    }
}
