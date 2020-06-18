using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
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
                Log.Debug($"Getting Gitlab info from: {ApiRequest}");
                using (var client = new WebClient()) 
                {
                    client.BaseAddress = ApiRequest;
                    client.Headers.Add("Content-Type:application/json");
                    client.Headers.Add("Accept:application/json");
                    var recentCommits = await client.DownloadStringTaskAsync(client.BaseAddress);
                    var serialized = JsonSerializer.Deserialize<List<GitlabApiResponse.CommitData>>(recentCommits);
                    return serialized.FirstOrDefault().id;
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to get Gitlab commit info.");
                return "";
            }
        }
    }
}