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
        #region Members

        // How often to save the streams (in seconds). If negative then don't save. 0 captures at max rate.
        double colorCapturePeriod = 0;
        double depthCapturePeriod = 0;
        double irCapturePeriod = 0;

        KinectSensor _sensor;
        MultiSourceFrameReader _reader;

        ImageSource _lastColor;
        ushort[] _depthData;
        ImageSource _lastIR;

        // Time stamps of last read in images.
        double _lastColorTime;
        double _lastDepthTime;
        double _lastIRTime;

        // Where to save output images to.
        string outDirectory;

        #endregion

        public KinectV2(string sensorID, Dictionary<string, object> settings, string connectEndpoint)
            : base(sensorID, connectEndpoint)
        {
            this.outDirectory = Convert.ToString(settings["out_directory"]);
            this.colorCapturePeriod = Convert.ToDouble(settings["color_capture_period"]);
            this.depthCapturePeriod = Convert.ToDouble(settings["depth_capture_period"]);
            this.irCapturePeriod = Convert.ToDouble(settings["ir_capture_period"]);

            base.MaxClosingTime = 3.0; // TODO
            base.MinLoopPeriod = Math.Min(Math.Min(colorCapturePeriod, depthCapturePeriod), irCapturePeriod);
        }

        protected override void Setup()
        {
            _sensor = KinectSensor.GetDefault();

            if (_sensor != null)
            {
                _sensor.Open();

                _reader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared);
                _reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
            }
        }

        protected override void Close()
        {
            if (_reader != null)
            {
                _reader.Dispose();
            }

            if (_sensor != null)
            {
                _sensor.Close();
            }
        }

        protected override bool IsClosed()
        {
            return (_reader == null) && (_sensor == null);
        }

        protected override void ReadNewData()
        {
            bool success = true;

            bool needToSaveColor = (SysTime - _lastColorTime) > colorCapturePeriod;
            bool colorEnabled = colorCapturePeriod >= 0;
            if (colorEnabled && needToSaveColor)
            {
                if (_lastColor != null)
                {
                    string filePath = System.IO.Path.Combine(outDirectory, "Color.jpg");
                    Extensions.WriteJpeg(filePath, 90, (BitmapSource)_lastColor);
                    HandleData(new Dictionary<string, object>() { { "utc_time", Time }, { "sys_time", SysTime }, { "file_path", filePath } });
                    _lastColor = null; // so don't accidentally save same image twice.
                }
                else
                {
                    success = false;
                }
            }

            if (false)
            {
                if (_lastIR != null)
                {
                    Extensions.WriteJpeg(System.IO.Path.Combine(outDirectory, "IR.jpg"), 90, (BitmapSource)_lastIR);
                    _lastIR = null; // so don't accidentally save same image twice.
                }
                else
                {
                    success = false;
                }
            }

            if (false)
            {
                if (_depthData != null)
                {
                    // write out depth data as binary file
                    string depthDataPath = System.IO.Path.Combine(outDirectory, "DepthData.bin");
                    using (FileStream fs = new FileStream(depthDataPath, FileMode.Create, FileAccess.Write))
                    {
                        using (BinaryWriter bw = new BinaryWriter(fs))
                        {
                            foreach (short depthValue in _depthData)
                            {
                                bw.Write(depthValue);
                            }
                        }
                    }
                }
                else
                {
                    success = false;
                }

                Health = success ? "good" : "bad";
            }
        }

        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var reference = e.FrameReference.AcquireFrame();

            // Color
            using (var frame = reference.ColorFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    _lastColorTime = Time;
                    _lastColor = frame.ToBitmap();
                }
            }

            // Depth
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    _lastDepthTime = Time;
                    if (_depthData == null)
                    {
                        int width = frame.FrameDescription.Width;
                        int height = frame.FrameDescription.Height;
                        _depthData = new ushort[width * height];
                    }
                    frame.CopyFrameDataToArray(_depthData);
                }
            }

            // Infrared
            using (var frame = reference.InfraredFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    _lastIRTime = Time;
                    _lastIR = frame.ToBitmap();
                }
            }
        }
    }
}
