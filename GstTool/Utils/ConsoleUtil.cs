using System;
using System.Collections.Generic;
using System.Dynamic;
using static System.ConsoleColor;

namespace GstTool.Utils
{
    public static class ConsoleUtil
    {
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
            Log.D(s);
        }

        public static bool HasProperty(this ExpandoObject obj, string propertyName)
        {
            return ((IDictionary<string, object>)obj).ContainsKey(propertyName);
        }
    }
}