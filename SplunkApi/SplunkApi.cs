//-----------------------------------------------------------------------
// <copyright file="SplunkApi.cs" company="VitalyAkulov">
//     API to connect to Splunk server. Copyright by Vitaly Akulov.
// </copyright>
//-----------------------------------------------------------------------

namespace SplunkTest
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Web;
    using System.Xml;


    /// <summary>
    /// Wrapper for Splunk connectivity actions.
    /// </summary>
    public class SplunkApi
    {
        private string _userName;
        private string _password;
        private string _sessionKey;
        public string SessionKey { get { return _sessionKey; } }
        private string baseUrl;
        public string BaseUrl { get { return baseUrl; } }
        private static Random _random = new Random();

        public SplunkApi(string serverName, string userName, string password, int port = 8089, bool useSSL=true)
        {
            this.baseUrl = useSSL ? string.Format("https://{0}:{1}", serverName, port) : string.Format("http://{0}:{1}", serverName, port);
            _userName = userName;
            _password = password;

            // Trust all certificates. Not suitable for production code but should be OK for testing
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            ServicePointManager.DefaultConnectionLimit = 5000;
        }

        public void Connect()
        {
            string authUrl = this.baseUrl + "/servicesNS/admin/search/auth/login";
            XmlDocument doc = HttpPost(authUrl, new[] { "username", "password" }, new[] { _userName, _password });
            _sessionKey = doc.SelectSingleNode("/response/sessionKey").InnerText;
        }

        public void EnableHttpInput()
        {
            //enable logging endpoints
            string enableUrl = this.baseUrl + "/servicesNS/admin/search/data/inputs/token/http/http";
            XmlDocument doc = HttpPost(enableUrl, new[] { "disabled" }, new[] { "0" });
        }

        public string CreateHttpToken(string tokenName)
        {
            try
            {
                var xmlDoc = HttpPost(this.BaseUrl + "/servicesNS/admin/search/data/inputs/token/http", new[] { "name", "description" }, new[] { tokenName, "token description" });
                XmlNode node = xmlDoc.LastChild;
                while (node.LastChild != null)
                    node = node.LastChild;
                return node.InnerText;
            }
            catch (System.Net.WebException)
            {
            }
            return "";
        }

        public void DeleteHttpToken(string tokenName)
        {
            try
            {
                HttpDelete(string.Format("{0}/servicesNS/admin/search/data/inputs/token/http/{1}", this.BaseUrl, tokenName));
            }
            catch (System.Net.WebException)
            {
            }
        }

        public bool SendDataViaHttp(string token, string postData)
        {
            WebRequest request = WebRequest.Create(this.BaseUrl + "/services/receivers/token");
            request.Method = "POST";
            request.Headers.Add("Authorization", "Splunk " + token);
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;
            using (Stream dataStream = request.GetRequestStream())
            {
                dataStream.Write(byteArray, 0, byteArray.Length);
            }

            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    string status = ((HttpWebResponse)response).StatusDescription;
                    if (status != "OK")
                    {
                        Console.WriteLine("{1}\tFailed to insert data. Status is {0}", status, DateTime.Now);
                        System.Environment.Exit(-1);
                    }
                    using (Stream dataStream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(dataStream))
                        {
                            string responseFromServer = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (System.Net.WebException e)
            {
                return false;
            }
            return true;
        }
        public XmlDocument Search(string query, bool saveResult, int upperTimeoutInMinutes, bool showStats = true)
        {
            string searchUrl = this.baseUrl + "/services/search/jobs";

            // Dispatch a new search and return search id
            DateTime tStart = DateTime.Now;
            string searchTimeout = _random.Next(1, 600).ToString(); // Saved searches should be deleted after completion
            XmlDocument doc = HttpPost(searchUrl, new[] { "search", "timeout" }, new[] { string.Format("search {0}", query), searchTimeout });
            string sid = doc.SelectSingleNode("/response/sid").InnerText;

            // Wait for search to finish
            string searchJobUrl = this.baseUrl + "/services/search/jobs/" + sid;
            bool isDone = false;
            int eventCount = 0;
            do
            {
                if ((DateTime.Now - tStart).TotalMinutes > upperTimeoutInMinutes)
                {
                    // No response for XX minutes
                    throw new TimeoutException(string.Format("No results were returned for job {0} for last {1} seconds. Aborting this job.", sid, (DateTime.Now - tStart).TotalSeconds));
                }
                doc = HttpGet(searchJobUrl);
                if (doc == null)
                {
                    Thread.Sleep(200);
                    continue;
                }

                var context = new XmlNamespaceManager(doc.NameTable);
                context.AddNamespace("s", "http://dev.splunk.com/ns/rest");
                context.AddNamespace("feed", "http://www.w3.org/2005/Atom");
                XmlNode ecNode = doc.SelectSingleNode("//feed:entry/feed:content/s:dict/s:key[@name='eventCount'][1]", context);
                XmlNode idNode = doc.SelectSingleNode("//feed:entry/feed:content/s:dict/s:key[@name='isDone'][1]", context);
                if (ecNode != null && idNode != null)
                {
                    eventCount = int.Parse(ecNode.InnerText);
                    isDone = idNode.InnerText == "1";
                }
            } while (!isDone);

            TimeSpan elapsedTime = DateTime.Now - tStart;
            if(showStats)
                Console.WriteLine("{0} {1} completed job {2,18}[timeout={3,3}] for '{4}', {5} results were found in {6:F2} seconds.", DateTime.Now, Thread.CurrentThread.Name, sid, searchTimeout, query, eventCount, elapsedTime.TotalSeconds);

            // Get search results
            if (eventCount > 0)
            {
                string searchResultsUrl = string.Format("{0}/services/search/jobs/{1}/results", this.baseUrl, sid);
                doc = HttpGet(searchResultsUrl);
                if (doc != null && saveResult)
                {
                    doc.Save(sid + ".xml");
                }
                return doc;
            }

            return null;
        }

        public XmlDocument HttpDelete(string url)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;

            request.Method = "DELETE";
            request.ContentType = "application/x-www-form-urlencoded";

            // Use session key if we already connected
            if (!string.IsNullOrEmpty(_sessionKey))
            {
                request.Headers.Add("Authorization", "Splunk " + _sessionKey);
            }

            // Pick up the response
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                var xmlDoc = new XmlDocument();
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    xmlDoc.Load(reader);
                }

                return xmlDoc;
            }
        }

        public XmlDocument HttpPost(string url, string[] paramName, string[] paramVal, string sessionKey = null)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            // Use session key if we already connected
            if (!string.IsNullOrEmpty(sessionKey))
            {
                request.Headers.Add("Authorization", "Splunk " + sessionKey);
            }
            else if (!string.IsNullOrEmpty(_sessionKey))
            {
                request.Headers.Add("Authorization", "Splunk " + _sessionKey);
            }

            // Build a string with all the params, properly encoded. We assume that the arrays paramName and paramVal are of equal length:
            var requestParameters = new StringBuilder();
            for (int i = 0; i < paramName.Length; i++)
            {
                requestParameters.AppendFormat("{0}={1}&", paramName[i], HttpUtility.UrlEncode(paramVal[i]));
            }

            // Remove last '&'
            requestParameters.Length -= 1;

            // Encode the parameters as form data:
            byte[] formData = UTF8Encoding.UTF8.GetBytes(requestParameters.ToString());

            // Send the request
            using (Stream post = request.GetRequestStream())
            {
                post.Write(formData, 0, formData.Length);
            }

            // Pick up the response
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                var xmlDoc = new XmlDocument();
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    xmlDoc.Load(reader);
                }

                return xmlDoc;
            }
        }

        public XmlDocument HttpGet(string url)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";
            request.ContentType = "application/x-www-form-urlencoded";

            // Use session key if we already connected
            if (!string.IsNullOrEmpty(_sessionKey))
            {
                request.Headers.Add("Authorization", "Splunk " + _sessionKey);
            }

            // Pick up the response:
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                var xmlDoc = new XmlDocument();
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    if (reader.EndOfStream)
                    {
                        return null;
                    }

                    xmlDoc.Load(reader);
                }

                return xmlDoc;
            }
        }
    }
}