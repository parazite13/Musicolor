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
        private static readonly int NUM_CHANNELS = 1;
        public static readonly int DEFAULT_SAMPLE_RATE = 44100;
        public static readonly uint DEFAULT_FRAMES_PER_BUFFER = 2048;

        public int SampleRate { get; private set; }
        public uint Buffer { get; private set; }
        public bool IsRecording { get; private set; }

        private DateTime lastUpdate;

        public Recorder(int samplerate, uint buffer)
        {
            try
            {
                Audio.LoggingEnabled = true;
                SampleRate = samplerate;
                Buffer = buffer;
                IsRecording = false;
                audio = new Audio(NUM_CHANNELS, 2, samplerate, buffer, RecordCallback);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public Recorder() : this(DEFAULT_SAMPLE_RATE, DEFAULT_FRAMES_PER_BUFFER) { }

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
                if(!IsRecording)
                {
                    IsRecording = true;
                    new Thread(CheckIfAlive).Start();
                }
                lastUpdate = DateTime.Now;
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
            lastUpdate = DateTime.Now;
            audio.Start();
        }

        private void CheckIfAlive()
        {
            while(IsRecording)
            {
                Thread.Sleep(500);
                if (DateTime.Now - lastUpdate > TimeSpan.FromSeconds(0.5))
                {
                    audio.Dispose();
                    IsRecording = false;
                }
            }
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
                IsRecording = false;
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
