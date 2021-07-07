using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using static System.ConsoleColor;

namespace GstTool
{
    public static class Utils
    {
        private const string TimeStampFormatFilenameRecord = "MMdd-HHmmss";
        private const string TimeStampFormatFilenameShot = "MMdd-HHmmss-fff";

        public static string GetRecordFilename(string filepath = "")
        {
            return filepath + DateTime.Now.ToString(TimeStampFormatFilenameRecord) + ".mp4";
        }

        public static string GetShotFilename(string filepath = "", string suffix = "png")
        {
            return filepath + DateTime.Now.ToString(TimeStampFormatFilenameShot) + "." + suffix;
        }

        public static int PrintErr(this string format, params object[] args)
        {
            var s = string.Format(format, args);
            PrintColor(s, Red);
            return -1;
        }

        public static void PrintYellow(this string s)
        {
            PrintColor(s, Yellow);
        }

        public static void PrintGreen(this string s)
        {
            PrintColor(s, Green);
        }

        public static void PrintMagenta(this string s)
        {
            PrintColor(s, Magenta);
        }

        private static void PrintColor(this string s, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(s);
            Console.ResetColor();
            Debug.WriteLine(s);
        }

        public static bool HasProperty(this ExpandoObject obj, string propertyName)
        {
            return ((IDictionary<string, object>) obj).ContainsKey(propertyName);
        }
    }
}