using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Markdig;
using Serilog;

namespace ApiFarm
{
    public class MarkdownDownloaderSingleton
    {
        private static readonly Task<MarkdownDownloaderSingleton> _getInstanceTask = CreateSingleton();
        
        //url for gitlab proj file
        private const string GithubUrlZip = @"https://gitlab.com/api-farm/docs/-/archive/master/docs-master.zip";
        
        //folder for saving docs cache
        private const string FileSavePath = @"api-farm-docs";

        public static Task<MarkdownDownloaderSingleton> Instance
        {
            get { return _getInstanceTask; }
        }

        private MarkdownDownloaderSingleton(List<MarkdownData> markdownData)
        {
            MarkdownData = markdownData;
        }

        public List<MarkdownData> MarkdownData { get; private set; }

        private static async Task<MarkdownDownloaderSingleton> CreateSingleton()
        {
            List<MarkdownData> markdownData = await LoadData();
            return new MarkdownDownloaderSingleton(markdownData);
        }

        public async Task ReloadData()
        {
            this.MarkdownData = await LoadData();
        }

        /// <summary>
        /// Downloading files from github, converting to html.
        /// </summary>
        /// <returns></returns>
        private static async Task<List<MarkdownData>> LoadData()
        {
            Log.Debug("Start!");
            var result = new List<MarkdownData>();

            var lastCommit = await GitlabApiRequest.GetLastCommitFromGitlabProject();
            if (string.IsNullOrEmpty(lastCommit))
            {
                
            }

            var downloadResult = await DownloadZip();
            if (!downloadResult.DownloadedSuccessfully)
            {
                Log.Error($"Failed to download file.");
                return result;
            }

            var unpackResult = UnpackZip(downloadResult.FileFullName);
            if (!unpackResult.UnpackedSuccessfully)
            {
                Log.Error($"Failed to unpack file.");
                return result;
            }

            var getMdFiles = GetMarkdownFiles(unpackResult.UnpackedDirectory);

            // foreach (var VARIABLE in getMdFiles)
            // {
            //     Log.Warning(VARIABLE.filePath);
            // }

            Log.Debug("Complete!");

            return getMdFiles;
        }

        private static List<MarkdownData> GetMarkdownFiles(string directoryName)
        {
            Log.Debug($"Getting markdown files: {directoryName}");

            var files = GetFilesInDir(directoryName, "*.md");

            var markdownData = new List<MarkdownData>();

            foreach (string file in files)
            {
                var htmlString = GetHtmlFromMarkdown(file);
                if (string.IsNullOrEmpty(htmlString)) continue;
                var data = new MarkdownData
                {
                    filePath = file,
                    html = htmlString,
                };
                markdownData.Add(data);
            }

            if (!markdownData.Any())
            {
                Log.Error($"Failed to find markdown files: {directoryName}");
            }

            return markdownData;
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
                Log.Error($"Failed to get HTML from: {pathToFile}");
                Log.Error($"{e}");
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
                Log.Error($"Failed to get files in directory: {directoryName}");
                Log.Error($"{e}");
                return new List<string>();
            }
        }

        private static (bool UnpackedSuccessfully, string UnpackedDirectory) UnpackZip(string fileFullName)
        {
            try
            {
                if (!File.Exists(fileFullName))
                {
                    Log.Error($"File doesn't exist: {fileFullName}");
                    return (false, null);
                }

                var fileInfo = new FileInfo(fileFullName);

                ZipFile.ExtractToDirectory(fileFullName, fileInfo.DirectoryName);
                return (true, fileInfo.DirectoryName);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to unpack zip: {fileFullName}");
                Log.Error($"{e}");
                return (false, null);
            }
        }

        private static async Task<(bool DownloadedSuccessfully, string FileFullName)> DownloadZip()
        {
            try
            {
                Log.Debug($"Downloading file from: \"{GithubUrlZip}\"");
                using var webClient = new WebClient();
                var directory = Path.Combine(FileSavePath, DateTime.Now.ToString("yyyy.MM.dd HH-mm"));

                //todo do not download files if data with last commit already downloaded
                var uniqString = await GitlabApiRequest.GetLastCommitFromGitlabProject();
                if (string.IsNullOrEmpty(uniqString))
                {
                    uniqString = Guid.NewGuid().ToString();
                }

                var directoryWithGuid = Path.Combine(directory + " " + uniqString);
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
                Log.Error($"Failed to download file: {GithubUrlZip}");
                Log.Error($"{e}");
                return (true, null);
            }
        }
    }
}
