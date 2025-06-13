namespace AngelProxy
{

    public static class LogFile
    {
        public static void Log(string result)
        {

            if (!IsWindowsService())
            {
                Console.WriteLine(result);
            }

            if (!Directory.Exists(Environment.CurrentDirectory + "/logs"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "/logs");
            }

            string log_file = Environment.CurrentDirectory + "/logs/Log-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";
            string last_error = Environment.CurrentDirectory + "/logs/Last_error-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";

            try
            {
                File.AppendAllText(log_file, DateTime.Now.ToString("HH:mm:ss") + "-->: " + result + "\n");
                File.WriteAllText(last_error, DateTime.Now.ToString("HH:mm:ss") + "-->: " + result + "\n");
            }
            catch (Exception)
            {
            }
        }

        private static bool IsWindowsService()
        {
            return !(Environment.UserInteractive);
        }
    }
}
