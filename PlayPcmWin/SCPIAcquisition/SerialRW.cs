using System;
using System.IO.Ports;

namespace SCPIAcquisition {
    public class SerialRW {
        private string[] mComPortList;
        private SerialPort mSerialPort = null;

        public string[] EnumerateComPorts() {
            mComPortList = SerialPort.GetPortNames();

            return mComPortList;
        }

        public bool IsConnected {
            get { return mSerialPort != null; }
        }

        public void Connect(int comPortIdx, int baud, Parity parity, int dataBits, StopBits stopBits) {
            System.Diagnostics.Debug.Assert(mSerialPort == null);

            string portName = mComPortList[comPortIdx];
            mSerialPort = new SerialPort(portName, baud, parity, dataBits, stopBits);
            if (!mSerialPort.IsOpen) {
                // 直ぐにオープンする。
                mSerialPort.Open();
            }
        }

        public void Disconnect() {
            if (mSerialPort != null) {
                Console.WriteLine("Close com port.");
                mSerialPort.Close();
                mSerialPort.Dispose();
                mSerialPort = null;
            }
        }

        public void Send(string s) {
            mSerialPort.Write(s);
        }

        public string RecvLine(int timeoutMS) {
            mSerialPort.ReadTimeout = timeoutMS;
            string r = "";
            r = mSerialPort.ReadLine();
            return r;
        }
    }
}
