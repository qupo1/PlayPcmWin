using System;
using System.Collections.Generic;
using System.Linq;

namespace SCPIAcquisition {
    class ScpiCommands {
        private SerialRW mSerialRW;

        private const int MEASURE_TIMEOUT_MS = 10000;
        private const int QUERY_TIMEOUT_MS = 1000;

        public enum CmdType {
            Reset,
            Measure,
            Beep,
            LcdDisplayOn,
            LcdDisplayOff,

            IDN,
            AutoRangeOn,
            AutoRangeOff,
        };

        public enum MeasureType {
            None = -1,

            DC_V,
            AC_V,
            DC_A,
            AC_A,
            Resistance,

            Capacitance,
            Frequency,
        };

        public class Cmd {
            public CmdType ct;
            public MeasureType mt = MeasureType.None;
            public string result = "";

            public Cmd(CmdType act) {
                ct = act;
            }

            public Cmd(MeasureType amt) {
                ct = CmdType.Measure;
                mt = amt;
            }

            public Cmd(CmdType act, MeasureType amt) {
                ct = act;
                mt = amt;
            }
        };

        List<Cmd> mCmdList = new List<Cmd>();
        List<Cmd> mResultList = new List<Cmd>();

        public ScpiCommands() {
            mSerialRW = null;
            // IDの問い合わせ。
            SetCmd(new ScpiCommands.Cmd(ScpiCommands.CmdType.IDN));
        }

        public void SetSerial(SerialRW serialRW) {
            mSerialRW = serialRW;
        }

        public void Term() {
            mSerialRW = null;
        }

        public void SetCmd(Cmd cmd) {
            lock (mCmdList) {
                mCmdList.Add(cmd);
            };
        }

        public int ResultsNum {
            get { return mResultList.Count; }
        }

        public List<Cmd> GetResults(int n) {
            System.Diagnostics.Debug.Assert(n <= ResultsNum);

            var r = new List<Cmd>();
            for (int i = ResultsNum - n; i < ResultsNum; ++i) {
                r.Add(mResultList.ElementAt(i));
            }

            return r;
        }

        public int QueuedCmdNum() {
            lock (mCmdList) {
                return mCmdList.Count;
            }
        }

        private string MeasureRangeTypeToStr(ScpiCommands.MeasureType mt) {
            switch (mt) {
                case MeasureType.None:
                default:
                    throw new ArgumentException();

                case MeasureType.DC_V:
                    return "VOLT:DC";
                case MeasureType.AC_V:
                    return "VOLT:DC";
                case MeasureType.DC_A:
                    return "CURR:DC";
                case MeasureType.AC_A:
                    return "CURR:AC";
                case MeasureType.Capacitance:
                    return "CAP";

                case MeasureType.Frequency:
                    return "FREQ:VOLT";
                case MeasureType.Resistance:
                    return "RES";
            }
        }

        public int ExecCmd() {
            int nCmd = 0;

            lock (mCmdList) {
                while (0 < mCmdList.Count) {
                    if (mSerialRW == null) {
                        return nCmd;
                    }
                    var cmd = mCmdList[0];

                    switch (cmd.ct) {
                        case CmdType.Reset:
                            mSerialRW.Send("*RST\n");
                            break;
                        case CmdType.IDN:
                            mSerialRW.Send("*IDN?\n");
                            cmd.result = mSerialRW.RecvLine(QUERY_TIMEOUT_MS);
                            break;
                        case CmdType.Beep:
                            mSerialRW.Send("SYST:BEEP\n");
                            break;
                        case CmdType.LcdDisplayOn:
                            mSerialRW.Send("DISP ON\n");
                            break;
                        case CmdType.LcdDisplayOff:
                            mSerialRW.Send("DISP OFF\n");
                            break;
                        case CmdType.AutoRangeOn:
                            mSerialRW.Send(string.Format("SENS:{0}:AUTO ON\n", MeasureRangeTypeToStr(cmd.mt)));
                            break;
                        case CmdType.AutoRangeOff:
                            mSerialRW.Send(string.Format("SENS:{0}:AUTO OFF\n", MeasureRangeTypeToStr(cmd.mt)));
                            break;
                        case CmdType.Measure:
                            switch (cmd.mt) {
                                case MeasureType.DC_V:
                                    mSerialRW.Send("MEAS:VOLT:DC?\n");
                                    cmd.result = mSerialRW.RecvLine(MEASURE_TIMEOUT_MS);
                                    break;
                                case MeasureType.AC_V:
                                    mSerialRW.Send("MEAS:VOLT:AC?\n");
                                    cmd.result = mSerialRW.RecvLine(MEASURE_TIMEOUT_MS);
                                    break;
                                case MeasureType.DC_A:
                                    mSerialRW.Send("MEAS:CURR:DC?\n");
                                    cmd.result = mSerialRW.RecvLine(MEASURE_TIMEOUT_MS);
                                    break;
                                case MeasureType.AC_A:
                                    mSerialRW.Send("MEAS:CURR:AC?\n");
                                    cmd.result = mSerialRW.RecvLine(MEASURE_TIMEOUT_MS);
                                    break;
                                case MeasureType.Resistance:
                                    mSerialRW.Send("MEAS:RES?\n");
                                    cmd.result = mSerialRW.RecvLine(MEASURE_TIMEOUT_MS);
                                    break;
                                case MeasureType.Capacitance:
                                    mSerialRW.Send("MEAS:CAP?\n");
                                    cmd.result = mSerialRW.RecvLine(MEASURE_TIMEOUT_MS);
                                    break;
                                case MeasureType.Frequency:
                                    mSerialRW.Send("MEAS:FREQ?\n");
                                    cmd.result = mSerialRW.RecvLine(MEASURE_TIMEOUT_MS);
                                    break;
                            }
                            break;
                    }

                    mCmdList.RemoveAt(0);
                    mResultList.Add(cmd);
                    ++nCmd;
                }
            }

            return nCmd;
        }
    }
}
