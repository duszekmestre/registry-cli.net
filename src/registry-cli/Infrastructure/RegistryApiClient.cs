using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace registry_cli.Infrastructure
{
    class RegistryApiClient : IRegistryApiClient
    {
        private const string DIGEST_METHOD = "HEAD";
        private const string ACCEPT_HEADER = "application/vnd.docker.distribution.manifest.v2+json";

        private readonly HttpClient httpClient;

        public RegistryApiClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<bool> DeleteTagAsync(string imageName, string tagDigest)
        {
            HttpResponseMessage response = await SendAsync($"/v2/{imageName}/manifests/{tagDigest}", method: HttpMethod.Delete);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("DONE");
                return true;
            }

            Console.WriteLine($"ERROR: [{response.StatusCode}] (hint: You might want to set REGISTRY_STORAGE_DELETE_ENABLED: \"true\" in your registry)");
            return false;
        }

        public async Task<List<string>> ListImagesAsync()
        {
            HttpResponseMessage response = await SendAsync("/v2/_catalog?n=10000");

            if (response.IsSuccessStatusCode)
            {
                JObject content = await ReadContent(response);

                return content["repositories"].ToObject<List<string>>();
            }

            return new List<string>();
        }

        public async Task<List<string>> ListTagsAsync(string imageName)
        {
            HttpResponseMessage response = await this.SendAsync($"v2/{imageName}/tags/list");

            if (response.IsSuccessStatusCode)
            {
                JObject content = await ReadContent(response);

                return content["tags"].ToObject<List<string>>();
            }

            return new List<string>();
        }

        public async Task<DateTime?> GetImageTagAgeAsync(string imageName, JToken tagConfig)
        {
            HttpResponseMessage response = await SendAsync($"/v2/{imageName}/blobs/{tagConfig["digest"]}", accept: $"{tagConfig["mediaType"]}");

            if (response.IsSuccessStatusCode)
            {
                JObject content = await ReadContent(response);
                return content["created"].ToObject<DateTime>();
            }

            return null;
        }

        public async Task<JToken> GetTagConfigAsync(string imageName, string tag)
        {
            HttpResponseMessage response = await this.SendAsync($"v2/{imageName}/manifests/{tag}");

            if (response.IsSuccessStatusCode)
            {
                JObject content = await ReadContent(response);
                return content["config"];
            }

            return null;
        }

        public async Task<string> GetTagDigestAsync(string imageName, string tag)
        {
            HttpResponseMessage response = await this.SendAsync($"v2/{imageName}/manifests/{tag}", method: new HttpMethod(DIGEST_METHOD));

            if (response.IsSuccessStatusCode)
            {
                return response.Headers.GetValues("Docker-Content-Digest").First();
            }

            return null;
        }

        private async Task<HttpResponseMessage> SendAsync(string path, HttpMethod method = default, string accept = ACCEPT_HEADER)
        {
            method ??= HttpMethod.Get;

            HttpRequestMessage request = new HttpRequestMessage(method, path);

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(accept));

            HttpResponseMessage response = await this.httpClient.SendAsync(request);

            return response;
        }

        private static async Task<JObject> ReadContent(HttpResponseMessage response)
        {
            string content = await response.Content.ReadAsStringAsync();
            JObject result = JsonConvert.DeserializeObject<JObject>(content);
            return result;
        }
    }
}
