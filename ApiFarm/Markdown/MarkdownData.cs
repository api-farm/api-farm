using System;
using System.Collections.Generic;

namespace ApiFarm
{
    public class MarkdownData
    {
        public string CommitId { get; set; }
        public DateTime LastUpdate { get; set; }
        public List<MarkdownContent> Content { get; set; }
    }
    
    public class MarkdownContent
    {
        public string Html { get; set; }
        public string FilePath { get; set; }
        
        public bool IsEndpoint { get; set; }
        public bool IsFunction { get; set; }
        public bool IsJustInfoPage { get; set; }
    }
}