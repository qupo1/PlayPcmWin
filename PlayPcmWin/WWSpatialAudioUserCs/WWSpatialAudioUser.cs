﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WWSpatialAudioUserCs {
    public class WWSpatialAudioUser : IDisposable {
        private int mInstanceId = -1;

        #region various enums
        public enum StateEnum {
            NoAudioDevice,
            SpatialAudioIsNotEnabled,
            Initialized,
            Ready,
        }

        private StateEnum mState = StateEnum.NoAudioDevice;
        public StateEnum State { get { return mState; } }

        /// <summary>
        /// WWTrackEnumと同じにする。
        /// </summary>
        public enum TrackTypeEnum {
            Prologue = -1,
            Epilogue = -2,
            Splice = -3,
            None = -4,

            Track0 = 0,
            Track1 = 1,
            Track2 = 2,
        }

        /// <summary>
        /// WWChangeTrackMethodと同じにする。
        /// </summary>
        public enum ChangeTrackMethod {
            Immediately = 0,
            Crossfade = 1,
        }

        /// <summary>
        /// AudioObjectType of SpatialAudioClient.h
        /// </summary>
        public enum AudioObjectType {
            None = 0,
            Dynamic = 1,
            FrontLeft = 2,
            FrontRight = 4,
            FrontCenter = 8,

            LowFrequency = 0x10,
            SideLeft = 0x20,
            SideRight = 0x40,
            BackLeft = 0x80,
            BackRight = 0x100,

            TopFrontLeft = 0x200,
            TopFrontRight = 0x400,
            TopBackLeft = 0x800,
            TopBackRight = 0x1000,
            BottomFrontLeft = 0x2000,

            BottomFrontRight = 0x4000,
            BottomBackLeft = 0x8000,
            BottomBackRight = 0x10000,
            BackCenter = 0x20000
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/ksmedia/ns-ksmedia-waveformatextensible
        /// </summary>
        public enum DwChannelMaskType {
            FrontLeft = 1,
            FrontRight = 2,
            FrontCenter = 4,
            LowFrequency = 8,
            BackLeft = 0x10,

            BackRight = 0x20,
            FrontLeftOfCenter = 0x40,
            FrontRightOfCenter = 0x80,
            BackCenter = 0x100,
            SideLeft = 0x200,

            SideRight = 0x400,
            TopCenter = 0x800,
            TopFrontLeft = 0x1000,
            TopFrontCenter = 0x2000,
            TopFrontRight = 0x4000,

            TopBackLeft = 0x8000,
            TopBackCenter = 0x10000,
            TopBackRight = 0x20000,
        }
        #endregion

        #region DwChannelMask and AudioObjectTypeMask conversion

        public static string DwChannelMaskShortStr(DwChannelMaskType t) {
            switch (t) {
            case DwChannelMaskType.FrontLeft:
                return "FL";
            case DwChannelMaskType.FrontRight:
                return "FR";
            case DwChannelMaskType.FrontCenter:
                return "FC";
            case DwChannelMaskType.LowFrequency:
                return "LFE";
            case DwChannelMaskType.BackLeft:
                return "BL";

            case DwChannelMaskType.BackRight:
                return "BR";
            case DwChannelMaskType.FrontLeftOfCenter:
                return "FLC";
            case DwChannelMaskType.FrontRightOfCenter:
                return "FRC";
            case DwChannelMaskType.BackCenter:
                return "BC";
            case DwChannelMaskType.SideLeft:
                return "SL";

            case DwChannelMaskType.SideRight:
                return "SR";
            case DwChannelMaskType.TopCenter:
                return "TC";
            case DwChannelMaskType.TopFrontCenter:
                return "TFC";
            case DwChannelMaskType.TopFrontLeft:
                return "TFL";
            case DwChannelMaskType.TopFrontRight:
                return "TFR";

            case DwChannelMaskType.TopBackLeft:
                return "TBL";
            case DwChannelMaskType.TopBackCenter:
                return "TBC";
            case DwChannelMaskType.TopBackRight:
                return "TBR";
            default:
                return string.Format("Unknown({0})", (int)t);
            }
        }

        public static List<DwChannelMaskType> DwChannelMaskToList(int dwChannelMask) {
            var r = new List<DwChannelMaskType>();

            for (int i=1; i<=0x20000; i*=2) {
                if (0 != (dwChannelMask & i)) {
                    r.Add((DwChannelMaskType)i);
                }
            }
            return r;
        }

        public static List<AudioObjectType> AudioObjectTypeMaskToList(int audioObjectTypeMask) {
            var r = new List<AudioObjectType>();

            for (int i = 1; i <= 0x20000; i *= 2) {
                if (0 != (audioObjectTypeMask & i)) {
                    r.Add((AudioObjectType)i);
                }
            }

            return r;
        }

        public static AudioObjectType DwChannelMaskTypeToAudioObjectType(DwChannelMaskType t) {
            return (AudioObjectType)DwChannelMaskToAudioObjectTypeMask((int)t);
        }

        public static int DwChannelMaskToAudioObjectTypeMask(int dwChannelMask) {
            int r = 0;
            if (0 != (dwChannelMask & (int)DwChannelMaskType.FrontLeft)) {
                r |= (int)AudioObjectType.FrontLeft;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.FrontRight)) {
                r |= (int)AudioObjectType.FrontRight;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.FrontCenter)) {
                r |= (int)AudioObjectType.FrontCenter;
            }

            if (0 != (dwChannelMask & (int)DwChannelMaskType.LowFrequency)) {
                r |= (int)AudioObjectType.LowFrequency;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.SideLeft)) {
                r |= (int)AudioObjectType.SideLeft;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.SideRight)) {
                r |= (int)AudioObjectType.SideRight;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.BackLeft)) {
                r |= (int)AudioObjectType.BackLeft;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.BackRight)) {
                r |= (int)AudioObjectType.BackRight;
            }

            if (0 != (dwChannelMask & (int)DwChannelMaskType.TopFrontLeft)) {
                r |= (int)AudioObjectType.TopFrontLeft;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.TopFrontRight)) {
                r |= (int)AudioObjectType.TopFrontRight;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.TopBackLeft)) {
                r |= (int)AudioObjectType.TopBackLeft;
            }
            if (0 != (dwChannelMask & (int)DwChannelMaskType.TopBackRight)) {
                r |= (int)AudioObjectType.TopBackRight;
            }

            // bottomFrontLeft
            // bottomFrontRight
            // bottomBackLeft
            // bottomBackRight

            if (0 != (dwChannelMask & (int)DwChannelMaskType.BackCenter)) {
                r |= (int)AudioObjectType.BackCenter;
            }

            return r;
        }
        public static int AudioObjectTypeMaskToDwChannelMask(int audioObjectTypeMask) {
            int r = 0;
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.FrontLeft)) {
                r |= (int)DwChannelMaskType.FrontLeft;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.FrontRight)) {
                r |= (int)DwChannelMaskType.FrontRight;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.FrontCenter)) {
                r |= (int)DwChannelMaskType.FrontCenter;
            }

            if (0 != (audioObjectTypeMask & (int)AudioObjectType.LowFrequency)) {
                r |= (int)DwChannelMaskType.LowFrequency;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.SideLeft)) {
                r |= (int)DwChannelMaskType.SideLeft;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.SideRight)) {
                r |= (int)DwChannelMaskType.SideRight;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.BackLeft)) {
                r |= (int)DwChannelMaskType.BackLeft;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.BackRight)) {
                r |= (int)DwChannelMaskType.BackRight;
            }

            if (0 != (audioObjectTypeMask & (int)AudioObjectType.TopFrontLeft)) {
                r |= (int)DwChannelMaskType.TopFrontLeft;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.TopFrontRight)) {
                r |= (int)DwChannelMaskType.TopFrontRight;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.TopBackLeft)) {
                r |= (int)DwChannelMaskType.TopBackLeft;
            }
            if (0 != (audioObjectTypeMask & (int)AudioObjectType.TopBackRight)) {
                r |= (int)DwChannelMaskType.TopBackRight;
            }

            // bottomFrontLeft
            // bottomFrontRight
            // bottomBackLeft
            // bottomBackRight

            if (0 != (audioObjectTypeMask & (int)AudioObjectType.BackCenter)) {
                r |= (int)DwChannelMaskType.BackCenter;
            }

            return r;
        }
        #endregion

        public class DeviceProperty {
            public int id;
            public string devIdStr;
            public string name;

            public DeviceProperty(int id, string devIdStr, string name) {
                this.id = id;
                this.devIdStr = devIdStr;
                this.name = name;
            }
        };

        public List<DeviceProperty> DevicePropertyList {
            get { return mDevicePropertyList; }
            set { mDevicePropertyList = value; }
        }
        private List<DeviceProperty> mDevicePropertyList = new List<DeviceProperty>();

#region NativeStuff
        internal static class NativeMethods {
            public const int TEXT_STRSZ = 256;

            [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
            internal struct WWSpatialAudioDeviceProperty {
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = TEXT_STRSZ)]
                public String devIdStr;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = TEXT_STRSZ)]
                public String name;
            };

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserInit();

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserTerm(int instanceId);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserDoEnumeration(int instanceId);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserGetDeviceCount(int instanceId);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserGetDeviceProperty(
                int instanceId, int idx,
                ref WWSpatialAudioDeviceProperty sadp);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserChooseDevice(
                int instanceId, int devIdx, int maxDynObjectCount, int staticObjectTypeMask);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserClearAllPcm(int instanceId);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserSetPcmBegin(
                int instanceId, int ch, long numSamples);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserSetPcmFragment(
                int instanceId, int ch, long startSamplePos, int sampleCount, float[] samples);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserSetPcmEnd(
                int instanceId, int ch, int audioObjectType);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserStart(
                int instanceId);
            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserStop(
                int instanceId);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserGetThreadErcd(
                int instanceId);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserGetPlayingTrackNr(
                int instanceId, int ch, ref int trackNr_r);

            /// 全てのチャンネルの再生位置を変更。
            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserSetPosFrame(
                int instanceId, long frame);

            [StructLayout(LayoutKind.Sequential, Pack = 8)]
            internal struct WWPlayStatus {
                public int trackNr;
                public int dummy;
                public long posFrame;
                public long totalFrameNum;
            };

            [DllImport("WWSpatialAudioUserCpp2017.dll")]
            internal extern static int
            WWSpatialAudioUserGetPlayStatus(int instanceId, int ch, ref WWPlayStatus a);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserRewind(
                int instanceId);

            [DllImport("WWSpatialAudioUserCpp2017.dll", CharSet = CharSet.Unicode)]
            internal extern static int WWSpatialAudioUserSetCurrentPcm(
                int instanceId, int trackEnum, int ctm);
        };
#endregion

        public WWSpatialAudioUser() {
            mInstanceId = NativeMethods.WWSpatialAudioUserInit();

            int hr = NativeMethods.WWSpatialAudioUserDoEnumeration(mInstanceId);
            if (hr < 0) {
                mState = StateEnum.NoAudioDevice;
                return;
            }

            mState = StateEnum.Initialized;
        }

        public int UpdateDeviceList() {
            mDevicePropertyList.Clear();

            int hr = NativeMethods.WWSpatialAudioUserDoEnumeration(mInstanceId);
            if (hr < 0) {
                mState = StateEnum.NoAudioDevice;
                return hr;
            }

            int nDev = NativeMethods.WWSpatialAudioUserGetDeviceCount(mInstanceId);

            for (int i=0; i<nDev; ++i) {
                var sadp = new NativeMethods.WWSpatialAudioDeviceProperty();
                NativeMethods.WWSpatialAudioUserGetDeviceProperty(mInstanceId, i, ref sadp);

                var dev = new DeviceProperty(i, sadp.devIdStr, sadp.name);
                mDevicePropertyList.Add(dev);
            }
            mState = StateEnum.Ready;
            return 0;
        }

        /// <summary>
        /// choose device to play sound
        /// </summary>
        /// <param name="deviceId">device list item index starts from 0</param>
        /// <param name="maxDynamicObjectCount"></param>
        /// <param name="staticObjectTypeMask">bitwiseOR of AudioObjectType</param>
        /// <returns></returns>
        public int ChooseDevice(int deviceId, int maxDynamicObjectCount, int staticObjectTypeMask) {
            int hr = NativeMethods.WWSpatialAudioUserChooseDevice(
                mInstanceId, deviceId, maxDynamicObjectCount, staticObjectTypeMask);
            if (0 <= hr) {
                mState = StateEnum.Ready;
            }
            return hr;
        }

        public void ClearAllPcm() {
            int hr = NativeMethods.WWSpatialAudioUserClearAllPcm(mInstanceId);
            System.Diagnostics.Debug.Assert(0 <= hr);
        }

        public int SetPcmBegin(int ch, long numSamples) {
            int hr = NativeMethods.WWSpatialAudioUserSetPcmBegin(mInstanceId, ch, numSamples);
            return hr;
        }

        /// <summary>
        /// ネイティブPCMストアーのstartSamplePosにpcmFragmentを全てコピーする。
        /// </summary>
        public int SetPcmFragment(int ch, long startSamplePos, float [] pcmFragment) {
            int hr = NativeMethods.WWSpatialAudioUserSetPcmFragment(mInstanceId, ch, startSamplePos, pcmFragment.Length, pcmFragment);
            return hr;
        }

        public void SetPcmEnd(int ch, AudioObjectType aot) {
            int hr = NativeMethods.WWSpatialAudioUserSetPcmEnd(mInstanceId, ch, (int)aot);
            System.Diagnostics.Debug.Assert(0 <= hr);
        }

        public int Start() {
            int hr = NativeMethods.WWSpatialAudioUserStart(mInstanceId);
            return hr;
        }

        public int Stop() {
            int hr = NativeMethods.WWSpatialAudioUserStop(mInstanceId);
            return hr;
        }

        public int GetThreadErcd() {
            return NativeMethods.WWSpatialAudioUserGetThreadErcd(mInstanceId);
        }

        /// <returns>PlayingTrackEnumが戻る。</returns>
        public int GetPlayingTrackNr(int ch) {
            int r = 0;
            int hr = NativeMethods.WWSpatialAudioUserGetPlayingTrackNr(mInstanceId, ch, ref r);
            System.Diagnostics.Debug.Assert(0 <= hr);
            return r;
        }

        public void SetCurrentPcm(TrackTypeEnum te, ChangeTrackMethod ctm) {
            int hr = NativeMethods.WWSpatialAudioUserSetCurrentPcm(mInstanceId, (int)te, (int)ctm);
            System.Diagnostics.Debug.Assert(0 <= hr);
        }

        public int SetPlayPos(long frame) {
            //Console.WriteLine("SetPlayPos {0}", frame);
            int hr = NativeMethods.WWSpatialAudioUserSetPosFrame(mInstanceId, frame);
            return hr;
        }

        public class PlayStatus {
            public int TrackNr { get; set; }
            public long PosFrame { get; set; }
            public long TotalFrameNum { get; set; }
            public PlayStatus(int tnr, long posFrame, long totalFrameNum) {
                TrackNr = tnr;
                PosFrame = posFrame;
                TotalFrameNum = totalFrameNum;
            }
        };

        public PlayStatus GetPlayStatus(int ch) {
            var p = new NativeMethods.WWPlayStatus();
            int hr = NativeMethods.WWSpatialAudioUserGetPlayStatus(mInstanceId, ch, ref p);
            if (hr < 0) {
                return null;
            }
            return new PlayStatus(p.trackNr, p.posFrame, p.totalFrameNum);
        }

        public void Rewind() {
            int hr = NativeMethods.WWSpatialAudioUserRewind(mInstanceId);
            System.Diagnostics.Debug.Assert(0 <= hr);
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // 終了処理とDispose。

        private void Term() {
            if (mInstanceId < 0) {
                return;
            }
            NativeMethods.WWSpatialAudioUserTerm(mInstanceId);
            mInstanceId = -1;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                }

                // Free unmanaged resources here.
                Term();

                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion



    };
};

