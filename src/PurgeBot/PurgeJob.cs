using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;

namespace PurgeBot
{
    public class PurgeJob
    {
        private static ILog _logger = LogManager.GetLogger(typeof(PurgeJob));

        private DateTime _toDate;
        private Uri _uri;

        public PurgeJob(DateTime toDate, Uri uri)
        {

            _toDate = toDate;
            _uri = uri;


        }

        public void Execute()
        {

            _logger.InfoFormat("Starting purge at {0}.", DateTime.Now.ToShortTimeString());
            _logger.Info("Fetching index mapping.");

            Mapping mapping = GetMappings(_uri);
            List<string> indices = mapping.Indices.Where(i => i.StartsWith("logstash-")).ToList();
            indices.Sort();


            var client = new ElasticClient(new ConnectionSettings(_uri));

            ConnectionStatus connectionStatus;

            if (client.TryConnect(out connectionStatus))
            {
                // _all index is not viable (?? why does it exist?) so we have to iterate alias. seems clumsy to me.

                foreach (string index in indices)
                {
                    string index1 = index;
                    // may also want to discriminate by 'type' which are available in mapping

                    _logger.InfoFormat("Checking index : {0}", index1);

                    bool exists = client.IndexExists(index1).Exists;

                    if (!exists)
                    {
                        _logger.Info("\t does not exist");
                    }
                    else
                    {
                        IEnumerable<dynamic> before =
                            client.Search(
                                s => s.Index(index1).Query(q => q.Range(r => r.OnField("@timestamp").To(_toDate))))
                                  .Documents;

                        if (before.Any())
                        {
                            client.DeleteByQuery(q => q.Index(index1).Range(r => r.OnField("@timestamp").To(_toDate)));

                            IEnumerable<dynamic> after =
                                client.Search(
                                    s => s.Index(index1).Query(q => q.Range(r => r.OnField("@timestamp").To(_toDate))))
                                      .Documents;


                            if (before.Count() > after.Count())
                            {
                                _logger.Info("\t deleted documents");
                            }
                            else
                            {
                                _logger.Warn("\t did not delete documents?!");
                            }
                        }
                        else
                        {
                            _logger.Info("\t no matching documents");
                        }


                        // clean up
                        DeleteEmptyIndex(client, index1);
                    }
                }
            }
            else
            {
                var exception = new Exception("could not connect");
                _logger.Error(exception);
                throw exception;
            }
        }

        private static void DeleteEmptyIndex(ElasticClient client, string index1)
        {
            IQueryResponse<dynamic> result2 = client.Search(s => s.Index(index1));
            int count = result2.Documents.Count();

            if (count != 0)
            {
                _logger.Info("\t not empty");
            }
            else
            {
                _logger.Info("\t is empty. deleting index");
                client.DeleteIndex(index1);
            }
        }


        public  Mapping GetMappings(Uri uri)
        {
            uri = new Uri(uri, "/_mapping?pretty");
            var client = new WebClient();
            var result = client.DownloadString(uri);
            var obj = (JObject)JsonConvert.DeserializeObject(result);
            var mapping = new Mapping();
            foreach (var property in obj.Properties())
            {

                var indexName = property.Name;
                mapping.Indices.Add(indexName);

                var index = (JObject)property.Value;
                foreach (var type in index.Properties())
                {
                    string typeName = type.Name;

                    if (!mapping.Types.Contains(typeName))
                    {
                        mapping.Types.Add(typeName);
                    }

                }

            }

            mapping.Types.Sort();
            return mapping;

        }
    }
}