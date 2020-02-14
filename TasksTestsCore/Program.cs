// enable TEST## to run specific test case
#define TEST__
// enabling TESTASYNC doesn't have effect
#define TESTASYNCx

using System;
using System.Threading;
using System.Threading.Tasks;

namespace TasksTestsCore
{
    class Program
    {
#if TESTASYNC
        private static async Task Test()
#else
        private static void Test()
#endif
        {
            try
            {
#if TESTASYNC
                // thrown exception is handled in the catch
                // though the call is blocking
                await FireAndForgetTask();
#elif TEST1
                // UnhandledException (app crashes)
                FireAndForgetVoid();
#elif TEST2
                // UnhandledException (app crashes)
                _ = Task.Run(FireAndForgetVoid);
#elif TEST3
                // UnobservedTaskException
                _ = FireAndForgetTask();
#elif TEST4
                // UnobservedTaskException
                _ = Task.Run(FireAndForgetTask);
#elif TEST5
                // UnobservedTaskException
                _ = Task.Run(async () => await FireAndForgetTask());
#elif TEST6
                // UnobservedTaskException
                _ = Task.Run(Throw);
#elif TEST7
                // thrown exception is captured in t.Exception property,
                // but leads to UnobservedTaskException,
                // because t.Exception is not accessed
                _ = FireAndForgetTask().ContinueWith(t =>
                {
                    Console.WriteLine($"ContinueWith: {t.Status}");
                });
#elif TEST8
                // thrown exception is captured in t.Exception property.
                // and works fine since t.Exception property is accessed
                _ = FireAndForgetTask().ContinueWith(t =>
                {
                    Console.WriteLine($"ContinueWith: {t.Status} {t.Exception}");
                });
#elif TEST9
                // thrown exception is handled by Forget
                FireAndForgetTask().Forget();
#elif TEST10
                // thrown exception is handled by Forget
                Task.Run(Throw).Forget();
#endif
            }
            catch (Exception e)
            {
                Console.WriteLine($"Catch: {e}");
            }
        }

        static void Main(string[] args)
        {
            // triggered immediately
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // triggered when GC calls ~TaskExceptionHolder finalizers
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // act
            try
            {
#if TESTASYNC
                Test().GetAwaiter().GetResult();
#else
                Test();
#endif
            }
            catch (Exception e)
            {
                Console.WriteLine($"Main: {e}");
            }

            Thread.Sleep(1000);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Console.WriteLine("Press enter to exit...");
            Console.ReadLine();
        }

#if !TESTASYNC && (TEST1 || TEST2)
        private static async void FireAndForgetVoid()
#else 
        private static async Task FireAndForgetTask()
#endif
        {
            await Task.Delay(500);
            Throw();
        }

        private static void Throw() => throw new NotImplementedException("test");

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine($"UnobservedTaskException: {e.Exception?.GetBaseException()}");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"UnhandledException: {e.ExceptionObject}");
        }
    }

    public static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            // note: this code is inspired by a tweet from Ben Adams. If someone find the link to the tweet I'll be pleased to add it here.
            // Only care about tasks that may fault (not completed) or are faulted,
            // so fast-path for SuccessfullyCompleted and Canceled tasks.
            if (!task.IsCompleted || task.IsFaulted)
            {
                // use "_" (Discard operation) to remove the warning IDE0058: Because this call is not awaited, execution of the current method continues before the call is completed
                // https://docs.microsoft.com/en-us/dotnet/csharp/discards#a-standalone-discard
                _ = ForgetAwaited(task);
            }


        }

        // Allocate the async/await state machine only when needed for performance reason.
        // More info about the state machine: https://blogs.msdn.microsoft.com/seteplia/2017/11/30/dissecting-the-async-methods-in-c/
        private static async Task ForgetAwaited(Task task)
        {
            try
            {
                // No need to resume on the original SynchronizationContext, so use ConfigureAwait(false)
                await task.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Forget: {e}");
                // Nothing to do here
            }
        }
    }
}
