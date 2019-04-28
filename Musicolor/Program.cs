using Q42.HueApi.Streaming.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Emgu.CV;
using Emgu.CV.Structure;
using Q42.HueApi.ColorConverters;
using NDesk.Options;
using System.Threading.Tasks;

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
        private static int restartCount;

        private static List<float> previousBrights;

        private static RGBColor color;

        private static VideoCapture videoCapture;

        private static bool verbose;
        private static bool autoRestart;
        private static int nbBrighsForAverage;

        static void Main(string[] args)
        {
            var showUsage = false;
            var samplerate = Recorder.DEFAULT_SAMPLE_RATE;
            var buffer = Recorder.DEFAULT_FRAMES_PER_BUFFER;
            autoRestart = false;
            nbBrighsForAverage = 5;
            var p = new OptionSet()
                .Add("ar|auto_restart", "If present the recorder will automatically restart if an underrun occurs", v => autoRestart = v != null)
                .Add("s|samplerate=", "Samplerate that will be used for the recording (default : " + Recorder.DEFAULT_SAMPLE_RATE + ")", v => samplerate = int.Parse(v))
                .Add("b|buffer=", "Buffer size that will be used for the recording (default : " + Recorder.DEFAULT_FRAMES_PER_BUFFER + ")", v => buffer = uint.Parse(v))
                .Add("ab|average_bright=", "Specify how many previous sample will be used to compute the average bright (default : 5)", v => nbBrighsForAverage = int.Parse(v))
                .Add("v|verbose", v => verbose = v != null)
                .Add("h|help", v => showUsage = v != null);
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
            restartCount = 0;
            previousBrights = new List<float>();

            var recorder = new Recorder(samplerate, buffer);
            recorder.OnSampleAvailable += Recorder_OnSampleAvailable; 
            recorder.Start();
            
            videoCapture = new VideoCapture();
            videoCapture.ImageGrabbed += VideoCapture_ImageGrabbed;
            videoCapture.Start();

            Console.Clear();

            try
            {
                var exitRequested = false;
                Task.Factory.StartNew(() =>
                {
                    while (Console.ReadKey().Key != ConsoleKey.Escape)
                    {
                    }

                    exitRequested = true;
                });

                while(!exitRequested)
                {
                    Thread.Sleep(200);

                    if(!recorder.IsRecording)
                    {
                        if(autoRestart)
                        {
                            Console.WriteLine("Restart recording !");
                            Thread.Sleep(1000);
                            recorder = new Recorder();
                            recorder.OnSampleAvailable += Recorder_OnSampleAvailable;
                            recorder.Start();
                            restartCount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    Console.Clear();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                videoCapture.Stop();
                Hue.Dispose();
                recorder.Stop();
            }
            
        }

        private static void VideoCapture_ImageGrabbed(object sender, EventArgs e)
        {
            using (var frame = new Mat())
            {
                videoCapture.Retrieve(frame);
                color = DominantColor(frame.ToImage<Bgr, byte>());
                videoUpdatePerSecond++;
            }
        }

        private static void Recorder_OnSampleAvailable(float[] sample)
        {
            if (lastVideoSec != DateTime.Now.Second)
            {
                lastKnownVideoUpdatePerSecond = videoUpdatePerSecond;
                videoUpdatePerSecond = 0;
                lastVideoSec = DateTime.Now.Second;
            }

            if (lastSoundSec != DateTime.Now.Second)
            {    
                lastKnownRequestPerSecond = requestPerSecond;
                requestPerSecond = 0;
                lastSoundSec = DateTime.Now.Second;
            }

            var scale = 50;
            var exponent = 4;
            
            var rms = Rms(sample);
            var level = (float)Math.Pow(Math.Min(rms * scale, 1.0), exponent);
            var bright = Math.Max(0.1f, level);

            previousBrights.Add(bright);
            if (previousBrights.Count < nbBrighsForAverage) return;

            previousBrights.RemoveAt(0);
            bright = previousBrights.Average();

            Hue.BaseLayer.GetLeft().SetState(new CancellationToken(), color, bright);
            Hue.BaseLayer.GetRight().SetState(new CancellationToken(), color, bright);
            Hue.BaseLayer.GetCenter().SetState(new CancellationToken(), color, 1f);

            //Hue.BaseLayer.SetState(new CancellationToken(), color, bright);

            requestPerSecond++;

            if(verbose)
            {
                Console.SetCursorPosition(0, 0);
                Console.Write("#########################################");
                Console.SetCursorPosition(0, 1);
                Console.Write("Requests per second:            ");
                Console.SetCursorPosition(30, 1);
                Console.Write(lastKnownRequestPerSecond);
                Console.SetCursorPosition(0, 2);
                Console.Write("Video updates per second:       ");
                Console.SetCursorPosition(30, 2);
                Console.Write(lastKnownVideoUpdatePerSecond);
                Console.SetCursorPosition(0, 3);
                Console.Write("Bright :                        ");
                Console.SetCursorPosition(30, 3);
                Console.Write(bright);
                Console.SetCursorPosition(0, 4);
                Console.Write("Color :                         ");
                Console.SetCursorPosition(30, 4);
                Console.Write("#" + color.ToHex());
                Console.SetCursorPosition(0, 5);
                if(autoRestart)
                {
                    Console.Write("Underruns restart count :       ");
                    Console.SetCursorPosition(30, 5);
                    Console.Write(restartCount);
                    Console.SetCursorPosition(0, 6);
                }
                else
                {
                    Console.SetCursorPosition(0, 5);
                }
                Console.WriteLine("#########################################");
            }

        }

        private static float Rms(IReadOnlyCollection<float> x)
        {
            return (float)Math.Sqrt(x.Sum(n => n * n) / x.Count);
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
