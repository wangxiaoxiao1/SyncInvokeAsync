using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace AsyncTestConsoleApp
{
    class Program
    {
        private static readonly TaskFactory MyTaskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        private static void RunSync(string request, Func<Task> func)
        {
            var cultureUi = CultureInfo.CurrentUICulture;
            var culture = CultureInfo.CurrentCulture;
            Console.WriteLine("beforenew:  {0}等待,The Request: {1}", Thread.CurrentThread.ManagedThreadId, request);
            MyTaskFactory.StartNew(() =>
            {
                Console.WriteLine("new:  {0}占用,The Request: {1}", Thread.CurrentThread.ManagedThreadId, request);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = cultureUi;
                return func();
            }).Unwrap().GetAwaiter().GetResult();
            Console.WriteLine("afternew:  {0}释放,The Request: {1}", Thread.CurrentThread.ManagedThreadId, request);
        }
        public static void PrintThreadLog(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "默认";
            }
            ThreadPool.GetMinThreads(out var minWorker0, out var minIOC0);
            ThreadPool.GetMaxThreads(out var maxWorker0, out var maxIOC0);
            ThreadPool.GetAvailableThreads(out var aWorkerThreads, out var aCompletionPortThreads);
            Console.WriteLine($"{title}：minWorker:{minWorker0} minIOC:{minIOC0}");
            Console.WriteLine($"{title}：maxWorker:{maxWorker0} maxIOC:{maxIOC0}");
            Console.WriteLine($"{title}：AvailableWorker:{maxWorker0} AvailablemaxCompletionPortThreads:{maxIOC0}");
        }
        static void Main(string[] args)
        {
            ThreadPool.SetMinThreads(2, 200);
            ThreadPool.SetMaxThreads(5, 200);
            PrintThreadLog("");
            IConnection connection = new Connection(new User());

            int waitingStart = 0, inProgress = 0;

            int sleepTime = 150;

            int waited = 0;
            int i = 0;
            //for (; ; )
            //{
                Interlocked.Increment(ref waitingStart);
                // 模拟客户端请求
                // assume IConnection interface can't be changed to async
                ThreadPool.QueueUserWorkItem(
                    _ =>
                    {
                        i++;
                        Interlocked.Decrement(ref waitingStart);
                        Interlocked.Increment(ref inProgress);
                        Console.WriteLine("beforeRequest: 占用{0},The Request: {1}", Thread.CurrentThread.ManagedThreadId, "request" + i);
                        //await connection.OnRequestReceivedAsync("request");
                        connection.OnRequestReceived("request" + i);
                        Interlocked.Decrement(ref inProgress);
                    });
                //     Console.WriteLine("main: 客户端请求线程{0}", Thread.CurrentThread.ManagedThreadId);
                ThreadPool.GetAvailableThreads(out int workThreads, out int conoletionPortThreads);

                string msg = string.Format(
                    "Requests in queue: {0}, requests in progress: {1}, current sleep time: {2} ,the available workThreads:{3},the conoletionPortThreads:{4}",
                    Volatile.Read(ref waitingStart),
                    Volatile.Read(ref inProgress),
                    Volatile.Read(ref sleepTime),
                    workThreads,
                    conoletionPortThreads
                    );

                bool poolAlive = CheckPoolAlive();
                if (waited > 500 || !poolAlive)
                {
                    //Console.Clear();
                    Console.WriteLine(msg);
                    waited = 0;
                }
                if (!poolAlive)
                    throw new InvalidOperationException("ThreadPool deadlocked! " + msg);

                if (sleepTime > 30) sleepTime--;
                Thread.Sleep(sleepTime);
                waited += sleepTime;
            //}
        }

        static bool CheckPoolAlive()
        {

            using (ManualResetEventSlim ok = new ManualResetEventSlim(false))
            {
                ThreadPool.QueueUserWorkItem(_ => ok.Set());
                return ok.Wait(30000);
            }
        }

        interface IConnection
        {
            //同步调异步
            int OnRequestReceived(string request);
            //异步调异步
            Task OnRequestReceivedAsync(string request);
            //同步调同步
            void OnRequestReceived2(string request);
        }

        class Connection : IConnection
        {
            readonly User _user;

            public Connection(User user)
            {
                _user = user;
            }

            public int OnRequestReceived(string request)
            {
                // Console.WriteLine("Requesting: 占用{0},The Request: {1}", Thread.CurrentThread.ManagedThreadId, request);
                RunSync(request, () => _user.HandleRequest(request));
                // var ret = _user.HandleRequest(request).GetAwaiter().GetResult();
                Console.WriteLine("afterReceive: 释放{0},The Request: {1}", Thread.CurrentThread.ManagedThreadId, request);
                return 1;
            }
            public void OnRequestReceived2(string request)
            {
                _user.HandleRequest2(request);
            }

            public async Task OnRequestReceivedAsync(string request)
            {
                // 同步调用异步
                var task = await _user.HandleRequest(request).ConfigureAwait(false);
                if (!task) throw new InvalidOperationException();
                return;
            }
        }

        class User
        {
            public async Task<bool> HandleRequest(string request)
            {
                int rnd = Rnd.Next(30, 700);
                Console.WriteLine("beforeawait: 释放{0} 在{2}毫秒后请求,The Request: {1}", Thread.CurrentThread.ManagedThreadId, request, rnd);
                //下面这句会把当前控制权交给调用者
                await Task.Delay(rnd).ConfigureAwait(false); // 模拟各种操作
                Console.WriteLine("afterawait: {0}占用,The Request: {1}", Thread.CurrentThread.ManagedThreadId, request);
                return true;
            }
            public bool HandleRequest2(string request)
            {
                Thread.Sleep(Rnd.Next(30, 700));
                //Task.Delay(Rnd.Next(30, 700)).GetAwaiter().GetResult();          
                return true;
            }
        }

        [ThreadStatic]
        static Random _rnd;

        static Random Rnd => _rnd ?? (_rnd = new Random(Guid.NewGuid().GetHashCode()));
    }
}