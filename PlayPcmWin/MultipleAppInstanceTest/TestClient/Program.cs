using System;
using MultipleAppInstanceComm;

namespace TestClient {
    public class Program {
        /// <summary>
        /// 既に起動しているアプリに起動引数を送ります。
        /// </summary>
        /// <returns>成功すると0。失敗すると負の数。</returns>
        public int Run(string[] args) {
            var maim = new MultipleAppInstanceMgr("MultipleAppInstanceTest");
            if (!maim.IsAppAlreadyRunning()) {
                Console.WriteLine("Please start server process. \nProgram will exit.");
                return 1;
            }

            Console.WriteLine("App is already running (this is expected).");
            // 既に起動しているインスタンスに接続し、コマンドライン引数を送ります。
            maim.ClientSendMsgToServer(args);
            // 送り終わったらプログラム終了。
            return 0;
        }

        public static void Main(string[] args) {
            var self = new Program();
            self.Run(args);
        }
    }
}
