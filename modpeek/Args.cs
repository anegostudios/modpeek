using CommandLine;

namespace VintageStory.ModPeek
{
    public class Args
    {
        [Option('h', "help", HelpText = "Print help info and exit")]
        public bool PrintHelp { get; set; }

        [Option('d', "id", HelpText = "Print modid and exit")]
        public bool Id { get; set; }

    }


    
}
