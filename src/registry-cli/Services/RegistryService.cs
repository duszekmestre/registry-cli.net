using Microsoft.Extensions.Logging;
using registry_cli.Extensions;
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
        private readonly ILogger<RegistryService> logger;

        public RegistryService(
            IRegistryApiClient registry,
            ILogger<RegistryService> logger
            )
        {
            this.registry = registry;
            this.logger = logger;
        }

        public async Task RunCliAsync(RegistryCliOptions options)
        {
            if (options.Delete)
            {
                logger.LogInformation($"Will delete all but {options.KeepLastVersions} last tags");
            }

            IEnumerable<string> imageList;
            if (!string.IsNullOrWhiteSpace(options.ImageName))
            {
                imageList = new List<string> { options.ImageName };
            }
            else
            {
                imageList = await registry.ListImagesAsync();
            }

            imageList = imageList.FilterCollection(options.ImageFilter);

            foreach (string imageName in imageList)
            {
                using IDisposable scope = logger.BeginScope($"Image: {imageName}");

                List<string> allTagsList = await registry.ListTagsAsync(imageName);

                if (!allTagsList.Any())
                {
                    logger.LogTrace($"No tags for image found");
                    continue;
                }

                logger.LogDebug("Found {allTagsCount} tags for image {imageName}", allTagsList.Count, imageName);
                IEnumerable<string> filteredTagsList = allTagsList.FilterCollection(options.TagsFilter);
                
                logger.LogDebug("Filtered tags count: {filteredCount}", filteredTagsList.Count());

                IEnumerable<string> orderedTagsList = await GetOrderedTagsAsync(imageName, filteredTagsList);

                await DeleteTagsAsync(imageName, orderedTagsList, options.KeepLastVersions, options.DryRun);
            }
        }

        private async Task<IEnumerable<string>> GetOrderedTagsAsync(string imageName, IEnumerable<string> allTagsList)
        {
            List<(string Tag, DateTime? DateTime)> tagsDate = await GetDateTimeTagsAsync(imageName, allTagsList);

            return tagsDate
                .Where(x => x.DateTime != null)
                .OrderByDescending(x => x.DateTime.Value)
                .Select(x => x.Tag);
        }

        private async Task<List<(string Tag, DateTime? DateTime)>> GetDateTimeTagsAsync(string imageName, IEnumerable<string> allTagsList)
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

        private async Task DeleteTagsAsync(string imageName, IEnumerable<string> orderedTagsList, int keepLastVersions, bool dryRun)
        {
            IEnumerable<string> tagsToDelete = orderedTagsList.Skip(keepLastVersions);

            logger.LogInformation("Found {tagstoDeleteCount} tags to delete", tagsToDelete.Count());

            List<string> digestToIgnore = new List<string>();
            foreach (string tag in tagsToDelete)
            {
                using var scope = logger.BeginScope($"deleting tag {tag}");

                await DeleteTagAsync(imageName, tag, dryRun, digestToIgnore);
            }
        }

        private async Task<bool> DeleteTagAsync(string imageName, string tag, bool dryRun, List<string> digestToIgnore)
        {
            digestToIgnore ??= new List<string>();

            if (dryRun)
            {
                logger.LogInformation($"[DRY RUN] delete tag {tag}");
                return false;
            }

            string tagDigest = await this.registry.GetTagDigestAsync(imageName, tag);

            if (digestToIgnore.Contains(tagDigest))
            {
                logger.LogWarning($"Digest {tagDigest} for tag {tag} is referenced by another tag or has already been deleted and will be ignored");
                return true;
            }

            if (string.IsNullOrWhiteSpace(tagDigest))
            {
                return false;
            }

            bool deleted = await registry.DeleteTagAsync(imageName, tagDigest);

            if (deleted)
            {
                logger.LogInformation("Succesfuly deleted tag: {imageName}:{tag}", imageName, tag);
            }
            else
            {
                logger.LogError($"ERROR: (hint: You might want to set REGISTRY_STORAGE_DELETE_ENABLED: \"true\" in your registry)");
            }

            return deleted;
        }
    }
}
