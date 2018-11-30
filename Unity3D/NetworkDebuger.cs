using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Network_Kcp
{
    public class NetworkDebuger
    {
        public static bool EnableLog;
        public static bool EnableTime = false;
        public static bool EnableSave = false;
        public static bool EnableStack = false;
        public static string LogFileDir = Application.persistentDataPath + "/NetworkLog/";
        public static string LogFileName = "";
        public static string Prefix = "> ";
        public static StreamWriter LogFileWriter = null;


        public static void Log(object message) {
            if (!NetworkDebuger.EnableLog) {
                return;
            }

            string msg = GetLogTime() + message;

            //Debug.Log(Prefix + msg, null);
            LogToFile("[I]" + msg);
        }

        //public static void Log(object message, object context) {
        //    if (!NetworkDebuger.EnableLog) {
        //        return;
        //    }

        //    string msg = GetLogTime() + message;

        //    //Debug.Log(Prefix + msg, context);
        //    LogToFile("[I]" + msg);
        //}

        public static void LogError(object message) {
            string msg = GetLogTime() + message;

            Debug.LogError(Prefix + msg, null);
            LogToFile("[E]" + msg, true);
        }

        //public static void LogError(object message, object context) {
        //    string msg = GetLogTime() + message;

        //    Debug.LogError(Prefix + msg, context);
        //    LogToFile("[E]" + msg, true);
        //}

        public static void LogWarning(object message) {
            string msg = GetLogTime() + message;

            Debug.LogWarning(Prefix + msg, null);
            LogToFile("[W]" + msg);
        }

        //public static void LogWarning(object message, Object context) {
        //    string msg = GetLogTime() + message;

        //    Debug.LogWarning(Prefix + msg, context);
        //    LogToFile("[W]" + msg);
        //}


        //----------------------------------------------------------------------

        public static void Log(string tag, string message) {
            if (!NetworkDebuger.EnableLog) {
                return;
            }

            message = GetLogText(tag, message);
            //Debug.Log(Prefix + message);
            LogToFile("[I]" + message);
        }

        public static void Log(string tag, string format, params object[] args) {
            if (!NetworkDebuger.EnableLog) {
                return;
            }

            string message = GetLogText(tag, string.Format(format, args));
            //Debug.Log(Prefix + message);
            LogToFile("[I]" + message);
        }

        public static void LogError(string tag, string message) {
            message = GetLogText(tag, message);
            Debug.LogError(Prefix + message);
            LogToFile("[E]" + message, true);
        }

        public static void LogError(string tag, string format, params object[] args) {
            string message = GetLogText(tag, string.Format(format, args));
            Debug.LogError(Prefix + message);
            LogToFile("[E]" + message, true);
        }


        public static void LogWarning(string tag, string message) {
            message = GetLogText(tag, message);
            Debug.LogWarning(Prefix + message);
            LogToFile("[W]" + message);
        }

        public static void LogWarning(string tag, string format, params object[] args) {
            string message = GetLogText(tag, string.Format(format, args));
            Debug.LogWarning(Prefix + message);
            LogToFile("[W]" + message);
        }


        //----------------------------------------------------------------------
        private static string GetLogText(string tag, string message) {
            string str = "";
            if (NetworkDebuger.EnableTime) {
                DateTime now = DateTime.Now;
                str = now.ToString("HH:mm:ss.fff") + " ";
            }

            str = str + tag + "::" + message;
            return str;
        }

        private static string GetLogTime() {
            string str = "";
            if (NetworkDebuger.EnableTime) {
                DateTime now = DateTime.Now;
                str = now.ToString("HH:mm:ss.fff") + " ";
            }
            return str;
        }



        private static void LogToFile(string message, bool EnableStack = false) {
            if (!EnableSave) {
                if (LogFileWriter != null) {
                    LogFileWriter.Flush();
                    LogFileWriter.Close();
                    LogFileWriter = null;
                }
                return;
            }

            if (LogFileWriter == null) {
                DateTime now = DateTime.Now;
                LogFileName = now.GetDateTimeFormats('s')[0].ToString();//2005-11-05T14:06:25
                LogFileName = LogFileName.Replace("-", "_");
                LogFileName = LogFileName.Replace(":", "_");
                LogFileName = LogFileName.Replace(" ", "");
                LogFileName += ".log";

                string fullpath = LogFileDir + LogFileName;
                try {
                    if (!Directory.Exists(LogFileDir)) {
                        Directory.CreateDirectory(LogFileDir);
                    }

                    LogFileWriter = File.AppendText(fullpath);
                    LogFileWriter.AutoFlush = true;
                }
                catch (Exception e) {
                    LogFileWriter = null;
                    Debug.LogError("LogToCache() " + e.Message + e.StackTrace);
                    return;
                }
            }

            if (LogFileWriter != null) {
                try {
                    LogFileWriter.WriteLine(message);
                    if (EnableStack || NetworkDebuger.EnableStack) {
                        LogFileWriter.WriteLine(StackTraceUtility.ExtractStackTrace());
                    }
                }
                catch (Exception) {
                    return;
                }
            }
        }
    }
}
