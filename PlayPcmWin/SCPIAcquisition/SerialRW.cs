using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace SCPIAcquisition {
    public class SerialRW {
        private const int READ_TIMEOUT_MS = 10000;
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

        public string RecvLine() {
            mSerialPort.ReadTimeout = READ_TIMEOUT_MS;
            string r = "";
            try {
                r = mSerialPort.ReadLine();
            } catch (TimeoutException ex) {
                // 特にすることはない。
            }
            return r;
        }
    }
}
