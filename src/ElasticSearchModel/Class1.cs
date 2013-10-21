using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ElasticSearchModel
{
    public class Mapping
    {
        public Mapping()
        {
            Indices = new List<string>();
            Types = new List<string>();
        }
        public List<string> Indices { get; set; }
        public List<string> Types { get; set; }



        public class IndexObj
        {
            public IndexObj()
            {
                Types = new List<TypeObj>();
            }
            public List<TypeObj> Types { get; set; }
        }
        public class TypeObj
        {
            public object[] DynamicTemplates { get; set; }
            public bool AllEnabled { get; set; }
            public bool SourceCompress { get; set; }

        }

        public class PropertyObj
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Index { get; set; }
            public bool OmitNorms { get; set; }
            public string IndexOptions { get; set; }
        }
    }


}
