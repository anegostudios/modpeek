using CommandLine;

namespace VintageStory.ModPeek
{
    public class Args
    {
        [Option('h', "help", HelpText = "Print help info and exit")]
        public bool PrintHelp { get; set; }

        [Option('i', "idandversion", HelpText = "Print modid:version and exit")]
        public bool IdAndVersion { get; set; }

        [Option('f', "file", HelpText = "Mod File")]
        public string File { get; set; }

    }


    
}
