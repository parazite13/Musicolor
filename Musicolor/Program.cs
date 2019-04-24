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
        private static int videoUpdatePerSecond;
        private static int lastKnownVideoUpdatePerSecond;
        private static int lastKnownRequestPerSecond;
        private static int lastSoundSec = DateTime.Now.Second;
        private static int lastVideoSec = DateTime.Now.Second;

        private static List<float> previousBrights;

        private static RGBColor color;

        private static VideoCapture videoCapture;

        private static bool verbose;
        private static int nbBrighsForAverage;

        static void Main(string[] args)
        {
            var showUsage = false;
            var videoUpdateRate = 1;
            nbBrighsForAverage = 5;
            var p = new OptionSet()
                .Add("r|rate=", "Maximun rate at which the video input will be grabbed to deduce which color is appropriate per second (default : 1)", v => videoUpdateRate = int.Parse(v))
                .Add("b|bright=", "Specify how many previous sample will be used to compute the average bright (default : 5)", v => nbBrighsForAverage = int.Parse(v))
                .Add("v|verbose", v => verbose = v != null)
                .Add("h|help", v => showUsage = true);
            p.Parse(args);

            if (showUsage)
            {
                Console.WriteLine("USAGE: \n mono Musicolor.exe");
                p.WriteOptionDescriptions(Console.Out);
                return;
            }
                
            Hue.Setup(bridgeIp);
            
            while(!Hue.Ready)
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
            
            Console.Clear();

            try
            {
                ConsoleKeyInfo cki;
                do
                {
                    Thread.Sleep(100);
                    cki = Console.ReadKey();
                }
                while (cki.Key != ConsoleKey.Escape);   
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                videoCaptureTimer.Dispose();
                Hue.Dispose();
                recorder.Stop();
            }
            
        }

        private static void FrameUpdateCallback(object state)
        {
            if(lastVideoSec != DateTime.Now.Second)
            {
                lastKnownVideoUpdatePerSecond = videoUpdatePerSecond;
                videoUpdatePerSecond = 0;
                lastVideoSec = DateTime.Now.Second;
            }

            using (var capture = new Mat())
            {
                videoCapture.Retrieve(capture);
                var image = capture.ToImage<Bgr, byte>();
                color = DominantColor(image);
            }

            videoUpdatePerSecond++;
        }  

        private static void Recorder_OnSampleAvailable(float[] sample)
        {
            if (lastSoundSec != DateTime.Now.Second)
            {    
                lastKnownRequestPerSecond = requestPerSecond;
                requestPerSecond = 0;
                lastSoundSec = DateTime.Now.Second;
            }

            var rms = RMS(sample);
            var level = (float)Math.Pow(rms * 30, 1 / 2f);
            var bright = Math.Max(1f / 255, level);

            previousBrights.Add(bright);
            if (previousBrights.Count < nbBrighsForAverage) return;

            previousBrights.RemoveAt(0);
            bright = previousBrights.Average();

            Hue.BaseLayer.SetState(new CancellationToken(), color, bright);

            requestPerSecond++;

            if(verbose)
            {
                Console.SetCursorPosition(0, 0);
                Console.Write("################################");
                Console.SetCursorPosition(0, 1);
                Console.Write("Requests per second: " + lastKnownRequestPerSecond);
                Console.SetCursorPosition(0, 2);
                Console.Write("Video updates per second: " + lastKnownVideoUpdatePerSecond);
                Console.SetCursorPosition(0, 3);
                Console.Write("Bright : " + bright);
                Console.SetCursorPosition(0, 4);
                Console.Write("Color : " + color.ToHex());
                Console.SetCursorPosition(0, 5);
                Console.WriteLine("################################");
            }

        }

        private static float RMS(float[] x)
        {
            return (float)Math.Sqrt(x.Sum(n => n * n) / x.Length);
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
