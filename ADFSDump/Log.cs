using System;

namespace ADFSDump
{
    /// <summary>
    /// Centralized console output. In JSON mode progress/diagnostic text is routed to
    /// STDERR and human-readable data lines are suppressed entirely, so STDOUT carries
    /// only the JSON document.
    /// </summary>
    public static class Log
    {
        public static bool Json = false;

        // Progress and diagnostics: STDOUT normally, STDERR in JSON mode.
        public static void Status(string message)
        {
            (Json ? Console.Error : Console.Out).WriteLine(message);
        }

        public static void Status(string format, params object[] args)
        {
            (Json ? Console.Error : Console.Out).WriteLine(format, args);
        }

        // Human-readable data: STDOUT normally, suppressed in JSON mode.
        public static void Data(string message)
        {
            if (!Json) Console.Out.WriteLine(message);
        }

        public static void Data(string format, params object[] args)
        {
            if (!Json) Console.Out.WriteLine(format, args);
        }
    }
}
