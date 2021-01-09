using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace MultipleAppInstanceComm {

    /// <summary>
    /// コマンドライン引数データを受信したとき呼ばれるコールバック。
    /// </summary>
    /// <param name="args">受信したコマンドライン引数</param>
    public delegate void MultipleAppInstanceRecvArgsCallback(object cbObject, MultipleAppInstanceMgr.ArgsParams args);

    /// <summary>
    /// 複数インスタンス起動を検出。
    /// 最初のインスタンスに起動引数を送ります。
    /// </summary>
    public class MultipleAppInstanceMgr {
        public class ArgsParams {
            public int version;
            public List<string> args = new List<string>();
        };

        private Mutex mAppInstanceMtx;
        private bool? mOnlyInstance = null;

        const int PROTOCOL_VERSION = 1;
        const int CONNECT_TIMEOUT_MSEC = 1000;
        const int NUM_THREADS = 1;

        private string mPipeName;
        private string mMutexName;

        private Thread mServerThread;
        private NamedPipeServerStream mServerStream;
        private bool mServerThreadEnd = false;
        private ManualResetEvent mServerStopEvent;

        private MultipleAppInstanceRecvArgsCallback mCb;
        private object mCbObject;

        /// <summary>
        /// ctor。
        /// </summary>
        /// <param name="appName">パイプとミューテックスの名前。</param>
        public MultipleAppInstanceMgr(string appName, MultipleAppInstanceRecvArgsCallback cb, object cbObject) {
            mPipeName = appName;
            mMutexName = appName;

            mCb = cb;
            mCbObject = cbObject;
        }

        /// <summary>
        /// アプリが既に起動しているか調べる。
        /// </summary>
        /// <returns>true: アプリが既に起動している。</returns>
        public bool IsAppAlreadyRunning() {
            if (mOnlyInstance != null && mOnlyInstance == true) {
                // 前回調べた結果、このインスタンスが唯一のインスタンスであった。
                return false;
            }

            bool onlyInstance = false;
            mAppInstanceMtx = new Mutex(true, mMutexName, out onlyInstance);
            if (!onlyInstance) {
                return true;
            }

            // 唯一のインスタンスである事が判った。
            mOnlyInstance = true;

            // Mutexのインスタンスのガベコレ消滅防止。
            GC.KeepAlive(mAppInstanceMtx);

            return false;
        }

        /// <summary>
        /// 最初に起動したPPWに起動引数を送ります。
        /// </summary>
        /// <returns>成功すると0。失敗すると負の数。</returns>
        public int SendArgsToServer(string[] args) {
            if (args.Length == 0) {
                // 送るデータが無い。
                Console.WriteLine("SendArgsToServer nothing to send to.");
                return 0;
            }

            var pipe = new NamedPipeClientStream(".", mPipeName,
                    PipeDirection.InOut, PipeOptions.None,
                    TokenImpersonationLevel.Impersonation);

            Console.WriteLine("Connecting to named pipe server \"{0}\" ...\n", mPipeName);
            try {
                pipe.Connect(CONNECT_TIMEOUT_MSEC);
            } catch (TimeoutException ex) {
                Console.WriteLine("Error: Connect timeout. {0}", ex.ToString());
                return -1;
            }

            /*
             * オフセット     サイズ(バイト)       内容
             * 0              4                    PROTOCOL_VERSION
             * 4              4                    args.Length
             * 8              4                    args[0].Length
             * 12             args[0].Len          args[0]の文字列。Unicode
             * 16+args[0].Len 4                    args[1].Length
             * ...
             * -              args[args.Len-1].Len args[args.Len-1]の文字列。
             */
            StreamWriteInt(pipe, PROTOCOL_VERSION);
            StreamWriteInt(pipe, args.Length);

            var ss = new StreamString(pipe);

            for (int i = 0; i < args.Length; ++i) {
                ss.WriteString(args[i]);
            }

            pipe.Close();
            return 0;
        }

        /// <summary>
        /// サーバースレッドを起動する。
        /// </summary>
        /// <returns>0:成功。負の数:既に起動している。</returns>
        public int StartServer() {
            if (mServerThread != null) {
                Console.WriteLine("StartServer() Server already running!");
                return -1;
            }

            // 起動します。
            mServerStopEvent = new ManualResetEvent(false);
            mServerThreadEnd = false;
            mServerThread = new Thread(ServerThread);
            mServerThread.Start();

            Console.WriteLine("StartServer() Success.");
            return 0;
        }

        /// <summary>
        /// サーバースレッドが起動していたら終了、Join、削除します。
        /// </summary>
        public void StopServer() {
            Console.WriteLine("StopServer()");
            if (mServerStream == null) {
                Console.WriteLine("StopServer() already stopped.");
                return;
            }

            // これ以降データを受信してもコールバックを呼ばない。
            mCb = null;
            mCbObject = null;

            mServerThreadEnd = true;
            mServerStopEvent.Set();

            mServerThread.Join();
            mServerThread = null;

            mServerStopEvent.Dispose();
            mServerStopEvent = null;

            Console.WriteLine("StopServer() success.");
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // サーバースレッド。

        /// <summary>
        /// WaitForConnectionのキャンセル対応版。
        /// </summary>
        private void WaitForConnection2(NamedPipeServerStream stream, ManualResetEvent cancelEvent) {
            Exception e = null;
            var connectEvent = new AutoResetEvent(false);
            stream.BeginWaitForConnection(ar => {
                try {
                    stream.EndWaitForConnection(ar);
                } catch (Exception er) {
                    e = er;
                }
                connectEvent.Set();
            }, null);

            if (WaitHandle.WaitAny(new WaitHandle[] { connectEvent, cancelEvent }) == 1) {
                stream.Close();
            }

            if (e != null) {
                // 例外が起きました。
                throw e;
            }
        }

        private void ServerStreamClose() {
            mServerStream.Close();
            mServerStream.Dispose();
            mServerStream = null;
        }

        private void ServerThread(object data) {
            Console.WriteLine("ServerThread started.");

            while (!mServerThreadEnd) {
                System.Diagnostics.Debug.Assert(mServerStream == null);
                mServerStream = new NamedPipeServerStream(mPipeName, PipeDirection.InOut, NUM_THREADS, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                Console.WriteLine("ServerThread \"{0}\" Wait for connection...", mPipeName);

                // ここでブロックします。
                WaitForConnection2(mServerStream, mServerStopEvent);

                if (mServerThreadEnd) {
                    ServerStreamClose();
                    break;
                }

                // クライアントが接続してきた。
                Console.WriteLine("Client connected on ServerThread.");
                try {
                    int version;
                    if (!StreamReadInt(mServerStream, out version)) {
                        // バージョン番号取得失敗。
                        // 切断する。
                        Console.WriteLine("Version number recv failed.");
                        ServerStreamClose();
                        continue;
                    }

                    if (version != PROTOCOL_VERSION) {
                        Console.WriteLine("Pipe protocol version mismatch {0}", version);
                        ServerStreamClose();
                        continue;
                    }

                    Console.WriteLine("Protocol version = {0}.", version);

                    int nString;
                    if (!StreamReadInt(mServerStream, out nString)) {
                        // 文字列個数取得失敗。
                        // 切断する。
                        Console.WriteLine("args.Length recv failed.");
                        ServerStreamClose();
                        continue;
                    }

                    if (nString <= 0) {
                        Console.WriteLine("nString out of range {0}", nString);
                        ServerStreamClose();
                        continue;
                    }

                    Console.WriteLine("args.Length = {0}", nString);

                    var ss = new StreamString(mServerStream);

                    bool recvSuccess = true;

                    var ap = new ArgsParams();
                    ap.version = version;
                    for (int i = 0; i < nString; ++i) {
                        string s;
                        if (!ss.ReadString(out s)) {
                            // 受信失敗。
                            Console.WriteLine("ReadString failed.");
                            ServerStreamClose();
                            recvSuccess = false;
                            break;
                        }
                        ap.args.Add(s);
                        Console.WriteLine("args[{0}] = {1}", i, s);
                    }

                    if (recvSuccess) {
                        // コマンドライン引数受信成功。
                        if (mCb != null) {
                            mCb(mCbObject, ap);
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine("ERROR: {0}", e.Message);
                }

                ServerStreamClose();
            }

            Console.WriteLine("ServerThread end.");
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // int32と文字列の送受信。

        private static void StreamWriteInt(Stream stream, int v) {
            var b = BitConverter.GetBytes(v);
            stream.Write(b, 0, b.Length);
        }

        private static bool StreamReadInt(Stream stream, out int v) {
            v = 0;

            var b = new byte[4];

            // 4バイト読むまで繰り返す。
            int c = 0;
            while (c < 4) {
                int r = stream.Read(b, c, 4 - c);
                if (r == 0) {
                    return false;
                }
                c += r;
            }

            v = BitConverter.ToInt32(b, 0);
            return true;
        }

        internal class StreamString {
            private Stream mStream;
            private UnicodeEncoding mEnc;

            public StreamString(Stream ioStream) {
                this.mStream = ioStream;
                mEnc = new UnicodeEncoding();
            }

            /// <summary>
            /// 文字列を1個受信する。
            /// </summary>
            /// <param name="s">受信した文字列。</param>
            /// <returns>true:受信成功。false:失敗。</returns>
            public bool ReadString(out string s) {
                s = "";

                int len;
                if (!StreamReadInt(mStream, out len) || len < 0) {
                    Console.WriteLine("ReadString failed 1");
                    return false;
                }

                var inBuffer = new byte[len];
                int count = 0;
                while (count < len) {
                    int r = mStream.Read(inBuffer, count, len -count);
                    if (r == 0) {
                        // 読めない。
                        Console.WriteLine("ReadString failed 2");
                        return false;
                    }
                    count += r;
                }

                s = mEnc.GetString(inBuffer);
                return true;
            }

            /// <summary>
            /// Streamに文字列を1個書き込む。
            /// </summary>
            /// <returns>出力したバイト数。</returns>
            public int WriteString(string s) {
                var b = mEnc.GetBytes(s);
                int len = b.Length;
                if (1024 * 1024 < len) {
                    throw new ArgumentOutOfRangeException();
                }

                StreamWriteInt(mStream, len);
                if (0 < len) {
                    mStream.Write(b, 0, len);
                }
                mStream.Flush();

                return b.Length + 4;
            }
        }
    }
};
