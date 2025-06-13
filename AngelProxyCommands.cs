namespace AngelSQL
{
    public class ProxyCommands
    {
        public static Dictionary<string, string> DbCommands()
        {
            Dictionary<string, string> commands = new Dictionary<string, string>
            {
                { @"APP DIRECTORY", @"APP DIRECTORY#free" },
                { @"CREATE HOST", @"CREATE HOST#free;PASSWORD#free;ACCOUNT#freeoptional;BRANCH#freeoptional;HOST#number" },
                { @"MEM", @"MEM#free" },
        };

            return commands;

        }
    }
}