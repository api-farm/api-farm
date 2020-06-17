using System;

namespace ApiFarm
{
    //classes for json deserialization from gitlab api
    public class ClassForGitlabApiResponse
    {
        //unused
        public class GitlabApiData
        {
            public CommitData[] GitlabCommits { get; set; }
        }

        public class CommitData
        {
            public string id { get; set; }
            public string short_id { get; set; }
            public DateTime created_at { get; set; }
            public string[] parent_ids { get; set; }
            public string title { get; set; }
            public string message { get; set; }
            public string author_name { get; set; }
            public string author_email { get; set; }
            public DateTime authored_date { get; set; }
            public string committer_name { get; set; }
            public string committer_email { get; set; }
            public DateTime committed_date { get; set; }
            public string web_url { get; set; }
        }
    }
}