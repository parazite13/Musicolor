using PortAudioSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Musicolor
{
    public class Recorder
    {
        public event Action<float[]> OnSampleAvailable;
        private Audio audio;
        private int NUM_CHANNELS = 1;
        public static int SAMPLE_RATE = 44100;
        private uint FRAMES_PER_BUFFER = PortAudio.paFramesPerBufferUnspecified;

        public Recorder()
        {
            try
            {
                Audio.LoggingEnabled = true;
                audio = new Audio(NUM_CHANNELS, 2, SAMPLE_RATE, FRAMES_PER_BUFFER, RecordCallback);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private PortAudio.PaStreamCallbackResult RecordCallback(
             IntPtr input,
             IntPtr output,
             uint frameCount,
             ref PortAudio.PaStreamCallbackTimeInfo timeInfo,
             PortAudio.PaStreamCallbackFlags statusFlags,
             IntPtr userData)
        {
            try
            {
                var callbackBuffer = new float[frameCount];
                Marshal.Copy(input, callbackBuffer, 0, (int)frameCount);
                OnSampleAvailable?.Invoke(callbackBuffer);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return PortAudio.PaStreamCallbackResult.paContinue;
        }

        public void Start()
        {
            audio.Start();
        }

        public void Stop()
        {
            try
            {
                if (audio == null)
                    return;
                audio.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                audio?.Dispose();
            }
        }

        public void Sleep(int timeoutms)
        {
            try
            {
                Thread.Sleep(timeoutms == -1 ? Timeout.Infinite : timeoutms);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
