﻿using System.Text;
using System.Runtime.InteropServices;
using System;

namespace Wasapi {
    public class WasapiCS {

#region native methods
        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Init(ref int instanceIdReturn);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_Term(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_EnumerateDevices(int instanceId, int deviceType);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetDeviceCount(int instanceId);

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet=CharSet.Unicode)]
        internal struct WasapiIoDeviceAttributes {
            public int    deviceId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String deviceIdString;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_GetDeviceAttributes(int instanceId, int deviceId, out WasapiIoDeviceAttributes attr);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct MixFormatArgs {
            public int sampleRate;
            public int sampleFormat;    ///< WWPcmDataSampleFormatType
            public int numChannels;
            public int dwChannelMask;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetMixFormat(int instanceId, int deviceId, out MixFormatArgs mixFormat);


        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct DevicePeriodArgs {
            public long defaultPeriod;
            public long minimumPeriod;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetDevicePeriod(int instanceId, int deviceId, out DevicePeriodArgs devicePeriod);


        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct InspectArgs {
            public int deviceType;      ///< DeviceType
            public int sampleRate;
            public int sampleFormat;    ///< WWPcmDataSampleFormatType
            public int numChannels;
            public int dwChannelMask;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_InspectDevice(int instanceId, int deviceId, ref InspectArgs args);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct SetupArgs {
            public int deviceType;
            public int streamType;
            public int sampleRate;
            public int sampleFormat;
            public int numChannels;

            public int dwChannelMask;
            public int shareMode;
            public int mmcssCall; ///< 0: disable, 1: enable, 2: do not call DwmEnableMMCSS()
            public int mmThreadPriority; ///< 0: None, 1: Low, 2: Normal, 3: High, 4: Critical
            public int schedulerTask;

            public int dataFeedMode;
            public int latencyMillisec;
            public int timePeriodHandledNanosec;
            public int zeroFlushMillisec;
            public int flags;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Setup(int instanceId, int deviceId, ref SetupArgs args);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_Unsetup(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmDataStart(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmData(int instanceId, int pcmId, byte[] data, long bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmDataSetPcmFragment(int instanceId, int pcmId, long posBytes, byte[] data, long bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_ResampleIfNeeded(int instanceId, int conversionQuality);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmDataEnd(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static double
        WasapiIO_ScanPcmMaxAbsAmplitude(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_ScalePcmAmplitude(int instanceId, double scale);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_ClearPlayList(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_RemovePlayPcmDataAt(int instanceId, int pcmId);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetPlayRepeat(int instanceId, bool repeat);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_ConnectPcmDataNext(int instanceId, int fromIdx, int toIdx);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetPcmDataId(int instanceId, int usageType);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetNowPlayingPcmDataId(int instanceId, int pcmId);

        [DllImport("WasapiIODLL.dll")]
        private extern static long
        WasapiIO_GetCaptureGlitchCount(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_ResetCaptureGlitchCount(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_StartPlayback(int instanceId, int wavDataId);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_StartRecording(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_Run(int instanceId, int millisec);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Stop(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Pause(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Unpause(int instanceId);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_SetPosFrame(int instanceId, long v);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct WasapiIoSessionStatus {
            public int streamType;
            public int pcmDataSampleRate;
            public int deviceSampleRate;
            public int deviceSampleFormat;
            public int deviceBytesPerFrame;
            public int deviceNumChannels;
            public int timePeriodHandledNanosec;
            public int bufferFrameNum;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_GetSessionStatus(int instanceId, out WasapiIoSessionStatus a);

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct WasapiIoCursorLocation {
            public long posFrame;
            public long totalFrameNum;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_GetPlayCursorPosition(int instanceId, int usageType, out WasapiIoCursorLocation a);

        public enum WasapiDeviceState {
            Active = 1,
            Disabled = 2,
            NotPresent = 4,
            Unplugged = 8,
        };

        /// <summary>
        /// デバイスが消えたとかのイベント。
        /// </summary>
        /// <param name="idStr">デバイスのID。</param>
        /// <param name="dwNewState">WasapiDeviceState型の値のOR</param>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate void StateChangedCallback(StringBuilder idStr, int dwNewState);

        [DllImport("WasapiIODLL.dll")]
        private static extern void WasapiIO_RegisterStateChangedCallback(int instanceId, StateChangedCallback callback);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void NativeCaptureCallback(IntPtr data, int bytes);

        public delegate void CaptureCallback(byte[] data);

        [DllImport("WasapiIODLL.dll")]
        private static extern void WasapiIO_RegisterCaptureCallback(int instanceId, NativeCaptureCallback callback);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void NativeRenderCallback(IntPtr data, int bytes);

        public delegate byte [] RenderCallback(int wantBytes);

        [DllImport("WasapiIODLL.dll")]
        private static extern void WasapiIO_RegisterRenderCallback(int instanceId, NativeRenderCallback callback);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct WasapiIoWorkerThreadSetupResult {
            public int dwmEnableMMCSSResult;
            public int avSetMmThreadCharacteristicsResult;
            public int avSetMmThreadPriorityResult;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_GetWorkerThreadSetupResult(int instanceId, out WasapiIoWorkerThreadSetupResult result);

        [DllImport("WasapiIODLL.dll", CharSet = CharSet.Unicode)]
        private extern static void
        WasapiIO_AppendAudioFilter(int instanceId, int audioFilterType, string args);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_ClearAudioFilter(int instanceId);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct WasapiIoVolumeParams {
            public float levelMinDB;
            public float levelMaxDB;
            public float volumeIncrementDB;
            public float defaultLevel;
            /// ENDPOINT_HARDWARE_SUPPORT_VOLUME ==1
            /// ENDPOINT_HARDWARE_SUPPORT_MUTE   ==2
            /// ENDPOINT_HARDWARE_SUPPORT_METER  ==4
            public int hardwareSupport;
        };

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetVolumeParams(int instanceId, out WasapiIoVolumeParams args);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_SetMasterVolumeInDb(int instanceId, float db);

#endregion

        public enum MMCSSCallType {
            Disable,
            Enable,
            DoNotCall
        };

        public enum MMThreadPriorityType {
            None,
            Low,
            Normal,
            High,
            Critical
        };

        public enum SchedulerTaskType {
            None,
            Audio,
            ProAudio,
            Playback
        };

        public enum ShareMode {
            Shared,
            Exclusive
        };

        public enum DataFeedMode {
            EventDriven,
            TimerDriven,
        };

        public enum DeviceType {
            Play,
            Rec
        };

        /// <summary>
        /// enum項目はPcmData.ValueRepresentationTypeと同じ順番で並べる。
        /// WasapiPcmUtilのVrtToBftも参照。
        /// </summary>
        public enum BitFormatType {
            SInt,
            SFloat
        };

        /// <summary>
        /// 注: 項目を追加したら以下のEnumも追加。
        /// WWPcmDataSampleFormatType
        /// 以下の関数に追加する。
        /// WWPcmDataSampleFormatTypeToStr
        /// WWPcmDataSampleFormatTypeToBitsPerSample
        /// WWPcmDataSampleFormatTypeToBytesPerSample
        /// WWPcmDataSampleFormatTypeToValidBitsPerSample
        /// WWPcmDataSampleFormatTypeIsFloat
        /// WWPcmDataSampleFormatTypeIsInt
        /// WWPcmDataSampleFormatTypeGenerate
        /// </summary>
        public enum SampleFormatType {
            Unknown = -1,
            
            Sint16,
            Sint24,
            Sint32V24,
            Sint32,
            Sfloat,

            Sdouble, //< WASAPIはサポートしないが便宜上用意する
        };

        public enum StreamType {
            PCM,
            DoP,
        };

        /// <summary>
        /// WWAudioFilterType.hと同じ順番で並べる
        /// </summary>
        public enum WWAudioFilterType {
            PolarityInvert,
            Monaural,
            ChannelMapping,
            MuteChannel,
            SoloChannel,

            ZohNosdacCompensation,
            Delay,
            DeEmphasis,
        };

        /// <summary>
        /// サンプルフォーマットタイプ→メモリ上に占めるビット数(1サンプル1chあたり)
        /// </summary>
        /// <param name="t">サンプルフォーマットタイプ</param>
        /// <returns>メモリ上に占めるビット数(1サンプル1chあたり)</returns>
        public static int SampleFormatTypeToUseBitsPerSample(SampleFormatType t) {
            switch (t) {
            case SampleFormatType.Sint16:
                return 16;
            case SampleFormatType.Sint24:
                return 24;
            case SampleFormatType.Sint32V24:
                return 32;
            case SampleFormatType.Sint32:
                return 32;
            case SampleFormatType.Sfloat:
                return 32;
            case SampleFormatType.Sdouble:
                return 64;
            default:
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }
        }

        public static SampleFormatType BitAndFormatToSampleFormatType(int bitsPerSample, int validBitsPerSample, BitFormatType bitFormat) {
            if (bitFormat == BitFormatType.SInt) {
                // int
                switch (bitsPerSample) {
                case 16:
                    return SampleFormatType.Sint16;
                case 24:
                    return SampleFormatType.Sint24;
                case 32:
                    switch (validBitsPerSample) {
                    case 24:
                        return SampleFormatType.Sint32V24;
                    case 32:
                        return SampleFormatType.Sint32;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        return SampleFormatType.Unknown;
                    }
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return SampleFormatType.Unknown;
                }
            } else {
                // float
                switch (bitsPerSample) {
                case 32:
                    return SampleFormatType.Sfloat;
                case 64:
                    return SampleFormatType.Sdouble;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return SampleFormatType.Unknown;
                }
            }
        }

        /// <summary>
        ///  サンプルフォーマットタイプ→有効ビット数(1サンプル1chあたり。バイト数ではなくビット数)
        /// </summary>
        /// <param name="t">サンプルフォーマットタイプ</param>
        /// <returns>有効ビット数(1サンプル1chあたり。バイト数ではなくビット数)</returns>
        public static int SampleFormatTypeToValidBitsPerSample(SampleFormatType t) {
            switch (t) {
            case SampleFormatType.Sint16:
                return 16;
            case SampleFormatType.Sint24:
                return 24;
            case SampleFormatType.Sint32V24:
                return 24;
            case SampleFormatType.Sint32:
                return 32;
            case SampleFormatType.Sfloat:
                return 32;
            case SampleFormatType.Sdouble:
                return 64;
            default:
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }
        }

        public static BitFormatType SampleFormatTypeToBitFormatType(SampleFormatType t) {
            switch (t) {
            case SampleFormatType.Sint16:
            case SampleFormatType.Sint24:
            case SampleFormatType.Sint32V24:
            case SampleFormatType.Sint32:
                return BitFormatType.SInt;
            case WasapiCS.SampleFormatType.Sfloat:
                return BitFormatType.SFloat;
            default:
                System.Diagnostics.Debug.Assert(false);
                return BitFormatType.SInt;
            }
        }

        /// numChannels to channelMask
        /// please refer this article https://msdn.microsoft.com/en-us/library/windows/hardware/dn653308%28v=vs.85%29.aspx
        public static int
        GetTypicalChannelMask(int numChannels) {
            int result = 0;

            switch (numChannels) {
            case 1:
                result = 0; // mono (unspecified)
                break;
            case 2:
                result = 3; // 2ch stereo (FL FR)
                break;
            case 4:
                result = 0x33; // 4ch matrix (FL FR BL BR)
                break;
            case 6:
                result = 0x3f; // 5.1 surround (FL FR FC LFE BL BR)
                break;
            case 8:
                result = 0x63f;    // 7.1 surround   (FL FR FC LFE BL BR SL SR)
                break;
            case 12:
                result = 0x2d63f; //< 7.1.4 surround (FL FR FC LFE BL BR SL SR TFL TFR TBL TBR)
                break;
            default:
                // 0 means we does not specify particular speaker locations.
                result = 0;
                break;
            }

            return result;
        }

        private int mId =  -1;

        private NativeCaptureCallback mNativeCaptureCallback;
        private CaptureCallback mCaptureCallback;

        private NativeRenderCallback mNativeRenderCallback;
        private RenderCallback mRenderCallback;

        public int Init() {
            return WasapiIO_Init(ref mId);
        }

        public void Term() {
            RegisterCaptureCallback(null);
            RegisterRenderCallback(null);

            WasapiIO_Term(mId);
        }

        public void RegisterStateChangedCallback(StateChangedCallback callback) {
            WasapiIO_RegisterStateChangedCallback(mId, callback);
        }
        
        private void NativeCaptureCallbackImpl(IntPtr ptr, int bytes) {
            var data = new byte[bytes];
            Marshal.Copy(ptr, data, 0, bytes);
            mCaptureCallback(data);
        }

        public void RegisterCaptureCallback(CaptureCallback cb) {
            if (cb == null) {
                mNativeCaptureCallback = null;
                mCaptureCallback = null;
                WasapiIO_RegisterCaptureCallback(mId, null);
                return;
            }

            mNativeCaptureCallback = new NativeCaptureCallback(NativeCaptureCallbackImpl);
            mCaptureCallback = cb;
            WasapiIO_RegisterCaptureCallback(mId, mNativeCaptureCallback);
        }

        private void NativeRenderCallbackImpl(IntPtr ptr, int bytes) {
            var data = mRenderCallback(bytes);
            System.Diagnostics.Debug.Assert(data.Length == bytes);
            Marshal.Copy(data, 0, ptr, bytes);
        }

        public void RegisterRenderCallback(RenderCallback cb) {
            if (cb == null) {
                mNativeRenderCallback = null;
                mRenderCallback = null;
                WasapiIO_RegisterRenderCallback(mId, null);
                return;
            }

            mNativeRenderCallback = new NativeRenderCallback(NativeRenderCallbackImpl);
            mRenderCallback = cb;
            WasapiIO_RegisterRenderCallback(mId, mNativeRenderCallback);
        }

        public int EnumerateDevices(DeviceType t) {
            return WasapiIO_EnumerateDevices(mId, (int)t);
        }

        public int GetDeviceCount() {
            return WasapiIO_GetDeviceCount(mId);
        }

        public class DeviceAttributes {
            /// <summary>
            /// device id. numbered from 0
            /// </summary>
            public int Id { get; set; }

            /// <summary>
            /// device friendly name to display
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// device id string to identify
            /// </summary>
            public string DeviceIdString { get; set; }

            public DeviceAttributes(int id, string name, string deviceIdString) {
                Id = id;
                Name = name;
                DeviceIdString = deviceIdString;
            }
        };

        public DeviceAttributes GetDeviceAttributes(int deviceId) {
            var a = new WasapiIoDeviceAttributes();
            if (!WasapiIO_GetDeviceAttributes(mId, deviceId, out a)) {
                return null;
            }
            return new DeviceAttributes(a.deviceId, a.name, a.deviceIdString);
        }

        public class MixFormat {
            public int sampleRate;
            public SampleFormatType sampleFormat;    ///< WWPcmDataSampleFormatType
            public int numChannels;
            public int dwChannelMask;
            public MixFormat(int sampleRate, SampleFormatType sampleFormat, int numChannels, int dwChannelMask) {
                this.sampleRate = sampleRate;
                this.sampleFormat = sampleFormat;
                this.numChannels = numChannels;
                this.dwChannelMask = dwChannelMask;
            }

            public bool IsTheSameTo(MixFormat a) {
                return sampleRate == a.sampleRate
                    && sampleFormat == a.sampleFormat
                    && numChannels == a.numChannels
                    && dwChannelMask == a.dwChannelMask;
            }

            public int UseBytesPerFrame {
                get {
                    return SampleFormatTypeToUseBitsPerSample(sampleFormat) * numChannels;
                }
            }
        };

        public MixFormat GetMixFormat(int deviceId) {
            MixFormatArgs args;
            if (WasapiIO_GetMixFormat(mId, deviceId, out args) < 0) {
                return null;
            }
            return new MixFormat(args.sampleRate, (SampleFormatType)args.sampleFormat, args.numChannels, args.dwChannelMask);
        }

        public class DevicePeriod {
            /// <summary>
            /// unit is 100 * nanosec
            /// </summary>
            public long defaultPeriod;
            /// <summary>
            /// unit is 100 * nanosec
            /// </summary>
            public long minimumPeriod;
            public DevicePeriod(long defaultPeriod, long minimumPeriod) {
                this.defaultPeriod = defaultPeriod;
                this.minimumPeriod = minimumPeriod;
            }
        }

        public DevicePeriod GetDevicePeriod(int deviceId) {
            DevicePeriodArgs args;
            if (WasapiIO_GetDevicePeriod(mId, deviceId, out args) < 0) {
                return null;
            }

            return new DevicePeriod(args.defaultPeriod, args.minimumPeriod);
        }

        public int InspectDevice(int deviceId, DeviceType dt, int sampleRate, SampleFormatType format,
                int numChannels, int dwChannelMask) {
            var args = new InspectArgs();
            args.deviceType = (int)dt;
            args.sampleRate = sampleRate;
            args.numChannels = numChannels;
            args.sampleFormat = (int)format;
            args.dwChannelMask = dwChannelMask;
            return WasapiIO_InspectDevice(mId, deviceId, ref args);
        }

        public int Setup(int deviceId, DeviceType t, StreamType streamType,
                int sampleRate, SampleFormatType format, int numChannels,
                int dwChannelMask,
                MMCSSCallType mmcssCall, MMThreadPriorityType threadPriority,
                SchedulerTaskType schedulerTask, ShareMode shareMode, DataFeedMode dataFeedMode,
                int latencyMillisec, int zeroFlushMillisec, int timePeriodHandredNanosec,
                bool isFormatSupportedCall) {
            var args = new SetupArgs();
            args.deviceType = (int)t;
            args.streamType = (int)streamType;
            args.sampleRate = sampleRate;
            args.sampleFormat = (int)format;
            args.numChannels = numChannels;
            args.dwChannelMask = dwChannelMask;
            args.mmcssCall = (int)mmcssCall;
            args.mmThreadPriority = (int)threadPriority;
            args.schedulerTask = (int)schedulerTask;
            args.shareMode = (int)shareMode;
            args.dataFeedMode = (int)dataFeedMode;
            args.latencyMillisec = latencyMillisec;
            args.timePeriodHandledNanosec = timePeriodHandredNanosec;
            args.zeroFlushMillisec = zeroFlushMillisec;
            args.flags = (isFormatSupportedCall ? 1 : 0);
            return WasapiIO_Setup(mId, deviceId, ref args);
        }

        public void Unsetup() {
            WasapiIO_Unsetup(mId);
        }

        public bool AddPlayPcmDataStart() {
            return WasapiIO_AddPlayPcmDataStart(mId);
        }

        public bool AddPlayPcmData(int pcmId, byte[] data) {
            return WasapiIO_AddPlayPcmData(mId, pcmId, data, data.LongLength);
        }

        public bool AddPlayPcmData(int pcmId, WWUtil.LargeArray<byte> data) {
            if (!AddPlayPcmDataAllocateMemory(pcmId, data.LongLength)) {
                return false;
            }

            long posBytes = 0;
            for (int i = 0; i < data.ArrayNum(); ++i) {
                var fragment = data.ArrayNth(i);
                if (!AddPlayPcmDataSetPcmFragment(pcmId, posBytes, fragment)) {
                    return false;
                }
                posBytes += fragment.LongLength;
            }

            return true;
        }

        public bool AddPlayPcmDataAllocateMemory(int pcmId, long bytes) {
            return WasapiIO_AddPlayPcmData(mId, pcmId, null, bytes);
        }

        public bool AddPlayPcmDataSetPcmFragment(int pcmId, long posBytes, byte[] data) {
            return WasapiIO_AddPlayPcmDataSetPcmFragment(mId, pcmId, posBytes, data, data.Length);
        }

        /// <summary>
        /// perform resample on shared mode. blocking call.
        /// </summary>
        /// <param name="conversionQuality">1(minimum quality) to 60(maximum quality)</param>
        /// <returns>HRESULT</returns>
        public int ResampleIfNeeded(int conversionQuality) {
            return WasapiIO_ResampleIfNeeded(mId, conversionQuality);
        }

        public double ScanPcmMaxAbsAmplitude() {
            return WasapiIO_ScanPcmMaxAbsAmplitude(mId);
        }

        public void ScalePcmAmplitude(double scale) {
            WasapiIO_ScalePcmAmplitude(mId, scale);
            mPcmScale = scale;
        }

        public double GetScalePcmAmplitude() {
            return mPcmScale;
        }

        private double mPcmScale = 1.0;

        public bool AddPlayPcmDataEnd() {
            return WasapiIO_AddPlayPcmDataEnd(mId);
        }

        public void RemovePlayPcmDataAt(int pcmId) {
            WasapiIO_RemovePlayPcmDataAt(mId, pcmId);
        }

        public void ClearPlayList() {
            WasapiIO_ClearPlayList(mId);
        }

        public void SetPlayRepeat(bool repeat) {
            WasapiIO_SetPlayRepeat(mId, repeat);
        }

        public bool ConnectPcmDataNext(int fromPcmId, int toPcmId) {
            return WasapiIO_ConnectPcmDataNext(mId, fromPcmId, toPcmId);
        }

        public enum PcmDataUsageType {
            NowPlaying,
            PauseResumeToPlay,
            SpliceNext,
            Capture,
            Splice,
        };

        public int GetPcmDataId(PcmDataUsageType t) {
            return WasapiIO_GetPcmDataId(mId, (int)t);
        }

        /// <summary>
        /// 再生中の曲変更。
        /// idのグループが読み込まれている必要がある。
        /// 再生中に呼ぶ必要がある。再生中でない場合、空振りする。
        /// 
        /// </summary>
        /// <param name="id">曲番号。id==-1を指定すると再生終了時無音に曲変更する(その後再生するものが無くなって再生停止する)。</param>
        public void UpdatePlayPcmDataById(int pcmId) {
            WasapiIO_SetNowPlayingPcmDataId(mId, pcmId);
        }

        public long GetCaptureGlitchCount() {
            return WasapiIO_GetCaptureGlitchCount(mId);
        }

        public void ResetCaptureGlitchCount() {
            WasapiIO_ResetCaptureGlitchCount(mId);
        }

        public int StartPlayback(int wavDataId) {
            return WasapiIO_StartPlayback(mId, wavDataId);
        }

        public int StartRecording() {
            return WasapiIO_StartRecording(mId);
        }

        /// <summary>
        /// 再生スレッドが終了したかどうか調べる。
        /// </summary>
        /// <param name="millisec">中でブロックする待ち時間。</param>
        /// <returns>true: 終了した。false: 再生スレッドが続行した。</returns>
        public bool Run(int millisec) {
            return WasapiIO_Run(mId, millisec);
        }

        public int Stop() {
            return WasapiIO_Stop(mId);
        }

        public int Pause() {
            return WasapiIO_Pause(mId);
        }

        public int Unpause() {
            return WasapiIO_Unpause(mId);
        }

        public bool SetPosFrame(long v) {
            return WasapiIO_SetPosFrame(mId, v);
        }

        public class SessionStatus {
            public StreamType StreamType { get; set; }
            public int PcmDataSampleRate { get; set; }
            public int DeviceSampleRate { get; set; }
            public SampleFormatType DeviceSampleFormat { get; set; }
            public int DeviceBytesPerFrame { get; set; }
            public int DeviceNumChannels { get; set; }
            public int TimePeriodHandledNanosec { get; set; }
            public int EndpointBufferFrameNum { get; set; }

            public SessionStatus(StreamType streamType, int pcmDataSampleRate, int deviceSampleRate,
                    SampleFormatType deviceSampleFormat,
                    int deviceBytesPerFrame, int deviceNumChannels, int timePeriodHandledNanosec, int bufferFrameNum) {
                StreamType = streamType;
                PcmDataSampleRate = pcmDataSampleRate;
                DeviceSampleRate = deviceSampleRate;
                DeviceSampleFormat = deviceSampleFormat;
                DeviceBytesPerFrame = deviceBytesPerFrame;
                DeviceNumChannels = deviceNumChannels;
                TimePeriodHandledNanosec = timePeriodHandledNanosec;
                EndpointBufferFrameNum = bufferFrameNum;
            }
        };

        public SessionStatus GetSessionStatus() {
            var s = new WasapiIoSessionStatus();
            if (!WasapiIO_GetSessionStatus(mId, out s)) {
                return null;
            }
            return new SessionStatus((StreamType)s.streamType, s.pcmDataSampleRate, s.deviceSampleRate,
                    (SampleFormatType)s.deviceSampleFormat,
                    s.deviceBytesPerFrame, s.deviceNumChannels, s.timePeriodHandledNanosec, s.bufferFrameNum);
        }

        public class CursorLocation {
            public long PosFrame { get; set; }
            public long TotalFrameNum { get; set; }
            public CursorLocation(long posFrame, long totalFrameNum) {
                PosFrame = posFrame;
                TotalFrameNum = totalFrameNum;
            }
        };

        public CursorLocation GetPlayCursorPosition(PcmDataUsageType usageType) {
            var p = new WasapiIoCursorLocation();
            if (!WasapiIO_GetPlayCursorPosition(mId, (int)usageType, out p)) {
                return null;
            }
            return new CursorLocation(p.posFrame, p.totalFrameNum);
        }

        public class WorkerThreadSetupResult {
            public int DwmEnableMMCSSResult { get; set; }
            public bool AvSetMmThreadCharacteristicsResult { get; set; }
            public bool AvSetMmThreadPriorityResult { get; set; }
            public WorkerThreadSetupResult(int dwm, bool av, bool tp) {
                DwmEnableMMCSSResult = dwm;
                AvSetMmThreadCharacteristicsResult = av;
                AvSetMmThreadPriorityResult = tp;
            }
        }

        public WorkerThreadSetupResult GetWorkerThreadSetupResult() {
            var p = new WasapiIoWorkerThreadSetupResult();
            WasapiIO_GetWorkerThreadSetupResult(mId, out p);
            return new WorkerThreadSetupResult(p.dwmEnableMMCSSResult, p.avSetMmThreadCharacteristicsResult!=0,
                    p.avSetMmThreadPriorityResult!=0);
        }

        public void ClearAudioFilter() {
            WasapiIO_ClearAudioFilter(mId);
        }

        public void AppendAudioFilter(WWAudioFilterType aft, string args) {
            WasapiIO_AppendAudioFilter(mId, (int)aft, args);
        }

        public int SetMasterVolumeInDb(float db) {
            return WasapiIO_SetMasterVolumeInDb(mId, db);
        }

        public class VolumeParams {
            public float levelMinDB;
            public float levelMaxDB;
            public float volumeIncrementDB;
            public float defaultLevel;
            /// ENDPOINT_HARDWARE_SUPPORT_VOLUME ==1
            /// ENDPOINT_HARDWARE_SUPPORT_MUTE   ==2
            /// ENDPOINT_HARDWARE_SUPPORT_METER  ==4
            public int hardwareSupport;
            public VolumeParams(float min, float max, float increment, float aDefault, int hs) {
                levelMinDB = min;
                levelMaxDB = max;
                volumeIncrementDB = increment;
                defaultLevel = aDefault;
                hardwareSupport = hs;
            }
        };

        public int GetVolumeParams(out VolumeParams volumeParams) {
            var vp = new WasapiIoVolumeParams();
            int hr = WasapiIO_GetVolumeParams(mId, out vp);
            volumeParams = new VolumeParams(vp.levelMinDB, vp.levelMaxDB, vp.volumeIncrementDB,
                    vp.defaultLevel, vp.hardwareSupport);
            return hr;
        }

        public static string GetErrorMessage(int ercd) {
            switch ((uint)ercd) {
            case 0x80070005: return "Access denied";
            case 0x800700AA: return "Resource is in use";
            case 0x88890001: return "AUDCLNT_E_NOT_INITIALIZED";
            case 0x88890002: return "AUDCLNT_E_ALREADY_INITIALIZED";
            case 0x88890003: return "AUDCLNT_E_WRONG_ENDPOINT_TYPE";
            case 0x88890004: return "AUDCLNT_E_DEVICE_INVALIDATED";
            case 0x88890005: return "AUDCLNT_E_NOT_STOPPED";

            case 0x88890006: return "AUDCLNT_E_BUFFER_TOO_LARGE";
            case 0x88890007: return "AUDCLNT_E_OUT_OF_ORDER";
            case 0x88890008: return "AUDCLNT_E_UNSUPPORTED_FORMAT";
            case 0x88890009: return "AUDCLNT_E_INVALID_SIZE";
            case 0x8889000a: return "AUDCLNT_E_DEVICE_IN_USE";

            case 0x8889000b: return "AUDCLNT_E_BUFFER_OPERATION_PENDING";
            case 0x8889000c: return "AUDCLNT_E_THREAD_NOT_REGISTERED";
            case 0x8889000e: return "AUDCLNT_E_EXCLUSIVE_MODE_NOT_ALLOWED";
            case 0x8889000f: return "AUDCLNT_E_ENDPOINT_CREATE_FAILED";
            case 0x88890010: return "AUDCLNT_E_SERVICE_NOT_RUNNING";

            case 0x88890011: return "AUDCLNT_E_EVENTHANDLE_NOT_EXPECTED";
            case 0x88890012: return "AUDCLNT_E_EXCLUSIVE_MODE_ONLY";
            case 0x88890013: return "AUDCLNT_E_BUFDURATION_PERIOD_NOT_EQUAL";
            case 0x88890014: return "AUDCLNT_E_EVENTHANDLE_NOT_SET";
            case 0x88890015: return "AUDCLNT_E_INCORRECT_BUFFER_SIZE";

            case 0x88890016: return "AUDCLNT_E_BUFFER_SIZE_ERROR";
            case 0x88890017: return "AUDCLNT_E_CPUUSAGE_EXCEEDED";
            case 0x88890018: return "AUDCLNT_E_BUFFER_ERROR";
            case 0x88890019: return "AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED";
            case 0x88890020: return "AUDCLNT_E_INVALID_DEVICE_PERIOD";

            case 0x88890021: return "AUDCLNT_E_INVALID_STREAM_FLAG";
            case 0x88890022: return "AUDCLNT_E_ENDPOINT_OFFLOAD_NOT_CAPABLE";
            case 0x88890023: return "AUDCLNT_E_OUT_OF_OFFLOAD_RESOURCES";
            case 0x88890024: return "AUDCLNT_E_OFFLOAD_MODE_ONLY";
            case 0x88890025: return "AUDCLNT_E_NONOFFLOAD_MODE_ONLY";

            case 0x88890026: return "AUDCLNT_E_RESOURCES_INVALIDATED";
            case 0x88890027: return "AUDCLNT_E_RAW_MODE_UNSUPPORTED";
            case 0x88890028: return "AUDCLNT_E_ENGINE_PERIODICITY_LOCKED";
            case 0x88890029: return "AUDCLNT_E_ENGINE_FORMAT_LOCKED";

            case 0x88890100: return "SPTLAUDCLNT_E_DESTROYED";
            case 0x88890101: return "SPTLAUDCLNT_E_OUT_OF_ORDER";
            case 0x88890102: return "SPTLAUDCLNT_E_RESOURCES_INVALIDATED";
            case 0x88890103: return "SPTLAUDCLNT_E_NO_MORE_OBJECTS";
            case 0x88890104: return "SPTLAUDCLNT_E_PROPERTY_NOT_SUPPORTED";

            case 0x88890105: return "SPTLAUDCLNT_E_ERRORS_IN_OBJECT_CALLS";
            case 0x88890106: return "SPTLAUDCLNT_E_METADATA_FORMAT_NOT_SUPPORTED";
            case 0x88890107: return "SPTLAUDCLNT_E_STREAM_NOT_AVAILABLE";
            case 0x88890108: return "SPTLAUDCLNT_E_INVALID_LICENSE";

            case 0x8889010a: return "SPTLAUDCLNT_E_STREAM_NOT_STOPPED";
            case 0x8889010b: return "SPTLAUDCLNT_E_STATIC_OBJECT_NOT_AVAILABLE";
            case 0x8889010c: return "SPTLAUDCLNT_E_OBJECT_ALREADY_ACTIVE";
            case 0x8889010d: return "SPTLAUDCLNT_E_INTERNAL";

            default:
                return "";
            }
        }
    }
}
