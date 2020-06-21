using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Markdig;
using Serilog;

namespace ApiFarm
{
    public class GitlabApiRequest
    {
        private const string ApiRequest = "https://gitlab.com/api/v4/projects/18902673/repository/commits?per_page=1";
        
        public static async Task<string> GetLastCommitFromGitlabProject()
        {
            try
            {
                Log.Debug($"{MarkdownDownloaderSingleton.LogPrefix} Getting Gitlab info from: {ApiRequest}");
                using (var client = new WebClient()) 
                {
                    client.BaseAddress = ApiRequest;
                    client.Headers.Add("Content-Type:application/json");
                    client.Headers.Add("Accept:application/json");
                    var recentCommits = await client.DownloadStringTaskAsync(client.BaseAddress);
                    var serialized = JsonSerializer.Deserialize<List<GitlabApiResponse.CommitData>>(recentCommits);
                    var lastCommitFound = serialized.FirstOrDefault()?.id;
                    Log.Debug($"{MarkdownDownloaderSingleton.LogPrefix} Last commit found? {lastCommitFound != null}");
                    return lastCommitFound;
                }
            }
            catch (Exception e)
            {
                Log.Error($"{MarkdownDownloaderSingleton.LogPrefix} Failed to get Gitlab commit info.");
                return null;
            }
        }
    }
}