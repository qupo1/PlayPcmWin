using System;
using MultipleAppInstanceComm;

public class Program {
    public void RecvMsgCallback(object o, MultipleAppInstanceMgr.ReceivedMessage msg) {
        Console.WriteLine("RecvMsgCallback() msg from another app instance.");
        Console.WriteLine("  Protocol version = {0}", msg.protocolVersion);
        Console.WriteLine("  msg.Length = {0}", msg.args.Count);
        for (int i = 0; i < msg.args.Count; ++i) {
            Console.WriteLine("    msg[{0}] = {1}", i, msg.args[i]);
        }
    }
    
    /// <summary>
    /// 最初に起動したアプリとなって、起動引数を受信します。
    /// </summary>
    /// <returns>成功すると0。失敗すると負の数。</returns>
    public int Run(string[] args) {
        var maim = new MultipleAppInstanceMgr("MultipleAppInstanceTest");
        if (maim.IsAppAlreadyRunning()) {
            Console.WriteLine("Error: App is already running. Program will exit.");
            return 1;
        }

        // 最初に起動したアプリである。
        // 起動引数を受け取るサーバー(名前付きパイプ)を起動します。
        maim.ServerStart(RecvMsgCallback, this);

        Console.WriteLine("Press Enter to stop server.");
        Console.ReadLine();

        maim.ServerStop();

        return 0;
    }

    public static void Main(string[] args) {
        var self = new Program();
        self.Run(args);
    }
}

