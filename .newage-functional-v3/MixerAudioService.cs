using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace NewAgeWorship.Desktop
{
    public sealed class AudioHealthSnapshot
    {
        public string DeviceName { get; set; }
        public int SampleRate { get; set; }
        public double Peak { get; set; }
        public double Rms { get; set; }
        public double ClippingPercent { get; set; }
        public bool IsSilent { get; set; }
        public string Status { get; set; }
        public string Summary => Status+" • peak "+(Peak*100).ToString("0")+"% • RMS "+(Rms*100).ToString("0")+"%"+(ClippingPercent>0?" • clip "+ClippingPercent.ToString("0.0")+"%":"")+" • "+SampleRate+" Hz";
    }

    public static class AudioHealthAnalyzer
    {
        public static AudioHealthSnapshot AnalyzePcm16(byte[] buffer,int bytesRecorded,int sampleRate,string deviceName)
        {
            if(buffer==null || bytesRecorded<2) return new AudioHealthSnapshot{DeviceName=deviceName,SampleRate=sampleRate,Status="NO SIGNAL",IsSilent=true};
            long sumSquares=0; int peak=0, clips=0, samples=0;
            for(int i=0;i+1<bytesRecorded;i+=2)
            {
                short sample=(short)(buffer[i] | (buffer[i+1]<<8));
                int abs=sample==short.MinValue?32768:Math.Abs((int)sample);
                peak=Math.Max(peak,abs); if(abs>=32112)clips++; sumSquares+=(long)sample*sample; samples++;
            }
            var peakNorm=samples>0?peak/32768.0:0;
            var rms=samples>0?Math.Sqrt(sumSquares/(double)samples)/32768.0:0;
            var clipPct=samples>0?clips*100.0/samples:0;
            var silent=rms<0.004;
            string status=silent?"SILENT":clipPct>0.5?"CLIPPING":rms<0.02?"LOW LEVEL":"HEALTHY";
            return new AudioHealthSnapshot{DeviceName=deviceName,SampleRate=sampleRate,Peak=peakNorm,Rms=rms,ClippingPercent=clipPct,IsSilent=silent,Status=status};
        }
    }

    public sealed class MixerAudioService : IDisposable
    {
        private WaveInEvent _input;
        public event EventHandler<AudioHealthSnapshot> HealthChanged;
        public bool IsMonitoring => _input!=null;
        public int DeviceCount => WaveIn.DeviceCount;

        public IList<string> GetDevices()
        {
            var devices=new List<string>();
            for(int i=0;i<WaveIn.DeviceCount;i++)
            {
                var caps=WaveIn.GetCapabilities(i);
                devices.Add(i+" • "+caps.ProductName);
            }
            return devices;
        }

        public void Start(int deviceNumber,int sampleRate=48000)
        {
            if(_input!=null) throw new InvalidOperationException("Mixer monitoring is already active.");
            if(WaveIn.DeviceCount<1) throw new InvalidOperationException("No Windows audio input device is available for the mixer feed.");
            if(deviceNumber<0 || deviceNumber>=WaveIn.DeviceCount) throw new ArgumentOutOfRangeException(nameof(deviceNumber));
            var deviceName=WaveIn.GetCapabilities(deviceNumber).ProductName;
            _input=new WaveInEvent{DeviceNumber=deviceNumber,WaveFormat=new WaveFormat(sampleRate,16,1),BufferMilliseconds=200};
            _input.DataAvailable+=(s,e)=>HealthChanged?.Invoke(this,AudioHealthAnalyzer.AnalyzePcm16(e.Buffer,e.BytesRecorded,sampleRate,deviceName));
            _input.RecordingStopped+=(s,e)=>{if(e.Exception!=null)HealthChanged?.Invoke(this,new AudioHealthSnapshot{DeviceName=deviceName,SampleRate=sampleRate,Status="DEVICE ERROR"});};
            _input.StartRecording();
        }

        public void Stop()
        {
            if(_input==null)return; try{_input.StopRecording();}catch{} _input.Dispose();_input=null;
        }
        public void Dispose()=>Stop();
    }
}
