using System;
using System.ServiceProcess;

namespace printer_info
{
    internal static class Program
    {
        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
        static void Main(string[] args)
        {
            // --test 模式：執行 API 驗證測試
            if (args != null && Array.IndexOf(args, "--test") >= 0)
            {
                DemoTest.Run(args);
                return;
            }

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service1()
            };
            ServiceBase.Run(ServicesToRun);

            //RunInteractive(ServicesToRun);
        }

        static void RunInteractive(ServiceBase[] servicesToRun)
        {
            // 利用Reflection取得非公開之 OnStart() 方法資訊
            var onStartMethod = typeof(ServiceBase).GetMethod("OnStart",
                 System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // 執行 OnStart 方法
            foreach (ServiceBase service in servicesToRun)
            {
                Console.Write("Starting {0}...", service.ServiceName);
                onStartMethod.Invoke(service, new object[] { new string[] { } });
                Console.Write("Started");
            }

            Console.WriteLine("Press any key to stop the services");
            Console.ReadKey();

            // 利用Reflection取得非公開之 OnStop() 方法資訊
            var onStopMethod = typeof(ServiceBase).GetMethod("OnStop",
                 System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // 執行 OnStop 方法
            foreach (ServiceBase service in servicesToRun)
            {
                Console.Write("Stopping {0}...", service.ServiceName);
                onStopMethod.Invoke(service, null);
                Console.WriteLine("Stopped");
            }
        }
    }
}
