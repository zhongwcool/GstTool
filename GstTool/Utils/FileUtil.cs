using System;

namespace GstTool.Utils
{
    public static class FileUtil
    {
        private const string TimeStampFormatFilenameRecord = "MMdd-HHmmss-fff";
        private const string TimeStampFormatFilenameShot = "MMdd-HHmmss-fff";

        public static string GetRecordFilename(string filepath = "")
        {
            return filepath + DateTime.Now.ToString(TimeStampFormatFilenameRecord) + ".mp4";
        }

        public static string GetShotFilename(string filepath = "", string suffix = "png")
        {
            return filepath + DateTime.Now.ToString(TimeStampFormatFilenameShot) + "." + suffix;
        }
    }
}