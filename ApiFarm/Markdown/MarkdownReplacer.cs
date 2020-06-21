using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Markdig;
using Serilog;

namespace ApiFarm
{
    public class MarkdownDownloaderSingleton
    {
        //url for gitlab proj file
        private const string GithubUrlZip = @"https://gitlab.com/api-farm/docs/-/archive/master/docs-master.zip";
        
        //folder for saving docs cache
        private const string FileSavePath = @"api-farm-docs";

        public const string LogPrefix = "[MarkdownFromGitlab]"; 

        private const int UpdateCheckIntervalMinutes = 1;

        private static readonly Task<MarkdownDownloaderSingleton> _getInstanceTask = CreateSingleton();
        public static Task<MarkdownDownloaderSingleton> Instance
        {
            get { return _getInstanceTask; }
        }

        private MarkdownDownloaderSingleton(MarkdownData markdownData)
        {
            MarkdownData = markdownData;
        }

        public MarkdownData MarkdownData { get; private set; } = null;

        private static async Task<MarkdownDownloaderSingleton> CreateSingleton()
        {
            MarkdownData markdownData = await LoadData();
            return new MarkdownDownloaderSingleton(markdownData);
        }

        public async Task GetActualData()
        {
            var shouldBeUpdatedDataIsNull = MarkdownData == null;
            var shouldBeUpdatedContent = MarkdownData?.Content?.Any() == null;
            var shouldBeUpdatedTime = MarkdownData != null && Math.Abs((DateTime.Now - MarkdownData.LastUpdate).TotalMinutes) > UpdateCheckIntervalMinutes;
          
            Log.Warning($"{LogPrefix} Data should be updated: D:{shouldBeUpdatedDataIsNull}  C:{shouldBeUpdatedContent}  T:{shouldBeUpdatedTime}");
            if (shouldBeUpdatedDataIsNull || shouldBeUpdatedTime || shouldBeUpdatedContent)
            {
                Log.Debug($"{LogPrefix} Updating");
                MarkdownData = await LoadData();
            }
        }

        /// <summary>
        /// Downloading files from github, converting to html.
        /// </summary>
        /// <returns></returns>
        private static async Task<MarkdownData> LoadData()
        {
            Log.Debug($"{LogPrefix} Start!");

            var lastCommit = await GitlabApiRequest.GetLastCommitFromGitlabProject();
            var dataDirectory = "";
            
            if (!string.IsNullOrEmpty(lastCommit))
            {
                var directory = Path.Combine(FileSavePath);
                Log.Debug($"{LogPrefix} Searching for last commit(\"{lastCommit}\") data in folder \"{directory}\"");
                var lastCommitDataFound = Directory.GetDirectories(directory).FirstOrDefault(v => v.Contains(lastCommit));
                
                if (!string.IsNullOrEmpty(lastCommitDataFound))
                {
                    Log.Debug($"{LogPrefix} Commit data in folder found: \"{lastCommitDataFound}\"");
                    dataDirectory = lastCommitDataFound;
                }
                else
                {
                    Log.Debug($"{LogPrefix} Commit data in folder not found");
                }
            }

            var dataAlreadyDownloaded = !string.IsNullOrEmpty(dataDirectory);
            Log.Debug($"{LogPrefix} Last commit data already downloaded? {dataAlreadyDownloaded}");
            if (!dataAlreadyDownloaded)
            {
                lastCommit = string.IsNullOrEmpty(lastCommit) ? new Guid().ToString() : lastCommit;
                var downloadResult = await DownloadDataFromGitlab(lastCommit);
                if (!string.IsNullOrEmpty(downloadResult))
                {
                    Log.Debug($"{LogPrefix} Downloaded data from gitlab: \"{downloadResult}\"");
                    dataDirectory = downloadResult;
                }
            }

            if (string.IsNullOrEmpty(dataDirectory))
            {
                Log.Error($"{LogPrefix} Something went wrong, data dir not found: \"{dataDirectory}\"");
                return new MarkdownData();
            }

            var getMdFiles = GetMarkdownFiles(dataDirectory);
            getMdFiles.LastUpdate = DateTime.Now;
            getMdFiles.CommitId = lastCommit;
            Log.Debug($"{LogPrefix} Complete!");

            return getMdFiles;
        }

        private static async Task<string> DownloadDataFromGitlab(string lastCommit)
        {
            Log.Debug($"{LogPrefix} Downloading commit data");
            var downloadResult = await DownloadZip(lastCommit);
            if (!downloadResult.DownloadedSuccessfully)
            {
                Log.Error($"{LogPrefix} Failed to download file.");
                return null;
            }

            var unpackResult = UnpackZip(downloadResult.FileFullName);
            if (!unpackResult.UnpackedSuccessfully)
            {
                Log.Error($"{LogPrefix} Failed to unpack file.");
                return null;
            }
            return unpackResult.UnpackedDirectory;
        }
        
        private static readonly Regex RegexPatternEndpoint = new Regex(@"\\(POST|GET|DELETE|PUT)\\");
        private static readonly Regex RegexPatternFunction = new Regex(@"morpher.ru\\ws\\3");
        private static readonly Regex RegexPatternJustInfoPage = new Regex(@"");

        private static MarkdownData GetMarkdownFiles(string directoryName)
        {
            Log.Debug($"{LogPrefix} Getting markdown files: {directoryName}");

            var files = GetFilesInDir(directoryName, "*.md");
            
            var result = new MarkdownData();

            result.Content = new List<MarkdownContent>();

            foreach (string file in files)
            {
                var htmlString = GetHtmlFromMarkdown(file);
                if (string.IsNullOrEmpty(htmlString)) continue;
                var data = new MarkdownContent()
                {
                    FilePath = file,
                    Html = htmlString,
                };

                if (RegexPatternEndpoint.Match(file).Success)
                {
                    data.IsEndpoint = true;
                }
                else if (RegexPatternFunction.Match(file).Success)
                {
                    data.IsFunction = true;
                }
                else if (RegexPatternJustInfoPage.Match(file).Success)
                {
                    data.IsJustInfoPage = true;
                }
                else
                {
                    data.IsJustInfoPage = true;
                }
                
                
                
                result.Content.Add(data);
            }

            if (!result.Content.Any())
            {
                Log.Error($"{LogPrefix} Failed to find markdown files: {directoryName}");
            }

            return result;
        }

        private static string GetHtmlFromMarkdown(string pathToFile)
        {
            try
            {
                var fileInfo = new FileInfo(pathToFile);
                string fileText = File.ReadAllText(fileInfo.FullName);
                return Markdown.ToHtml(fileText);
            }
            catch (Exception e)
            {
                Log.Error($"{LogPrefix} Failed to get HTML from: {pathToFile}");
                Log.Error($"{LogPrefix} {e}");
                return null;
            }
        }

        private static IEnumerable<string> GetFilesInDir(string directoryName, string fileExtension)
        {
            try
            {
                return Directory.GetFiles(directoryName, fileExtension, SearchOption.AllDirectories);
            }
            catch (Exception e)
            {
                Log.Error($"{LogPrefix} Failed to get files in directory: {directoryName}");
                Log.Error($"{LogPrefix} {e}");
                return new List<string>();
            }
        }

        private static (bool UnpackedSuccessfully, string UnpackedDirectory) UnpackZip(string fileFullName)
        {
            try
            {
                if (!File.Exists(fileFullName))
                {
                    Log.Error($"{LogPrefix} File doesn't exist: {fileFullName}");
                    return (false, null);
                }

                var fileInfo = new FileInfo(fileFullName);

                ZipFile.ExtractToDirectory(fileFullName, fileInfo.DirectoryName);
                return (true, fileInfo.DirectoryName);
            }
            catch (Exception e)
            {
                Log.Error($"{LogPrefix} Failed to unpack zip: {fileFullName}");
                Log.Error($"{LogPrefix} {e}");
                return (false, null);
            }
        }

        private static async Task<(bool DownloadedSuccessfully, string FileFullName)> DownloadZip(string lastCommit)
        {
            try
            {
                Log.Debug($"{LogPrefix} Downloading file from: \"{GithubUrlZip}\"");
                using var webClient = new WebClient();
                var directory = Path.Combine(FileSavePath, DateTime.Now.ToString("yyyy.MM.dd HH-mm"));

                var directoryWithGuid = Path.Combine(directory + " " + lastCommit);
                if (!Directory.Exists(directoryWithGuid))
                {
                    Directory.CreateDirectory(directoryWithGuid);
                }

                var fileFullName = Path.Combine(directoryWithGuid, "data.zip");

                await webClient.DownloadFileTaskAsync(GithubUrlZip, fileFullName);
                return (true, fileFullName);
            }
            catch (Exception e)
            {
                Log.Error($"{LogPrefix} Failed to download file: {GithubUrlZip}");
                Log.Error($"{LogPrefix} {e}");
                return (true, null);
            }
        }
    }
}
