
#define ENABLE_ASSERTIONS

using System.Diagnostics;

namespace Peeper.Util
{
    public static class ExceptionHandling
    {

        [System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("ENABLE_ASSERTIONS")]
        public static void Assert(bool condition, string? message = null)
        {
#if DEBUG
            Debug.Assert(condition, message);
#else
            if (!condition)
            {
                throw new AssertionException("Assertion failed: " + message + Environment.NewLine);
            }
#endif
        }

        public static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;

            if (e.GetType() == typeof(AssertionException))
            {
                //  This is "handled"
                return;
            }

            Log("An UnhandledException occurred!\r\n" + e.ToString());
            using (FileStream fs = new FileStream(@".\crashlog.txt", FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                using StreamWriter sw = new StreamWriter(fs);

                sw.WriteLine("An UnhandledException occurred!\r\n" + e.ToString());

                sw.Flush();
            }

            Console.WriteLine("info string I'm going to crash! Exception: ");
            foreach (string s in e.ToString().Split(Environment.NewLine))
            {
                Console.WriteLine("info string " + s);
                Thread.Sleep(10);
            }
        }

    }

    public class AssertionException : Exception
    {
        public AssertionException() { }
        public AssertionException(string message) : base(message) { }
        public AssertionException(string message, Exception inner) : base(message, inner) { }
    }
}