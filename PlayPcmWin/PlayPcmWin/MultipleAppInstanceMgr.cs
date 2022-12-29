using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace MultipleAppInstanceComm {

    /// <summary>
    /// 他のアプリインスタンスからのメッセージ(コマンドライン引数等)を受信した時呼ばれるコールバックのsignature。
    /// </summary>
    /// <param name="cbObject">サーバースレッド起動時にセットされたcbObject。</param>
    /// <param name="msg">受信したメッセージ。</param>
    public delegate void MultipleAppInstanceRecvMsgCallback(object cbObject, MultipleAppInstanceMgr.ReceivedMessage msg);

    /// <summary>
    /// 同一アプリの複数インスタンス起動を検出。
    /// 最初に起動したアプリインスタンスに起動引数を送る。
    /// </summary>
    public class MultipleAppInstanceMgr {

        /// <summary>
        /// 他のアプリインスタンスから受信したメッセージ。
        /// </summary>
        public class ReceivedMessage {
            public int protocolVersion;
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

        private MultipleAppInstanceRecvMsgCallback mCb;
        private object mCbObject;

        /// <summary>
        /// ctor。
        /// </summary>
        /// <param name="appName">名前付きパイプと名前付きミューテックスの名前。ユニークなアプリの名前をセットする。</param>
        public MultipleAppInstanceMgr(string appName) {
            mPipeName  = appName;
            mMutexName = appName;

            mCb = null;
            mCbObject = null;
        }

        public void Term() {
            ServerStop();

            if (mAppInstanceMtx != null) {
                mAppInstanceMtx.Dispose();
                mAppInstanceMtx = null;
            }
        }

        /// <summary>
        /// このアプリが既に起動しているか調べる。
        /// </summary>
        /// <returns>true: アプリが既に起動している。false: このアプリの唯一のインスタンスである。</returns>
        public bool IsAppAlreadyRunning() {
            if (mOnlyInstance != null && mOnlyInstance == true) {
                // 前回調べた結果、このインスタンスが唯一のインスタンスであった。
                return false;
            }

            bool onlyInstance;
            mAppInstanceMtx = new Mutex(true, mMutexName, out onlyInstance);
            if (!onlyInstance) {
                // 同名の名前付きミューテックスが既に存在する：他にインスタンスが存在。
                return true;
            }

            // このアプリの唯一のインスタンスである事が判った。
            mOnlyInstance = true;

            // 名前付きMutexのガベコレ消滅防止。
            GC.KeepAlive(mAppInstanceMtx);

            return false;
        }

        /// <summary>
        /// 最初に起動したアプリインスタンスに起動引数を送ります。
        /// </summary>
        /// <returns>成功すると0。失敗すると負の数。</returns>
        public int ClientSendMsgToServer(string[] args) {
            var stream = new NamedPipeClientStream(".", mPipeName,
                    PipeDirection.InOut, PipeOptions.None,
                    TokenImpersonationLevel.Impersonation);

            Console.WriteLine("Connecting to named stream server \"{0}\" ...\n", mPipeName);
            try {
                stream.Connect(CONNECT_TIMEOUT_MSEC);
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
            StreamWriteInt(stream, PROTOCOL_VERSION);
            StreamWriteInt(stream, args.Length);

            for (int i = 0; i < args.Length; ++i) {
                StreamWriteString(stream, args[i]);
            }

            stream.Close();
            return 0;
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // サーバースレッド。

        /// <summary>
        /// サーバースレッドを起動する。
        /// </summary>
        /// <param name="cb">後から起動したインスタンスからメッセージを受け取るコールバック。必要ないときはnullをセット。</param>
        /// <param name="cbObject">コールバック関数の第1引数として渡されるオブジェクト。</param>
        /// <returns>0:成功。負の数:既に起動しているので失敗。</returns>
        public int ServerStart(MultipleAppInstanceRecvMsgCallback cb, object cbObject) {
            if (mServerThread != null) {
                Console.WriteLine("ServerStart() Server already running!");
                return -1;
            }

            mCb = cb;
            mCbObject = cbObject;

            // 起動します。
            mServerStopEvent = new ManualResetEvent(false);
            mServerThreadEnd = false;
            mServerThread = new Thread(NamedPipeServerThread);
            mServerThread.Start();

            //Console.WriteLine("ServerStart() Success.");
            return 0;
        }

        /// <summary>
        /// サーバースレッドが呼び出すコールバックを変更する。
        /// </summary>
        /// <param name="cb">新しいコールバック。nullの場合呼ばなくなる。</param>
        /// <param name="cbObject">コールバックの第1引数。</param>
        public void ServerUpdateCallback(MultipleAppInstanceRecvMsgCallback cb, object cbObject) {
            mCb = cb;
            mCbObject = cbObject;
        }

        /// <summary>
        /// サーバースレッドが起動していたら終了、Join、削除します。
        /// </summary>
        public void ServerStop() {
            //Console.WriteLine("ServerStop()");
            // これ以降データを受信してもコールバックを呼ばない。
            mCb = null;
            mCbObject = null;

            if (mServerThread != null) {
                // サーバースレッドを止めます。

                mServerThreadEnd = true;
                mServerStopEvent.Set();

                mServerThread.Join();
                mServerThread = null;

                mServerStopEvent.Dispose();
                mServerStopEvent = null;
            }

            Debug.Assert(mServerStream == null);
            
            //Console.WriteLine("ServerStop() success.");
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // サーバースレッド。

        /// <summary>
        /// WaitForConnectionのキャンセル対応版。
        /// </summary>
        private static void WaitForConnection2(NamedPipeServerStream stream, ManualResetEvent cancelEvent) {
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

        /// <summary>
        /// クライアントからのメッセージを受信。
        /// </summary>
        /// <returns>true:成功。false:途中で切断が起きたので失敗。</returns>
        private bool ServerRecvMsg(out ReceivedMessage msg_return) {
            msg_return = new ReceivedMessage();

            try {
                // データプロトコルはClientSendArgsToServer参照。

                int version;
                if (!StreamReadInt(mServerStream, out version)) {
                    // バージョン番号取得失敗。
                    // 切断する。
                    Console.WriteLine("ServerRecvMsg Version number recv failed.");
                    return false;
                }

                if (version != PROTOCOL_VERSION) {
                    Console.WriteLine("ServerRecvMsg Pipe protocol protocolVersion mismatch {0}", version);
                    return false;
                }

                //Console.WriteLine("ServerRecvMsg Protocol protocolVersion = {0}.", version);

                int nString;
                if (!StreamReadInt(mServerStream, out nString)) {
                    // 文字列個数取得失敗。
                    // 切断する。
                    Console.WriteLine("ServerRecvMsg args.Length recv failed.");
                    return false;
                }

                if (nString < 0) {
                    Console.WriteLine("ServerRecvMsg args.Length out of range {0}", nString);
                    return false;
                }

                //Console.WriteLine("ServerRecvMsg args.Length = {0}", nString);

                // 受信データをapに入れる。
                msg_return.protocolVersion = version;

                if (0 < nString) {
                    // コマンドライン引数があるので受信。
                    for (int i = 0; i < nString; ++i) {
                        string s;
                        if (!StreamReadString(mServerStream, out s)) {
                            // 受信失敗。
                            Console.WriteLine("ServerRecvMsg ReadString {0} failed.", i);
                            return false;
                        }
                        msg_return.args.Add(s);
                        //Console.WriteLine("ServerRecvMsg args[{0}] = {1}", i, s);
                    }
                }

                // すべて成功。
                return true;
            } catch (Exception e) {
                Console.WriteLine("ServerRecvMsg Error: {0}", e.Message);
            }

            // 途中で例外が発生し失敗。
            return false;
        }

        /// <summary>
        /// 名前付きパイプの待ち受けサーバースレッド。
        /// </summary>
        private void NamedPipeServerThread(object data) {
            Console.WriteLine("NamedPipeServerThread \"{0}\" started.", mPipeName);

            while (!mServerThreadEnd) {

                Debug.Assert(mServerStream == null);
                mServerStream = new NamedPipeServerStream(mPipeName, PipeDirection.InOut, NUM_THREADS, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                //Console.WriteLine("NamedPipeServerThread \"{0}\" Wait for connection...", mPipeName);

                // ここでブロックします。
                WaitForConnection2(mServerStream, mServerStopEvent);

                if (mServerThreadEnd) {
                    ServerStreamClose();
                    break;
                }

                // クライアントが接続してきた。
                // メッセージを受信します。
                //Console.WriteLine("Client connected on NamedPipeServerThread.");

                ReceivedMessage msg;
                if (ServerRecvMsg(out msg)) {
                    // 受信成功。
                    if (mCb != null) {
                        mCb(mCbObject, msg);
                    }
                } else {
                    // 受信失敗。
                }

                ServerStreamClose();
            }

            Console.WriteLine("NamedPipeServerThread \"{0}\" end.", mPipeName);
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
        // int32と文字列の送受信。

        /// <summary>
        /// 指定バイト数のbyte arrayを受信。
        /// </summary>
        /// <param name="tgtBytes">必要バイト数</param>
        /// <param name="b_inout">受信したデータの置き場。</param>
        /// <returns>true: 受信成功。false:途中で接続が切断し失敗。</returns>
        private static bool StreamRecvBytes(Stream stream, int bytes, out byte[] b_return) {
            b_return = new byte[bytes];

            // stream.readは指定バイト数よりも
            // 少ないバイト数を受信して戻すことがある。
            // bytes読むまで繰り返す。

            int c = 0;
            while (c < bytes) {
                int r = stream.Read(b_return, c, bytes - c);
                if (r == 0) {
                    // 接続が切断した場合。失敗。
                    return false;
                }
                c += r;
            }

            return true;
        }

        /// <summary>
        /// int32を1個(4バイト)送出。
        /// </summary>
        private static void StreamWriteInt(Stream stream, int v) {
            var b = BitConverter.GetBytes(v);
            
            Debug.Assert(b.Length == 4);

            stream.Write(b, 0, b.Length);
            stream.Flush();
        }

        /// <summary>
        /// int32を1個(4バイト)受信。
        /// </summary>
        /// <returns>true:成功。false:途中で接続が切れたので失敗。</returns>
        private static bool StreamReadInt(Stream stream, out int v) {
            v = 0;

            byte[] b;
            if (!StreamRecvBytes(stream, 4, out b)) {
                return false;
            }

            v = BitConverter.ToInt32(b, 0);
            return true;
        }

        /// <summary>
        /// 文字列を1個受信。
        /// </summary>
        /// <param name="s">受信した文字列。</param>
        /// <returns>true:受信成功。false:失敗。</returns>
        private static bool StreamReadString(Stream stream, out string s) {
            s = "";

            int len;
            if (!StreamReadInt(stream, out len) || len < 0) {
                Console.WriteLine("StreamReadString failed to recv size.");
                return false;
            }

            if (len == 0) {
                // 0バイトの文字列を受信した。
                return true;
            }

            // 1バイト以上の文字列を受信。
            byte[] b;
            if (!StreamRecvBytes(stream, len, out b)) {
                // 失敗。
                Console.WriteLine("StreamReadString failed to recv string.");
                return false;
            }

            var enc = new UnicodeEncoding();
            s = enc.GetString(b);
            return true;
        }

        /// <summary>
        /// 文字列を1個送信。
        /// </summary>
        /// <returns>出力したバイト数。</returns>
        private static int StreamWriteString(Stream stream, string s) {
            var enc = new UnicodeEncoding();
            
            var b = enc.GetBytes(s);

            if (int.MaxValue - 4 < b.LongLength) {
                throw new ArgumentOutOfRangeException();
            }

            int len = b.Length;

            StreamWriteInt(stream, len);

            if (0 < len) {
                stream.Write(b, 0, len);
            }

            stream.Flush();

            return 4 + len;
        }
    }
};
