using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GstTool.Utils
{
    /**
     * 日志写入组件(**多线程写日志队列，单线程写日志文件**)
     * 使用方法：
     * 在使用的地方调用 Log. I D W E
     * 在关闭的地方调用 Log.OnExit();
     * 功能描述：1.多类型日志自动归类，将不同类型的日志写入不同的文件中。例如：登录日志，操作日志，错误日志等
     * 2.批量写入日志，减少IO操作   
     * 3.延时写入功能，当日志大小小于某个阈值时不写入
     * 4.强制写入功能，当日志在批处理列表中保留时间超过某个阈值时强制写入
     * 5.日志分割(按天，按体积分割)
     */
    public class Log
    {
        private const string TimeStampFormat = "MM-dd HH:mm:ss.fff";
        private const string TimeStampFormatFilename = "yyyy-MM-dd";
        private const int MaxWriteErrorTime = 10; //最大连续写入错误次数
        private static readonly Log Instance = new();
        private static int _maxByteSize = 1024 * 1; //当单个缓存日志达到多少byte时写入文件
        private static int _maxFileSize = 20 * 1024 * 1024; //单个日志文件的大小
        private static int _maxMilliseconds = 10; //最大相隔时间，不管缓存区中的日志大小到不到_maxByteSize都写入硬盘
        private static bool _enableDebug = true;

        private readonly ConcurrentQueue<LogModel> _logModelQueue = new(); //日志队列      

        //对不同类型的日志进行区分合并
        private readonly LogBuffer _myLogBuffer = new() { StartTime = DateTime.Now };
        private readonly Thread _thread;
        private int _currentWriteErrorTime; //当前连续写入错误次数
        private bool _isStart;
        private string _savePath = Environment.CurrentDirectory + "\\Log\\";
        private LogModel _tmpLog;

        /// <summary>
        ///     开始写入日志，系统初始化时调用：向磁盘写入日志需要满足同一类型日志条数>=batchCount条或者同一类型日志存在超过maxSecond秒
        /// </summary>
        private Log()
        {
            _thread = new Thread(RunnableDigest) { IsBackground = true };
            _thread.Start();
        }

        /// <param name="savePath">日志保存的基目录(全路径)，</param>
        public void SetSavePath(string savePath = "")
        {
            if (null != savePath) Instance._savePath = savePath;
        }

        /// <param name="maxByteSize">当日志缓存队列日志达到多少byte时写入文件,默认64KB</param>
        public static void SetMaxByteSize(int maxByteSize)
        {
            if (maxByteSize < 1024) return;
            _maxByteSize = maxByteSize;
        }

        /// <param name="maxFileSize">单个日志文件大小，默认1M</param>
        public void SetMaxFileSize(int maxFileSize)
        {
            if (maxFileSize < 1024) return;
            _maxFileSize = maxFileSize;
        }

        /// <param name="maxMilliseconds">当同一类型日志存在且超过maxSecond秒时向磁盘写入日志</param>
        public static void SetMaxMilliseconds(int maxMilliseconds)
        {
            if (maxMilliseconds < 500) return;
            _maxMilliseconds = maxMilliseconds;
        }

        public static void EnableDebug(bool enable)
        {
            _enableDebug = enable;
        }

        public void AgainStart()
        {
            Instance._isStart = true;
        }

        public static void OnExit()
        {
            Instance.Flush();
        }

        private void RunnableDigest()
        {
            _isStart = true;
            while (_isStart)
            {
                if (_logModelQueue.Count > 0)
                    TransferQueue2Buffer();
                else
                    Thread.Sleep(50);

                TrimLogBuffer();
            }
        }

        private void TransferQueue2Buffer()
        {
            _logModelQueue.TryDequeue(out _tmpLog);
            if (_tmpLog == null) return;

            _myLogBuffer.Builder = _myLogBuffer.Builder.AppendFormat("{0} {1} {2}" + Environment.NewLine,
                _tmpLog.CreatedTime.ToString(TimeStampFormat),
                _tmpLog.Tag,
                _tmpLog.Msg
            );
            _tmpLog = null;
        }

        private void TrimLogBuffer()
        {
            //缓存日志，定期写入文件，空间换时间 避免频繁操作硬盘 造成IO开销过大       
            var second = (DateTime.Now - _myLogBuffer.StartTime).TotalMilliseconds;
            if (_myLogBuffer.Builder.Length > _maxByteSize || second >= _maxMilliseconds)
            {
                //写入日志
                if (!Write2File(_myLogBuffer.Builder.ToString())) return;
                _myLogBuffer.StartTime = DateTime.Now;
                _myLogBuffer.Builder.Clear();
            }
        }

        private void Flush()
        {
            //写入日志
            if (_myLogBuffer.Builder.Length <= 0) return;
            if (!Write2File(_myLogBuffer.Builder.ToString())) return;
            _myLogBuffer.StartTime = DateTime.Now;
            _myLogBuffer.Builder.Clear();
        }

        /// <summary>
        ///     关闭线程
        /// </summary>
        public void Abort()
        {
            Instance._isStart = false;
            Instance._thread.Abort();
        }

        public static void D(string content)
        {
            if (_enableDebug) Instance.Write(content, "D");
        }

        public static void I(Exception exception)
        {
            var info = GetExceptionInfo(exception);
            Instance.Write(info, "I");
        }

        public static void I(string content)
        {
            Instance.Write(content, "I");
        }

        public static void E(Exception exception)
        {
            var info = GetExceptionInfo(exception);
            Instance.Write(info, "E");
            Instance.Flush();
        }

        public static void E(string content)
        {
            Instance.Write(content, "E");
            Instance.Flush();
        }

        private static string GetExceptionInfo(Exception exception)
        {
            if (null == exception) return "exception is null";
            var builder = new StringBuilder();
            builder.AppendLine("异常信息：" + exception.Message);
            builder.AppendLine(" 异常源：" + exception.Source);
            builder.AppendLine(" 调用堆栈：\n   " + exception.StackTrace.Trim());
            builder.AppendLine(" 触发方法：" + exception.TargetSite);
            return builder.ToString();
        }

        /// <summary>
        ///     日志写入
        /// </summary>
        /// <param name="content">日志内容</param>
        /// <param name="tag">Log级别</param>
        private void Write(string content, string tag)
        {
            _logModelQueue.Enqueue(new LogModel { Msg = content, Tag = tag });
        }

        private void Write(LogModel log)
        {
            _logModelQueue.Enqueue(log);
        }

        private bool Write2File(string log)
        {
            if (string.IsNullOrEmpty(log)) return true;

            try
            {
                if (!Directory.Exists(_savePath))
                {
                    Directory.CreateDirectory(_savePath); //如果文件夹不存在，则创建一个新的
                }
                else
                {
                    //日志文件命名，形如: 20170213_0.log
                    var prefix = DateTime.Now.ToString(TimeStampFormatFilename);
                    //获取文件夹下的所有文件
                    var index = 0;
                    var fileList = Directory.GetFiles(_savePath).ToList();
                    if (fileList.Count > 0)
                    {
                        index = fileList.Count(file => file.Contains(prefix));
                        if (index > 0) index--;
                    }

                    var filename = prefix + "_" + index + ".log";
                    var fileNameNew = Path.Combine(_savePath, filename); //文件不存在则创建
                    if (!File.Exists(fileNameNew))
                    {
                        Write2File(log, fileNameNew, true);
                    }
                    else
                    {
                        //获取文件大小
                        var fileInfo = new FileInfo(fileNameNew);
                        if (fileInfo.Length > _maxFileSize) //如果大于单个文件最大体积
                        {
                            fileNameNew = fileNameNew.Replace("_" + index + ".log", "_" + (index + 1) + ".log");
                            Write2File(log, fileNameNew, !File.Exists(fileNameNew));
                        }
                        else
                        {
                            Write2File(log, fileNameNew, false);
                        }
                    }
                }

                _currentWriteErrorTime = 0;
                return true;
            }
            catch (Exception)
            {
                _currentWriteErrorTime++;
                Thread.Sleep(10 * 1000); //暂停10秒
                if (_currentWriteErrorTime > MaxWriteErrorTime) OnExit(); //停止写入日志，发送报警短信

                return false;
            }
        }

        private static void Write2File(string log, string filePath, bool isCreated)
        {
            var fileStream = isCreated
                ? new FileStream(filePath, FileMode.CreateNew)
                : new FileStream(filePath, FileMode.Append);

            var streamWriter = new StreamWriter(fileStream, Encoding.Default);
            streamWriter.Write(log);
            streamWriter.Flush();
            streamWriter.Close();
            fileStream.Close();
        }
    }

    [Serializable]
    public class LogModel
    {
        public LogModel()
        {
            CreatedTime = DateTime.Now;
        }

        public DateTime CreatedTime { get; set; }
        public string Msg { get; set; }
        public string Operator { get; set; }
        public string Tag { get; set; }
    }

    [Serializable]
    internal class LogBuffer
    {
        public LogBuffer()
        {
            Builder = new StringBuilder();
        }

        public int Count { get; set; }
        public DateTime StartTime { get; set; }
        public StringBuilder Builder { get; set; }
    }
}