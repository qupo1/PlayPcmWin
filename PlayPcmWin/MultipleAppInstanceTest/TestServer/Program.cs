using System;
using MultipleAppInstanceComm;

public class Program {
    public void RecvArgsCallback(object o, MultipleAppInstanceMgr.ArgsParams args) {
        Console.WriteLine("RecvArgsCallback() args from another instance.");
        Console.WriteLine("total {0}", args.args.Count);
        for (int i = 0; i < args.args.Count; ++i) {
            Console.WriteLine("  args[{0}] = {1}", i, args.args[i]);
        }
    }
    
    /// <summary>
    /// 最初に起動したアプリとなって、起動引数を受信します。
    /// </summary>
    /// <returns>成功すると0。失敗すると負の数。</returns>
    public int Run(string[] args) {
        var maim = new MultipleAppInstanceMgr("MultipleAppInstanceTest", RecvArgsCallback, this);
        if (maim.IsAppAlreadyRunning()) {
            Console.WriteLine("Error: App is already running. Program will exit.");
            return 1;
        }

        // 最初に起動したアプリである。
        // 起動引数を受け取るサーバー(名前付きパイプ)を起動します。

        maim.StartServer();

        Console.WriteLine("Press Enter to stop server.");
        Console.ReadLine();

        maim.StopServer();

        Console.WriteLine("Server stopped. Press Enter to end app.");
        Console.ReadLine();

        return 0;
    }

    public static void Main(string[] args) {
        var self = new Program();
        self.Run(args);
    }
}

