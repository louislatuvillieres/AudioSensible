using System;
using System.Runtime.InteropServices;

namespace HearingLossSimulator
{
    public static class AlsaNative
    {
        private const string ALSA_LIB = "libasound.so.2";

        // Types ALSA
        public enum snd_pcm_stream_t
        {
            SND_PCM_STREAM_PLAYBACK = 0,
            SND_PCM_STREAM_CAPTURE = 1
        }

        public enum snd_pcm_access_t
        {
            SND_PCM_ACCESS_RW_INTERLEAVED = 3
        }

        public enum snd_pcm_format_t
        {
            SND_PCM_FORMAT_S16_LE = 2
        }

        // Handles opaques
        public struct snd_pcm_t { public IntPtr handle; }
        public struct snd_pcm_hw_params_t { public IntPtr handle; }

        // Fonctions principales ALSA
        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_open(out snd_pcm_t pcm, string name, snd_pcm_stream_t stream, int mode);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_close(snd_pcm_t pcm);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params_malloc(out snd_pcm_hw_params_t hw_params);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern void snd_pcm_hw_params_free(snd_pcm_hw_params_t hw_params);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params_any(snd_pcm_t pcm, snd_pcm_hw_params_t hw_params);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params_set_access(snd_pcm_t pcm, snd_pcm_hw_params_t hw_params, snd_pcm_access_t access);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params_set_format(snd_pcm_t pcm, snd_pcm_hw_params_t hw_params, snd_pcm_format_t format);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params_set_channels(snd_pcm_t pcm, snd_pcm_hw_params_t hw_params, uint channels);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params_set_rate_near(snd_pcm_t pcm, snd_pcm_hw_params_t hw_params, ref uint rate, ref int dir);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params_set_period_size_near(snd_pcm_t pcm, snd_pcm_hw_params_t hw_params, ref ulong frames, ref int dir);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params_set_buffer_size_near(snd_pcm_t pcm, snd_pcm_hw_params_t hw_params, ref ulong frames);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_hw_params(snd_pcm_t pcm, snd_pcm_hw_params_t hw_params);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_prepare(snd_pcm_t pcm);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_start(snd_pcm_t pcm);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern long snd_pcm_readi(snd_pcm_t pcm, IntPtr buffer, ulong frames);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern long snd_pcm_writei(snd_pcm_t pcm, IntPtr buffer, ulong frames);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_recover(snd_pcm_t pcm, int err, int silent);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_drop(snd_pcm_t pcm);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern int snd_pcm_drain(snd_pcm_t pcm);

        [DllImport(ALSA_LIB, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr snd_strerror(int errnum);

        // Helpers
        public static string GetErrorString(int error)
        {
            IntPtr ptr = snd_strerror(error);
            return Marshal.PtrToStringAnsi(ptr) ?? $"Error {error}";
        }

        public static void CheckError(int result, string operation)
        {
            if (result < 0)
            {
                throw new Exception($"ALSA Error in {operation}: {GetErrorString(result)}");
            }
        }
    }

    // Classe wrapper pour simplifier l'utilisation
    public class AlsaDevice : IDisposable
    {
        private AlsaNative.snd_pcm_t handle;
        private bool isCapture;
        private uint sampleRate;
        private uint channels;
        private ulong periodSize;
        private ulong bufferSize;

        public uint SampleRate => sampleRate;
        public uint Channels => channels;
        public ulong PeriodSize => periodSize;
        public ulong BufferSize => bufferSize;

        public AlsaDevice(string deviceName, bool capture, uint rate, uint chan, ulong period, ulong buffer)
        {
            isCapture = capture;
            sampleRate = rate;
            channels = chan;
            periodSize = period;
            bufferSize = buffer;

            var stream = capture ? AlsaNative.snd_pcm_stream_t.SND_PCM_STREAM_CAPTURE 
                                 : AlsaNative.snd_pcm_stream_t.SND_PCM_STREAM_PLAYBACK;

            int result = AlsaNative.snd_pcm_open(out handle, deviceName, stream, 0);
            AlsaNative.CheckError(result, "snd_pcm_open");

            ConfigureDevice();
        }

        private void ConfigureDevice()
        {
            int result;
            AlsaNative.snd_pcm_hw_params_t hw_params;

            result = AlsaNative.snd_pcm_hw_params_malloc(out hw_params);
            AlsaNative.CheckError(result, "snd_pcm_hw_params_malloc");

            try
            {
                result = AlsaNative.snd_pcm_hw_params_any(handle, hw_params);
                AlsaNative.CheckError(result, "snd_pcm_hw_params_any");

                result = AlsaNative.snd_pcm_hw_params_set_access(handle, hw_params, 
                    AlsaNative.snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED);
                AlsaNative.CheckError(result, "snd_pcm_hw_params_set_access");

                result = AlsaNative.snd_pcm_hw_params_set_format(handle, hw_params, 
                    AlsaNative.snd_pcm_format_t.SND_PCM_FORMAT_S16_LE);
                AlsaNative.CheckError(result, "snd_pcm_hw_params_set_format");

                result = AlsaNative.snd_pcm_hw_params_set_channels(handle, hw_params, channels);
                AlsaNative.CheckError(result, "snd_pcm_hw_params_set_channels");

                int dir = 0;
                uint actualRate = sampleRate;
                result = AlsaNative.snd_pcm_hw_params_set_rate_near(handle, hw_params, ref actualRate, ref dir);
                AlsaNative.CheckError(result, "snd_pcm_hw_params_set_rate_near");
                sampleRate = actualRate;

                ulong actualPeriod = periodSize;
                result = AlsaNative.snd_pcm_hw_params_set_period_size_near(handle, hw_params, ref actualPeriod, ref dir);
                AlsaNative.CheckError(result, "snd_pcm_hw_params_set_period_size_near");
                periodSize = actualPeriod;

                ulong actualBuffer = bufferSize;
                result = AlsaNative.snd_pcm_hw_params_set_buffer_size_near(handle, hw_params, ref actualBuffer);
                AlsaNative.CheckError(result, "snd_pcm_hw_params_set_buffer_size_near");
                bufferSize = actualBuffer;

                result = AlsaNative.snd_pcm_hw_params(handle, hw_params);
                AlsaNative.CheckError(result, "snd_pcm_hw_params");

                result = AlsaNative.snd_pcm_prepare(handle);
                AlsaNative.CheckError(result, "snd_pcm_prepare");
            }
            finally
            {
                AlsaNative.snd_pcm_hw_params_free(hw_params);
            }
        }

        public AlsaNative.snd_pcm_t Handle => handle;

        public void Start()
        {
            if (isCapture)
            {
                int result = AlsaNative.snd_pcm_start(handle);
                AlsaNative.CheckError(result, "snd_pcm_start");
            }
        }

        public int Read(short[] buffer, int frames)
        {
            unsafe
            {
                fixed (short* ptr = buffer)
                {
                    long result = AlsaNative.snd_pcm_readi(handle, (IntPtr)ptr, (ulong)frames);
                    
                    if (result < 0)
                    {
                        // Tentative de récupération
                        AlsaNative.snd_pcm_recover(handle, (int)result, 0);
                        return 0;
                    }
                    
                    return (int)result;
                }
            }
        }

        public int Write(short[] buffer, int frames)
        {
            unsafe
            {
                fixed (short* ptr = buffer)
                {
                    long result = AlsaNative.snd_pcm_writei(handle, (IntPtr)ptr, (ulong)frames);
                    
                    if (result < 0)
                    {
                        // Tentative de récupération
                        AlsaNative.snd_pcm_recover(handle, (int)result, 0);
                        return 0;
                    }
                    
                    return (int)result;
                }
            }
        }

        public void Dispose()
        {
            if (handle.handle != IntPtr.Zero)
            {
                AlsaNative.snd_pcm_drop(handle);
                AlsaNative.snd_pcm_close(handle);
                handle.handle = IntPtr.Zero;
            }
        }
    }
}