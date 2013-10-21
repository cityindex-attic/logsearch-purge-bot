namespace ElasticSearchModel
{
    public class LogRecord
    {
        public string Index { get; set; }
        public string Type { get; set; }
        public string Id { get; set; }
        public decimal Score { get; set; }
        public SourceObj Source { get; set; }
    }
}