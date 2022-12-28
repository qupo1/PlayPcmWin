using System;
using System.Globalization;
using System.Text;
using System.Windows;
using Wasapi;

namespace PlayPcmWin {
    public sealed partial class MainWindow : Window {
        struct InspectFormat {
            public int sampleRate;
            public int bitsPerSample;
            public int validBitsPerSample;
            public WasapiCS.BitFormatType bitFormat;
            public InspectFormat(int sr, int bps, int vbps, WasapiCS.BitFormatType bf) {
                sampleRate = sr;
                bitsPerSample = bps;
                validBitsPerSample = vbps;
                bitFormat = bf;
            }
        };

        const int TEST_SAMPLE_RATE_NUM = 8;
        const int TEST_BIT_REPRESENTATION_NUM = 5;

        static readonly int[] gInspectNumChannels = new int[] {
                2,
                4,
                6,
                8,
        };

        static readonly InspectFormat[] gInspectFormats = new InspectFormat[] {
                new InspectFormat(44100,  16, 16, WasapiCS.BitFormatType.SInt),
                new InspectFormat(48000,  16, 16, WasapiCS.BitFormatType.SInt),
                new InspectFormat(88200,  16, 16, WasapiCS.BitFormatType.SInt),
                new InspectFormat(96000,  16, 16, WasapiCS.BitFormatType.SInt),
                new InspectFormat(176400, 16, 16, WasapiCS.BitFormatType.SInt),
                new InspectFormat(192000, 16, 16, WasapiCS.BitFormatType.SInt),
                new InspectFormat(352800, 16, 16, WasapiCS.BitFormatType.SInt),
                new InspectFormat(384000, 16, 16, WasapiCS.BitFormatType.SInt),

                new InspectFormat(44100,  24, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(48000,  24, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(88200,  24, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(96000,  24, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(176400, 24, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(192000, 24, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(352800, 24, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(384000, 24, 24, WasapiCS.BitFormatType.SInt),

                new InspectFormat(44100,  32, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(48000,  32, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(88200,  32, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(96000,  32, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(176400, 32, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(192000, 32, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(352800, 32, 24, WasapiCS.BitFormatType.SInt),
                new InspectFormat(384000, 32, 24, WasapiCS.BitFormatType.SInt),

                new InspectFormat(44100,  32, 32, WasapiCS.BitFormatType.SInt),
                new InspectFormat(48000,  32, 32, WasapiCS.BitFormatType.SInt),
                new InspectFormat(88200,  32, 32, WasapiCS.BitFormatType.SInt),
                new InspectFormat(96000,  32, 32, WasapiCS.BitFormatType.SInt),
                new InspectFormat(176400, 32, 32, WasapiCS.BitFormatType.SInt),
                new InspectFormat(192000, 32, 32, WasapiCS.BitFormatType.SInt),
                new InspectFormat(352800, 32, 32, WasapiCS.BitFormatType.SInt),
                new InspectFormat(384000, 32, 32, WasapiCS.BitFormatType.SInt),

                new InspectFormat(44100,  32, 32, WasapiCS.BitFormatType.SFloat),
                new InspectFormat(48000,  32, 32, WasapiCS.BitFormatType.SFloat),
                new InspectFormat(88200,  32, 32, WasapiCS.BitFormatType.SFloat),
                new InspectFormat(96000,  32, 32, WasapiCS.BitFormatType.SFloat),
                new InspectFormat(176400, 32, 32, WasapiCS.BitFormatType.SFloat),
                new InspectFormat(192000, 32, 32, WasapiCS.BitFormatType.SFloat),
                new InspectFormat(352800, 32, 32, WasapiCS.BitFormatType.SFloat),
                new InspectFormat(384000, 32, 32, WasapiCS.BitFormatType.SFloat),
            };

        private void InspectSupportedFormats() {
            var attr = mAp.wasapi.GetDeviceAttributes(listBoxDevices.SelectedIndex);

            AddLogText(string.Format(CultureInfo.InvariantCulture, "mAp.wasapi.InspectDevice()\r\nDeviceFriendlyName={0}\r\nDeviceIdString={1}{2}",
                attr.Name, attr.DeviceIdString, Environment.NewLine));

            foreach (int numChannels in gInspectNumChannels) {
                int channelMask = WasapiCS.GetTypicalChannelMask(numChannels);
                AddLogText(string.Format(CultureInfo.InvariantCulture,
                        "Num of channels={0}, dwChannelMask=0x{1:X}:\n", numChannels, channelMask));

                AddLogText(string.Format(CultureInfo.InvariantCulture, "++-------------++-------------++-------------++-------------++-------------++-------------++-------------++-------------++{0}", Environment.NewLine));
                for (int fmt = 0; fmt < TEST_BIT_REPRESENTATION_NUM; ++fmt) {
                    var sb = new StringBuilder();
                    for (int sr = 0; sr < TEST_SAMPLE_RATE_NUM; ++sr) {
                        int idx = sr + fmt * TEST_SAMPLE_RATE_NUM;
                        System.Diagnostics.Debug.Assert(idx < gInspectFormats.Length);
                        var ifmt = gInspectFormats[idx];
                        sb.Append(string.Format(CultureInfo.InvariantCulture, "||{0,3}kHz {1}{2}V{3}",
                                ifmt.sampleRate / 1000, ifmt.bitFormat == 0 ? "i" : "f",
                                ifmt.bitsPerSample, ifmt.validBitsPerSample));
                    }
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "||{0}", Environment.NewLine));
                    AddLogText(sb.ToString());

                    sb.Clear();
                    for (int sr = 0; sr < TEST_SAMPLE_RATE_NUM; ++sr) {
                        int idx = sr + fmt * TEST_SAMPLE_RATE_NUM;
                        System.Diagnostics.Debug.Assert(idx < gInspectFormats.Length);
                        var ifmt = gInspectFormats[idx];
                        int hr = mAp.wasapi.InspectDevice(listBoxDevices.SelectedIndex,
                                WasapiCS.DeviceType.Play, ifmt.sampleRate,
                                WasapiCS.BitAndFormatToSampleFormatType(ifmt.bitsPerSample, ifmt.validBitsPerSample, ifmt.bitFormat), numChannels, channelMask);
                        sb.Append(string.Format(CultureInfo.InvariantCulture, "|| {0} {1:X8} ", hr == 0 ? "OK" : "NA", hr));
                    }
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "||{0}", Environment.NewLine));
                    AddLogText(sb.ToString());
                    AddLogText(string.Format(CultureInfo.InvariantCulture, "++-------------++-------------++-------------++-------------++-------------++-------------++-------------++-------------++{0}", Environment.NewLine));
                }
                AddLogText("\n");
            }

            var mixFormat = mAp.wasapi.GetMixFormat(listBoxDevices.SelectedIndex);
            if (mixFormat == null) {
                AddLogText(string.Format(CultureInfo.InvariantCulture, "IAudioClient::GetMixFormat() failed!\n"));
            } else {
                AddLogText(string.Format(CultureInfo.InvariantCulture, "IAudioClient::GetMixFormat()\n  {0}Hz {2}ch {1}, dwChannelMask=0x{3:X}\n",
                    mixFormat.sampleRate, mixFormat.sampleFormat, mixFormat.numChannels, mixFormat.dwChannelMask));
            }

            var devicePeriod = mAp.wasapi.GetDevicePeriod(listBoxDevices.SelectedIndex);
            if (devicePeriod == null) {
                AddLogText(string.Format(CultureInfo.InvariantCulture, "IAudioClient::GetDevicePeriod() failed!\n"));
            } else {
                AddLogText(string.Format(CultureInfo.InvariantCulture, "IAudioClient::GetDevicePeriod()\n  default={0}ms, min={1}ms\n",
                    devicePeriod.defaultPeriod / 10000.0,
                    devicePeriod.minimumPeriod / 10000.0));
            }
        }


    }
}
