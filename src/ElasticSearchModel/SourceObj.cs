using System;

namespace ElasticSearchModel
{
    public class SourceObj
    {
        public string Source { get; set; }
        public string[] Tags { get; set; }
        public FieldsObj Fields { get; set; }
        public DateTime TimeStamp { get; set; }
        public string SourceHost { get; set; }
        public string SourcePath { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
    }
}