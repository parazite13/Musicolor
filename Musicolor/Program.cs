using Q42.HueApi.Streaming.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Emgu.CV;
using Emgu.CV.Structure;
using Q42.HueApi.ColorConverters;
using NDesk.Options;

namespace Musicolor
{
    internal static class Program
    {

        private static string bridgeIp = "192.168.1.31";

        private static int requestPerSecond;
        private static int lastKnowRequestPerSecond;
        private static int lastSoundSec = DateTime.Now.Second;

        private static List<float> previousBrights;

        private static RGBColor color;

        private static VideoCapture videoCapture;

        private static bool verbose;

        static void Main(string[] args)
        {
            var showUsage = false;
            var videoUpdateRate = 1;
            var p = new OptionSet()
                .Add("r|rate=", "Rate at which the video input will be grabbed to deduce which color is appropriate (per second)", v => videoUpdateRate = int.Parse(v))
                .Add("v|verbose", v => verbose = v != null)
                .Add("u|usage", v => showUsage = true);
            p.Parse(args);

            if (showUsage)
            {
                Console.WriteLine("USAGE: \n mono Musicolor.exe");
                p.WriteOptionDescriptions(Console.Out);
                return;
            }
                
            Hue.Register(bridgeIp);
            
            while(!Hue.Setup)
            {
                Thread.Sleep(1000);
            }           

            color = new RGBColor(1, 1, 1);

            previousBrights = new List<float>();

            var recorder = new Recorder();
            recorder.OnSampleAvailable += Recorder_OnSampleAvailable; 
            recorder.Start();

            videoCapture = new VideoCapture();
            var videoCaptureTimer = new Timer(FrameUpdateCallback, new object(), 0, 1000 / videoUpdateRate);

            try
            {
                while(true)
                {
                    Thread.Sleep(100);
                }   
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                videoCaptureTimer.Dispose();
                recorder.Stop();
            }
            
        }

        private static void FrameUpdateCallback(object state)
        {
            using (var capture = new Mat())
            {
                videoCapture.Retrieve(capture);
                var image = capture.ToImage<Bgr, byte>();
                color = DominantColor(image);
            }
        }  

        private static float RMS(float[] x)
        {
            return (float)Math.Sqrt(x.Sum(n => n * n) / x.Length);
        }

        private static void Recorder_OnSampleAvailable(float[] sample)
        {
            
            if(verbose) Console.Clear();
            if (lastSoundSec != DateTime.Now.Second)
            {
                if(verbose) Console.WriteLine("Requests per second: " + requestPerSecond);
                lastKnowRequestPerSecond = requestPerSecond;
                requestPerSecond = 0;
                lastSoundSec = DateTime.Now.Second;
            }
            else
            {
                if (verbose) Console.WriteLine("Requests per second : " + lastKnowRequestPerSecond);
                requestPerSecond++;
            }

            
            var rms = RMS(sample);
            var level = (float)Math.Pow(rms * 30, 1 / 2f);
            var bright = Math.Max(1f / 255, level);

            previousBrights.Add(bright);
            if (previousBrights.Count < 5) return;

            previousBrights.RemoveAt(0);
            bright = previousBrights.Average();

            Hue.BaseLayer.SetState(new CancellationToken(), color, bright, default(TimeSpan), true);
            if (verbose) Console.WriteLine("Bright : " + bright);

            if (verbose) Console.WriteLine("Color : " + color.ToHex());
        }

        private static RGBColor DominantColor(Image<Bgr, byte> image)
        {
            int r = 0, g = 0, b = 0; 
            var total = 0;
 
            for (var x = 0; x < image.Width; x++)
            {
                for (var y = 0; y < image.Height; y++)
                {
                    b += image.Data[y, x, 0];
                    g += image.Data[y, x, 1];
                    r += image.Data[y, x, 2];
 
                    total++;
                }
            }
 
            r /= total;
            g /= total;
            b /= total;
 
            return new RGBColor(r, g, b);
        }
    }
}
