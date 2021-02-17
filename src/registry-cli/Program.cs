using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace registry_cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<RegistryCliOptions>(args)
                .WithParsedAsync(async options =>
                {
                    Registry registry = Registry.Create(options.Hostname);

                    if (options.Delete)
                    {
                        Console.WriteLine($"Will delete all but {options.KeepLastVersions} last tags");
                    }

                    List<string> imageList;
                    if (!string.IsNullOrWhiteSpace(options.ImageName))
                    {
                        imageList = new List<string> { options.ImageName };
                    }
                    else
                    {
                        imageList = await registry.ListImagesAsync();
                    }

                    imageList = FilterList(options.ImageFilter, imageList);

                    foreach (string imageName in imageList)
                    {
                        Console.WriteLine("----------------------");
                        Console.WriteLine($"Image: {imageName}");

                        List<string> allTagsList = await registry.ListTagsAsync(imageName);

                        if (!allTagsList.Any())
                        {
                            continue;
                        }

                        List<string> tagsList = await GetOrderedTagsAsync(registry, imageName, allTagsList);

                        tagsList = FilterList(options.TagsFilter, tagsList);

                        foreach (var tag in tagsList)
                        {
                            Console.WriteLine($"\ttag: {tag}");
                        }

                        await DeleteTagsAsync(registry, imageName, tagsList, options.KeepLastVersions, options.DryRun);
                    }
                });
        }

        private static List<string> FilterList(IEnumerable<string> filters, List<string> list)
        {
            if (filters?.Any() == true)
            {
                var re = filters.Select(filter => new Regex(filter)).ToList();
                return list.Where(item => re.Any(r => r.IsMatch(item))).ToList();
            }

            return list;
        }

        private static async Task<List<string>> GetOrderedTagsAsync(Registry registry, string imageName, List<string> allTagsList)
        {
            var tagsDate = await GetDateTimeTagsAsync(registry, imageName, allTagsList);

            return tagsDate
                .Where(x => x.DateTime != null)
                .OrderBy(x => x.DateTime.Value)
                .Select(x => x.Tag)
                .ToList();
        }

        private static async Task<List<(string Tag, DateTime? DateTime)>> GetDateTimeTagsAsync(Registry registry, string imageName, List<string> allTagsList)
        {
            List<(string Tag, DateTime? DateTime)> result = new List<(string Tag, DateTime? DateTime)>();

            foreach (var tag in allTagsList)
            {
                await registry.GetTagConfigAsync(imageName, tag);

                var imageConfig = await registry.GetTagConfigAsync(imageName, tag);

                if (imageConfig != null)
                {
                    DateTime? imageAge = await registry.GetImageTagAgeAsync(imageName, imageConfig);
                    result.Add((tag, imageAge));
                }
            }

            return result;
        }

        private static async Task DeleteTagsAsync(Registry registry, string imageName, List<string> orderedTagsList, int keepLastVersions, bool dryRun)
        {
            IEnumerable<string> tagsToDelete = orderedTagsList.Skip(keepLastVersions);

            List<string> digestToIgnore = new List<string>();
            foreach (var tag in tagsToDelete)
            {
                Console.WriteLine($"\tdeleting tag {tag}");
                await registry.DeleteTagAsync(imageName, tag, dryRun, digestToIgnore);
            }
        }
    }
}
