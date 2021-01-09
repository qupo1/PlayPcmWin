using System;
using MultipleAppInstanceComm;

namespace TestClient {
    public class Program {
        /// <summary>
        /// 既に起動しているアプリに起動引数を送ります。
        /// </summary>
        /// <returns>成功すると0。失敗すると負の数。</returns>
        public int Run(string[] args) {
            var maim = new MultipleAppInstanceMgr("MultipleAppInstanceTest", null, null);
            if (!maim.IsAppAlreadyRunning()) {
                Console.Write("Please start server process. Program will exit.");
                return 1;
            }

            Console.WriteLine("App is already running (this is expected).");
            // 既に起動しているインスタンスに、コマンドライン引数を送ります。
            maim.SendArgsToServer(args);
            // 送り終わったらプログラム終了。
            return 0;
        }

        public static void Main(string[] args) {
            var self = new Program();
            self.Run(args);
        }
    }
}
