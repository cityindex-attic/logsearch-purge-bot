
using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Globalization;
// external references
using ServiceStack.Text;



namespace LogstashPurge
{
	class MainClass
	{
		public static void Main (string[] args)
		{

			// http://ec2-79-125-57-123.eu-west-1.compute.amazonaws.com:9200
			// or, if you think i have the x-factor, plug in the live cluster ;)

			Uri elasticSearchUrl;

			if (args.Length == 0) {
				elasticSearchUrl = new Uri (Environment.GetEnvironmentVariable ("elasticSearchUrl"));
			} else {
				elasticSearchUrl = new Uri (args [0]);
			}

			var mapping = GetMappings (elasticSearchUrl);
			var toDate = new DateTime (DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day).Subtract (TimeSpan.FromDays (28));
			var toDateString = GetDayOnlyString (toDate);


			mapping.Indices.Sort ();
			foreach (var i in mapping.Indices) {

				Console.WriteLine ("checking index {0}", i);
				if (IndexExists (elasticSearchUrl, i)) {
					var count = GetCount (elasticSearchUrl, i, toDateString);
					if (count != 0) {
						Console.WriteLine ("deleting {0} records", count);
						Uri uri = new Uri (elasticSearchUrl, i + "/_query");

						string range = GetRangeString (toDateString);
						var queryBytes = Encoding.UTF8.GetBytes (range);
						/*string responseText = */
						GetResponseText (uri, "DELETE", queryBytes);

						//{"ok":true,"_indices":{"logstash-2013.07.21":{"_shards":{"total":4,"successful":4,"failed":0}}}}
					} else {
						Console.WriteLine ("index {0} has no matching records", i);
					}

					// then clean it up if empty
					if (DeleteIndexIfEmpty (elasticSearchUrl, i)) {
						Console.WriteLine ("deleted emtpy index {0}", i);
					}
				} else {
					Console.WriteLine ("index {0} does not exist", i);
				}

			}



		}

		public static bool DeleteIndexIfEmpty (Uri baseUri, string index)
		{
			var count = GetCount (baseUri, index, null);
			if (count == 0) {
				Uri uri = new Uri (baseUri, index);
				/*string responseText = */
				GetResponseText (uri, "DELETE", null);
				// {"ok":true,"acknowledged":true}
				return true;
			} else {
				return false;
			}
		}

		public static int GetCount (Uri baseUri, string index, string toDateString)
		{
			Uri uri = new Uri (baseUri, index + "/_count");

			byte[] queryBytes = null;
			if (!string.IsNullOrEmpty (toDateString)) {
				string range = GetRangeString (toDateString);
				queryBytes = Encoding.UTF8.GetBytes (range);
			}


			string responseText = GetResponseText (uri, "POST", queryBytes);

			Dictionary<string,object> responseObject = JsonSerializer.DeserializeFromString<Dictionary<string,object>> (responseText);
			return int.Parse (responseObject ["count"].ToString ());

		}

		public static string GetRangeString (string toDateString)
		{
			return  "{" +
				"    \"range\": {" +
				"      \"@timestamp\": {" +
				"        \"to\": \"" + toDateString + "\"" +
				"      }" +
				"    }" +
				"}";
		}

		public static string GetQueryString (string toDateString)
		{
			return  "{" + GetRangeString (toDateString) + "}";

		}

		public static bool IndexExists (Uri uri, string index)
		{
			string method = "HEAD";
			var u = new Uri (uri, index + "/");
			
			try {
				GetResponseText (u, method, null);
				return true;
			} catch {
				return false;
			}
			
		}

		public static string GetResponseText (Uri uri, string method, byte[] queryBytes)
		{
			string responseText;
			var req = (HttpWebRequest)WebRequest.Create (uri);
			
			req.Method = method;
			if (method.ToLower () == "post" || method.ToLower () == "delete") {
				if (queryBytes != null) {
					using (var reqStream = req.GetRequestStream()) {
						reqStream.Write (queryBytes, 0, queryBytes.Length);
					}   
				}
			}
			
			using (var response = (HttpWebResponse) req.GetResponse()) {
				if (response.StatusCode != HttpStatusCode.OK) {
					throw new Exception (string.Format ("status {0}", response.StatusCode));
				}
				using (var resStream = response.GetResponseStream()) {
					using (var streamReader = new StreamReader(resStream)) {
						responseText = streamReader.ReadToEnd ();
					}
				}
			}
			return responseText;
		}

		public static Mapping GetMappings (Uri uri)
		{

			string json = GetResponseText (new Uri (uri, "/_mapping?pretty"), "GET", null);



			Dictionary<string,Dictionary<string,object>> obj = JsonSerializer.DeserializeFromString<Dictionary<string,Dictionary<string,object>>> (json);

			var mapping = new Mapping ();

			foreach (string k in obj.Keys) {
				if (!string.IsNullOrEmpty (k)) {
					if (k.StartsWith ("logstash-")) {
						mapping.Indices.Add (k);
						var index = (Dictionary<string,object>)obj [k];
						foreach (string t in index.Keys) {
							if (!mapping.Types.Contains (t)) {
								mapping.Types.Add (t);
							}
						}


					}
				}
			}

			mapping.Types.Sort ();
			return mapping;
			
		}

		public static string GetDayOnlyString (DateTime toDate)
		{
			//2013-09-06T00:00:00
			var date1 = new DateTime (toDate.Year, toDate.Month, toDate.Day);
			var ci = CultureInfo.InvariantCulture;
			var toDateString = date1.ToString ("yyyy-MM-dd", ci) + "T00:00:00";
			return toDateString;
		}
	}

	public class Mapping
	{
		public Mapping ()
		{
			Indices = new List<string> ();
			Types = new List<string> ();
		}

		public List<string> Indices { get; set; }

		public List<string> Types { get; set; }
	}
}
