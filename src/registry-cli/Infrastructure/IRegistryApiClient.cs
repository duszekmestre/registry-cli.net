using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace registry_cli.Infrastructure
{
    interface IRegistryApiClient
    {
        Task<bool> DeleteTagAsync(string imageName, string tagDigest);
        Task<DateTime?> GetImageTagAgeAsync(string imageName, JToken tagConfig);
        Task<JToken> GetTagConfigAsync(string imageName, string tag);
        Task<string> GetTagDigestAsync(string imageName, string tag);
        Task<List<string>> ListImagesAsync();
        Task<List<string>> ListTagsAsync(string imageName);
    }
}