using System;
using System.IO;

namespace ICT104ProtocolWrapper
{
    public static class Logger
    {
        private static string filePath = null;
        public static DateTime lastLogTime;
        static Logger()
        {
            filePath = ResetFileNamebyDate();
            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }
        }
        internal static void Info(string param)
        {
            try
            {
                if (lastLogTime.Date != DateTime.Now.Date)
                {
                    filePath = ResetFileNamebyDate();
                }
                lastLogTime = DateTime.Now;
                using (StreamWriter sw = new StreamWriter(filePath, true))
                {
                    sw.WriteLine($"{lastLogTime} INFO : {param}");
                }
            }
            catch
            {

            }
        }

        internal static void Error(string param)
        {
            try
            {
                if (lastLogTime.Date != DateTime.Now.Date)
                {
                    filePath = ResetFileNamebyDate();
                }
                lastLogTime = DateTime.Now;
                using (StreamWriter sw = new StreamWriter(filePath, true))
                {
                    sw.WriteLine($"{lastLogTime} ERROR : {param}");
                }
            }
            catch
            {

            }
        }

        internal static void Warn(string param)
        {
            try
            {
                if (lastLogTime.Date != DateTime.Now.Date)
                {
                    filePath = ResetFileNamebyDate();
                }
                lastLogTime = DateTime.Now;
                using (StreamWriter sw = new StreamWriter(filePath, true))
                {
                    sw.WriteLine($"{lastLogTime} WARN : {param}");
                }
            }
            catch
            {

            }
        }

        public static string ResetFileNamebyDate()
        {
            try
            {
                return $"logs/log{DateTime.Now.ToShortDateString()}_{System.Reflection.Assembly.GetEntryAssembly().GetName().Name}_BillAccepter.log";
            }
            catch
            {
                return $"logs/logUndefinableDateTime_{System.Reflection.Assembly.GetEntryAssembly().GetName().Name}_BillAccepter.log";
            }
        }

        internal static void Info(object obj)
        {
            Info(obj.ToString());
        }

        internal static void Error(object obj)
        {
            Error(obj.ToString());
        }

        internal static void Warn(object obj)
        {
            Warn(obj.ToString());
        }
    }
}
