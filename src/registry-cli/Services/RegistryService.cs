using registry_cli.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace registry_cli.Services
{
    class RegistryService : IRegistryService
    {
        private readonly IRegistryApiClient registry;

        public RegistryService(
            IRegistryApiClient registry
            )
        {
            this.registry = registry;
        }

        public async Task RunCliAsync(RegistryCliOptions options)
        {
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

                List<string> tagsList = await GetOrderedTagsAsync(imageName, allTagsList);

                tagsList = FilterList(options.TagsFilter, tagsList);

                foreach (string tag in tagsList)
                {
                    Console.WriteLine($"\ttag: {tag}");
                }

                await DeleteTagsAsync(imageName, tagsList, options.KeepLastVersions, options.DryRun);
            }
        }

        private List<string> FilterList(IEnumerable<string> filters, List<string> list)
        {
            if (filters?.Any() == true)
            {
                List<Regex> re = filters.Select(filter => new Regex(filter)).ToList();
                return list.Where(item => re.Any(r => r.IsMatch(item))).ToList();
            }

            return list;
        }

        private async Task<List<string>> GetOrderedTagsAsync(string imageName, List<string> allTagsList)
        {
            List<(string Tag, DateTime? DateTime)> tagsDate = await GetDateTimeTagsAsync(imageName, allTagsList);

            return tagsDate
                .Where(x => x.DateTime != null)
                .OrderBy(x => x.DateTime.Value)
                .Select(x => x.Tag)
                .ToList();
        }

        private async Task<List<(string Tag, DateTime? DateTime)>> GetDateTimeTagsAsync(string imageName, List<string> allTagsList)
        {
            List<(string Tag, DateTime? DateTime)> result = new List<(string Tag, DateTime? DateTime)>();

            foreach (string tag in allTagsList)
            {
                await registry.GetTagConfigAsync(imageName, tag);

                Newtonsoft.Json.Linq.JToken imageConfig = await registry.GetTagConfigAsync(imageName, tag);

                if (imageConfig != null)
                {
                    DateTime? imageAge = await registry.GetImageTagAgeAsync(imageName, imageConfig);
                    result.Add((tag, imageAge));
                }
            }

            return result;
        }

        private async Task DeleteTagsAsync(string imageName, List<string> orderedTagsList, int keepLastVersions, bool dryRun)
        {
            IEnumerable<string> tagsToDelete = orderedTagsList.Skip(keepLastVersions);

            List<string> digestToIgnore = new List<string>();
            foreach (string tag in tagsToDelete)
            {
                Console.WriteLine($"\tdeleting tag {tag}");
                await DeleteTagAsync(imageName, tag, dryRun, digestToIgnore);
            }
        }

        private async Task<bool> DeleteTagAsync(string imageName, string tag, bool dryRun, List<string> digestToIgnore)
        {
            digestToIgnore ??= new List<string>();

            if (dryRun)
            {
                Console.WriteLine($"\t\t[DRY RUN] delete tag {tag}");
                return false;
            }

            string tagDigest = await this.registry.GetTagDigestAsync(imageName, tag);

            if (digestToIgnore.Contains(tagDigest))
            {
                Console.WriteLine($"Digest {tagDigest} for tag {tag} is referenced by another tag or has already been deleted and will be ignored");
                return true;
            }

            if (string.IsNullOrWhiteSpace(tagDigest))
            {
                return false;
            }

            bool deleted = await registry.DeleteTagAsync(imageName, tagDigest);

            return deleted;
        }
    }
}
