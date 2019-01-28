using log4net;
using log4net.Appender;
using log4net.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICT104ProtocolWrapper
{
    public static class Logger
    {
        public static readonly ILog LogWriter = GetAppLogger();

        static Logger()
        {
            log4net.Config.XmlConfigurator.Configure(new System.IO.FileInfo("log4net.xml"));
        }

        /// <summary>
        /// AppLogger를 가져온다.
        /// </summary>
        /// <returns>DeviceLogger</returns>
        private static ILog GetAppLogger()
        {
            RollingFileAppender roller = new RollingFileAppender();
            roller.AppendToFile = true;
            roller.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller.MaxSizeRollBackups = 10;
            roller.MaximumFileSize = "3MB";
            roller.File = $@"logs\{System.Reflection.Assembly.GetEntryAssembly().GetName().Name+"_BillAccepter"}.log";

            roller.StaticLogFileName = true;
            roller.Layout = new PatternLayout("%d{yyMMdd HH:mm:ss.fff} %-5p : %m%n");
            roller.LockingModel = new FileAppender.MinimalLock();
            roller.ActivateOptions();


            DummyLogger dummyILogger = new DummyLogger("AppLog");
            // 요걸 연결안해주면 log4net 안에서 Null참조 예외가 발생한다.
            dummyILogger.Hierarchy = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
            dummyILogger.Level = log4net.Core.Level.Info;
            dummyILogger.AddAppender(roller);

            return new NSLog(dummyILogger);
        }
    }

    public sealed class DummyLogger : log4net.Repository.Hierarchy.Logger
    {
        // Methods
        internal DummyLogger(string name)
            : base(name)
        {
        }
    }

    public class NSLog : log4net.Core.LogImpl
    {
        public NSLog(DummyLogger log) : base(log)
        {
        }


        public override void Info(object message)
        {
            base.Info(message);
        }
        public override void Error(object message)
        {
            base.Error(message);
        }
        public override void Warn(object message)
        {
            base.Warn(message);
        }
        public override void Error(object message, Exception exception)
        {
            base.Error(message, exception);
        }
        public override void Warn(object message, Exception exception)
        {
            base.Warn(message, exception);
        }
    }
}
