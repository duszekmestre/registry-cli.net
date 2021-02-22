using CommandLine;
using System.Collections.Generic;

namespace registry_cli
{
    internal class RegistryCliOptions
    {
        [Option('r', "registry", Required = true, HelpText = "Set registry hostname")]
        public string Hostname { get; set; }

        [Option("num", Default = 10, HelpText = "Keep last image versions")]
        public int KeepLastVersions { get; set; }

        [Option("delete", Default = false, HelpText = "Whether to delete images")]
        public bool Delete { get; set; }

        [Option('i', "image", HelpText = "Image name")]
        public string ImageName { get; set; }

        [Option("images-like", HelpText = "Images filter")]
        public IEnumerable<string> ImageFilter { get; set; }

        [Option("tags-like", HelpText = "Tags filter")]
        public IEnumerable<string> TagsFilter { get; set; }
        
        [Option("dry-run", HelpText = "Tags filter")]
        public bool DryRun { get; set; }
    }
}