using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Data.Xml.Dom;



namespace MetroUsageMeter
{
    class NodeConnector
    {
        protected Uri nodeApiUri;
        private ICredentials creds;

        protected Uri resourcesUri;
        protected Uri historyUri;
        protected Uri usageUri;
        protected Uri serviceUri;

        protected long usageBytes;
        protected long quotaBytes;
        protected DateTime rolloverDate;
        protected DateTime startDate;
        protected string interval;

        public NodeConnector(Uri uri)
        {
            this.nodeApiUri = uri;
        }

        public Uri NodeApiUri { get { return this.nodeApiUri; } }
        public ICredentials Creds { set { this.creds = value; } }
        public long UsageBytes { get { return this.usageBytes; } }
        public long QuotaBytes { get { return this.quotaBytes; } }
        public DateTime RolloverDate { get { return this.rolloverDate; } }
        public DateTime StartDate { get { return this.startDate; } }
        public string Interval { get { return this.interval; } }

        public async Task refresh()
        {
            if (resourcesUri == null)
            {
                XmlDocument servicesXML = new XmlDocument();
                servicesXML.LoadXml(await webRequest(nodeApiUri));
                IXmlNode serviceNode = servicesXML.GetElementsByTagName("service").Item(0);
                resourcesUri = new Uri(nodeApiUri, (string)serviceNode.Attributes.GetNamedItem("href").NodeValue);
            }

            if (historyUri == null || usageUri == null || serviceUri == null)
            {
                XmlDocument resourcesXML = new XmlDocument();
                resourcesXML.LoadXml(await webRequest(resourcesUri));
                XmlNodeList resourceNodes = resourcesXML.GetElementsByTagName("resource");
                foreach (IXmlNode resourceNode in resourceNodes)
                {
                    string type = (string)resourceNode.Attributes.GetNamedItem("type").NodeValue;
                    string href = (string)resourceNode.Attributes.GetNamedItem("href").NodeValue;
                    if (type == "history")
                    {
                        historyUri = new Uri(resourcesUri, href);
                    }
                    else if (type == "usage")
                    {
                        usageUri = new Uri(resourcesUri, href);
                    }
                    else if (type == "service")
                    {
                        serviceUri = new Uri(resourcesUri, href);
                    }
                }
            }

            XmlDocument usageXML = new XmlDocument();
            usageXML.LoadXml(await webRequest(usageUri));
            IXmlNode usageNode = usageXML.GetElementsByTagName("traffic").Item(0);

            string rollover = (string)usageNode.Attributes.GetNamedItem("rollover").NodeValue;
            int rolloverYear = Convert.ToInt32(rollover.Split('-').ElementAt(0));
            int rolloverMonth = Convert.ToInt32(rollover.Split('-').ElementAt(1));
            int rolloverDay = Convert.ToInt32(rollover.Split('-').ElementAt(2));
            DateTime rolloverDate = new DateTime(rolloverYear, rolloverMonth, rolloverDay);
            
            string interval = (string)usageNode.Attributes.GetNamedItem("plan-interval").NodeValue;
            
            DateTime startDate;
            if (interval.Equals("Quarterly", StringComparison.CurrentCultureIgnoreCase))
            {
                startDate = rolloverDate.AddMonths(-3);
            }
            else
            {
                startDate = rolloverDate.AddMonths(-1);
            }

            lock(this)
            {
                this.usageBytes = Convert.ToInt64(usageNode.InnerText);
                this.quotaBytes = Convert.ToInt64((string)usageNode.Attributes.GetNamedItem("quota").NodeValue);
                this.rolloverDate = rolloverDate;
                this.startDate = startDate;
                this.interval = interval;
            }

            this.creds = null; // allow GC to collect...
        }

        protected async Task<string> webRequest(Uri uri)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.Credentials = creds;
            HttpClient http = new HttpClient(handler);
            HttpResponseMessage response = await http.GetAsync(uri);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
