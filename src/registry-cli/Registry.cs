using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace registry_cli
{
    class Registry
    {
        private const string ACCEPT_HEADER = "application/vnd.docker.distribution.manifest.v2+json";
        
        private readonly HttpClient httpClient;

        private string hostname;

        private string lastError;

        private string digestMethod = "HEAD";

        public Registry()
        {
            this.httpClient = new HttpClient();
        }

        private (string username, string password) ParseLogin(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                return (null, null);
            }

            if (!login.Contains(":"))
            {
                this.lastError = "Please provide -l in the form USER:PASSWORD";
                return (null, null);
            }

            this.lastError = null;

            string[] parts = login.Split(':');

            parts[0].Trim('"', '\'');
            parts[1].Trim('"', '\'');

            return (parts[0], parts[1]);
        }

        public static Registry Create(string host, string digestMethod = "HEAD")
        {
            Registry r = new Registry();

            r.ThrowIfErrorFound();

            r.hostname = host;
            r.digestMethod = digestMethod;

            return r;
        }

        private void ThrowIfErrorFound()
        {
            if (!string.IsNullOrWhiteSpace(lastError))
            {
                throw new Exception(lastError);
            }
        }

        private async Task<HttpResponseMessage> SendAsync(string path, HttpMethod method = default, string accept = ACCEPT_HEADER)
        {
            method ??= HttpMethod.Get;

            HttpRequestMessage request = new HttpRequestMessage();
            
            request.Method = method;
            request.RequestUri = new Uri(new Uri(this.hostname), path);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(ACCEPT_HEADER));

            HttpResponseMessage response = await this.httpClient.SendAsync(request);

            return response;
        }

        private static async Task<JObject> ReadContent(HttpResponseMessage response)
        {
            string content = await response.Content.ReadAsStringAsync();
            JObject result = JsonConvert.DeserializeObject<JObject>(content);
            return result;
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

        internal async Task<List<string>> ListTagsAsync(string imageName)
        {
            HttpResponseMessage response = await this.SendAsync($"v2/{imageName}/tags/list");

            if (response.IsSuccessStatusCode)
            {
                JObject content = await ReadContent(response);

                return content["tags"].ToObject<List<string>>();
            }

            return new List<string>();
        }

        internal async Task<JToken> GetTagConfigAsync(string imageName, string tag)
        {
            HttpResponseMessage response = await this.SendAsync($"v2/{imageName}/manifests/{tag}");

            if (response.IsSuccessStatusCode)
            {
                JObject content = await ReadContent(response);
                return content["config"];
            }

            return null;
        }

        internal async Task<DateTime?> GetImageTagAgeAsync(string imageName, JToken tagConfig)
        {
            HttpResponseMessage response = await SendAsync($"/v2/{imageName}/blobs/{tagConfig["digest"]}", accept: $"{tagConfig["mediaType"]}");

            if (response.IsSuccessStatusCode)
            {
                JObject content = await ReadContent(response);
                return content["created"].ToObject<DateTime>();
            }

            return null;
        }

        private async Task<string> GetTagDigestAsync(string imageName, string tag)
        {
            HttpResponseMessage response = await this.SendAsync($"v2/{imageName}/manifests/{tag}", method: new HttpMethod(digestMethod));

            if (response.IsSuccessStatusCode)
            {
                return response.Headers.GetValues("Docker-Content-Digest").First();
            }

            return null;
        }

        internal async Task<bool> DeleteTagAsync(string imageName, string tag, bool dryRun, List<string> digestToIgnore)
        {
            digestToIgnore ??= new List<string>();

            if (dryRun)
            {
                Console.WriteLine($"\t\t[DRY RUN] delete tag {tag}");
                return false;
            }

            var tagDigest = await this.GetTagDigestAsync(imageName, tag);

            if (digestToIgnore.Contains(tagDigest))
            {
                Console.WriteLine($"Digest {tagDigest} for tag {tag} is referenced by another tag or has already been deleted and will be ignored");
                return true;
            }

            if (string.IsNullOrWhiteSpace(tagDigest))
            {
                return false;
            }

            HttpResponseMessage response = await SendAsync($"/v2/{imageName}/manifests/{tagDigest}", method: HttpMethod.Delete);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("DONE");
                return true;
            }

            Console.WriteLine($"ERROR: [{response.StatusCode}] (hint: You might want to set REGISTRY_STORAGE_DELETE_ENABLED: \"true\" in your registry)");
            return false;
        }
    }
}
