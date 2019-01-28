using System;
using System.Threading;
using System.Threading.Tasks;

namespace SyncInvokeAsync
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            ThreadPool.SetMaxThreads(4, 10);
            int i = 0;
            for ( i = 0; i < 10; i++)
            {
                ThreadPool.QueueUserWorkItem((t) =>
                {
                    Console.WriteLine("aa："+Thread.CurrentThread.ManagedThreadId + "_" + t);
                    Task.Delay(500).GetAwaiter().GetResult();
                    //Test(t.ToString()).GetAwaiter().GetResult();
                    Console.WriteLine("cc：" + Thread.CurrentThread.ManagedThreadId + "_" + t);
                }, i);
            }
            Console.WriteLine("End!");
            Console.Read();
        }

        static async Task  Test(string t)
        {
            await Task.Delay(500);
            Console.WriteLine("bb：" + Thread.CurrentThread.ManagedThreadId+"_"+t);
        }
    }
}
