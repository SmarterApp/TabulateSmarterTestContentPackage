using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml.Linq;
using System.Linq;

// From GitLab API Documentation Here: https://docs.gitlab.com/ce/api/

/* API Notes
A user has a namespace ID and a user ID, both numeric, that are different.
Likewise, a group has a namespace ID and a group ID, both numeric, that seem to be the samet.
When you look up a namespace, the ID that's returned is the namespace ID and does not
correspond to the user ID.

The get projects by user API doesn't seem to work (/users/:id/projects). It returns a 404.

To get projects by user, have to know the user's namespace ID, then get all projects and
then filter by the namespace ID.

*/

namespace TabulateSmarterTestContentPackage
{
    /// <summary>
    /// C# Connection to the GitLab API
    /// </summary>
    class GitLab
    {
        const string c_GitLabApiPath = "/api/v4/";

        // GitLab API max items per page is 100.
        const int c_elementsPerPage = 100;

        Uri m_baseAddress;
        string m_accessToken;

        public GitLab(string serverUrl, string accessToken)
        {
            m_baseAddress = new Uri(serverUrl);
            m_accessToken = accessToken;
        }

        // Throws a detailed exception if access is denied.
        public void VerifyAccess(string nameSpace)
        {
            string nsId;
            bool isGroup;
            GetNamespaceIdAndType(nameSpace, out nsId, out isGroup);
        }

        public IEnumerable<XElement> GetProjectsInNamespace(string ns)
        {
            string nsId;
            bool isGroup;
            GetNamespaceIdAndType(ns, out nsId, out isGroup);

            var uri = new UriBuilder(m_baseAddress);
            if (isGroup)
            {
                uri.Path = string.Concat(c_GitLabApiPath, "groups/", Uri.EscapeDataString(nsId), "/projects");
            }
            else
            {
                // Will retrieve all projects the logged-in user can see which may be more than those
                // actually owned by user specified as namespace. This means we have to filter the results which
                // may be inefficient if the user has access to a lot of projects.
                // Unfortunately, the API doesn't seem to offer a way to filter by user as owner.
                // (/users/:id/projects is in the API docs but it doesn't work.)
                uri.Path = string.Concat(c_GitLabApiPath, "projects");
            }
            uri.Query = "per_page=" + c_elementsPerPage.ToString();

            return new ProjectsEnumerable(this, uri.Uri, nsId);
        }

        /// <summary>
        /// Get the ID that corresponds to a project (repository) name. 
        /// </summary>
        /// <param name="ns">The namespace (username or group name) that owns the project.</param>
        /// <param name="name">The name of the project.</param>
        /// <returns>The project ID.</returns>
        public string ProjectIdFromName(string ns, string name)
        {
            UriBuilder uri = new UriBuilder(m_baseAddress);
            uri.Path = string.Concat(c_GitLabApiPath, "projects/", Uri.EscapeDataString(string.Concat(ns, "/", name)));
            var doc = HttpReceiveJson(uri.Uri);
            return doc.Element("id").Value;
        }

        /// <summary>
        /// List the blobs (files) that belong to a project (repository).
        /// </summary>
        /// <param name="projectId">The project ID for which to list files.</param>
        /// <returns>A list of key value pairs. The keys are the names of the blobs (files). The values are the IDs.</returns>
        public IReadOnlyList<KeyValuePair<string, string>> ListRepositoryTree(string projectId)
        {

            var result = new List<KeyValuePair<string, string>>();

            UriBuilder ub = new UriBuilder(m_baseAddress);
            ub.Path = string.Concat(c_GitLabApiPath, "projects/", Uri.EscapeDataString(projectId), "/repository/tree");
            ub.Query = $"recursive=true&per_page={c_elementsPerPage}";

            // This API is paginated, it may require multiple requests to retrieve all items.
            Uri uri = ub.Uri;
            int totalExpectedFiles = 0;
            int page = 1;   // GitLab numbers pages starting with 1
            while (uri != null)
            {
                var request = HttpPrepareRequest(uri);
                using (var response = HttpGetResponseHandleErrors(request))
                {
                    // Get the total expected files
                    int.TryParse(response.GetResponseHeader("X-Total"), out totalExpectedFiles);

                    // Get the returned page number and check to make sure it was the right one.
                    int pageReturned = 0;
                    int.TryParse(response.GetResponseHeader("X-Page"), out pageReturned);
                    if (pageReturned != page)
                    {
                        throw new ApplicationException($"GitLab returned page {pageReturned} expected {page}");
                    }
                    ++page;

                    // Get the next page URI (returns null if no more pages)
                    uri = HttpNextPageUri(response);

                    // Retrieve the files
                    var doc = HttpReceiveJson(response);
                    foreach (var el in doc.Elements("item"))
                    {
                        result.Add(new KeyValuePair<string, string>(el.Element("path").Value, el.Element("id").Value));
                    }
                }
            }

            if (result.Count != totalExpectedFiles)
            {
                throw new ApplicationException($"Expected {totalExpectedFiles} files in item but received {result.Count}");
            }

            return result;
        }

        /// <summary>
        /// Reads a blob (file) from a project on GitLab.
        /// </summary>
        /// <param name="projectId">The id of the project containing the blob.</param>
        /// <param name="blobId">The id of the blob (not the name).</param>
        /// <returns>A read-forward stream with the contents of the blob.</returns>
        /// <remarks>
        /// Be sure to dispose the stream when reading is complete.
        /// </remarks>
        public Stream ReadBlob(string projectId, string blobId, out long length)
        {
            UriBuilder uri = new UriBuilder(m_baseAddress);
            uri.Path = string.Concat(c_GitLabApiPath, "projects/", Uri.EscapeDataString(projectId), "/repository/blobs/", blobId, "/raw");

            try
            {
                HttpWebRequest request = HttpPrepareRequest(uri.Uri);
                WebResponse response = HttpGetResponseHandleErrors(request);
                length = response.ContentLength;
                return response.GetResponseStream(); // Closing the stream also closes the response so we don't need to dispose response
            }
            catch (WebException ex)
            {
                throw ConvertWebException(ex);
            }
        }

        /// <summary>
        /// Gets the size of a Blob on GitLab.
        /// </summary>
        /// <param name="projectId">The id of the project containing the blob.</param>
        /// <param name="blobId">The id of the blob (not the name).</param>
        /// <returns>The size of the blob.</returns>
        /// <remarks>
        /// Uses HTTP HEAD
        /// </remarks>
        public long GetBlobSize(string projectId, string blobId)
        {
            UriBuilder uri = new UriBuilder(m_baseAddress);
            uri.Path = string.Concat(c_GitLabApiPath, "projects/", Uri.EscapeDataString(projectId), "/repository/blobs/", blobId, "/raw");
            try
            {
                HttpWebRequest request = HttpPrepareRequest(uri.Uri);
                request.Method = "HEAD";
                using (WebResponse response = HttpGetResponseHandleErrors(request))
                {
                    return response.ContentLength;
                }
            }
            catch (WebException ex)
            {
                throw ConvertWebException(ex);
            }
        }

        // Determines whether a namespace is a group (as opposed to a user)
        private void GetNamespaceIdAndType(string ns, out string id, out bool isGroup)
        {
            // Determine whether namespace is a username or a group name
            UriBuilder uri = new UriBuilder(m_baseAddress);
            uri.Path = c_GitLabApiPath + "namespaces";
            uri.Query = "search=" + Uri.EscapeDataString(ns);
            var doc = HttpReceiveJson(uri.Uri);
            //DumpXml(doc);

            foreach (var el in doc.Elements("item"))
            {
                if (el.Element("name").Value.Equals(ns, StringComparison.Ordinal))
                {
                    id = el.Element("id").Value;
                    isGroup = el.Element("kind").Value.Equals("group", StringComparison.OrdinalIgnoreCase);
                    return;
                }
            }

            throw new ApplicationException($"Namespace '{ns}' not found in GitLab item bank.");
        }

        private HttpWebRequest HttpPrepareRequest(Uri uri)
        {
            HttpWebRequest request = WebRequest.CreateHttp(uri);
            request.Headers["PRIVATE-TOKEN"] = m_accessToken;
            return request;
        }

        private static HttpWebResponse HttpGetResponseHandleErrors(HttpWebRequest request)
        {
            try
            {
                return (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                throw ConvertWebException(ex);
            }
        }

        private static XElement HttpReceiveJson(HttpWebResponse response)
        {
            XElement doc = null;
            using (var stream = response.GetResponseStream())
            {
                using (var jsonReader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas()))
                {
                    doc = XElement.Load(jsonReader);
                }
            }
            return doc;
        }

        private XElement HttpReceiveJson(Uri uri)
        {
            HttpWebRequest request = HttpPrepareRequest(uri);
            using (var response = HttpGetResponseHandleErrors(request))
            {
                return HttpReceiveJson(response);
            }
        }

        private static Uri HttpNextPageUri(HttpWebResponse response)
        {
            var parser = new HttpLinkHeaderParser(response);
            while (parser.MoveNext())
            {
                if ((parser.CurrentParameters["rel"] ?? string.Empty).Equals("next", StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(parser.CurrentUrl);
                }
            }
            return null;
        }

        private static Exception ConvertWebException(WebException ex)
        {
            HttpWebResponse response = null;
            try
            {
                response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    string detail;
                    using (var reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        detail = reader.ReadToEnd().Trim();
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return new HttpNotFoundException(string.Concat("HTTP Resource Not Found: ", response.ResponseUri, "\r\n", detail));
                    }
                    else if (detail.Length > 0)
                    {
                        return new HttpErrorException(string.Concat(ex.Message, "\r\n", detail));
                    }
                    else
                    {
                        return ex;
                    }
                }
                return ex;
            }
            finally
            {
                if (response != null)
                {
                    response.Dispose();
                    response = null;
                }
            }
        }

        static void DumpXml(XElement xml)
        {
            var settings = new System.Xml.XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = true;
            using (var writer = System.Xml.XmlWriter.Create(Console.Error, settings))
            {
                xml.WriteTo(writer);
            }
            Console.Error.WriteLine();
        }

        private class ProjectsEnumerable : IEnumerable<XElement>
        {
            GitLab m_gitlab;
            Uri m_url;
            string m_nsId;

            public ProjectsEnumerable(GitLab gitlab, Uri url, string nsId)
            {
                m_gitlab = gitlab;
                m_url = url;
                m_nsId = nsId;
            }

            public IEnumerator<XElement> GetEnumerator()
            {
                return new ProjectsEnumerator(m_gitlab, m_url, m_nsId);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            const int c_MaxRetriesEnumProjects = 5;

            private class ProjectsEnumerator : IEnumerator<XElement>
            {
                GitLab m_gitlab;
                string m_nsId;
                Uri m_url;
                Uri m_nextUrl;
                IEnumerator<XElement> m_projects;
                XElement m_current;

                public ProjectsEnumerator(GitLab gitlab, Uri url, string nsId)
                {
                    m_gitlab = gitlab;
                    m_url = url;
                    m_nsId = nsId;
                    m_nextUrl = m_url;
                }

                public XElement Current => m_current;

                object IEnumerator.Current => m_current;

                public bool MoveNext()
                {
                    for (; ; )
                    {
                        if (m_projects == null)
                        {
                            if (m_nextUrl == null)
                            {
                                return false;
                            }

                            // Try this up to five times - with the large count of projects, this sometimes times out.
                            XElement doc;
                            for (int iteration = 0; ; ++iteration)
                            {
                                try
                                {
                                    HttpWebRequest request = m_gitlab.HttpPrepareRequest(m_nextUrl);
                                    using (var response = HttpGetResponseHandleErrors(request))
                                    {
                                        m_nextUrl = HttpNextPageUri(response);
                                        doc = HttpReceiveJson(response);
                                    }
                                }
                                catch (Exception err)
                                {
                                    if (iteration >= c_MaxRetriesEnumProjects)
                                    {
                                        throw err;
                                    }
#if DEBUG
                                    Console.WriteLine();
                                    Console.WriteLine($"Retry: {err.Message}");
#endif
                                    continue;
                                }
                                break;
                            }

                            m_projects = doc.Elements("item").GetEnumerator();
                        }

                        System.Diagnostics.Debug.Assert(m_projects != null);
                        if (!m_projects.MoveNext())
                        {
                            m_projects.Dispose();
                            m_projects = null;
                            continue;
                        }

                        m_current = m_projects.Current;
                        {
                            // There's an elegant Linq to XML way of doing this but it throws null reference exceptions if an element isn't found.
                            var el1 = m_current.Element("namespace");
                            if (el1 == null) continue;
                            var el2 = el1.Element("id");
                            if (el2 == null) continue;
                            if (!el2.Value.Equals(m_nsId, StringComparison.Ordinal)) continue;
                        }
                        return true;
                    }
                }

                public void Reset()
                {
                    m_projects = null;
                    m_current = null;
                    m_nextUrl = m_url;
                }

                public void Dispose()
                {
                    if (m_projects != null)
                    {
                        m_projects.Dispose();
                    }
                    m_projects = null;
                }
            }
        }
    }

    class HttpNotFoundException : Exception
    {
        public HttpNotFoundException(string message)
            : base(message)
        {
        }

        public HttpNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    class HttpErrorException : Exception
    {
        public HttpErrorException(string message)
            : base(message)
        {
        }

        public HttpErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Parse a link header value according to http://www.rfc-editor.org/rfc/rfc5988.txt
    /// </summary>
    class HttpLinkHeaderParser
    {
        string m_header;
        int m_cursor;

        string m_currentUrl;
        Dictionary<string, string> m_currentParameters = new Dictionary<string, string>();

        public HttpLinkHeaderParser(string linkHeaderValue)
        {
            m_header = linkHeaderValue;
            m_cursor = 0;
        }

        public HttpLinkHeaderParser(HttpWebResponse response)
        {
            m_header = response.GetResponseHeader("Link");
            m_cursor = 0;
        }

        public string CurrentUrl
        {
            get { return m_currentUrl; }
        }

        public IReadOnlyDictionary<string, string> CurrentParameters
        {
            get { return m_currentParameters; }
        }

        public bool MoveNext()
        {
            m_currentParameters.Clear();
            int end = m_header.Length;
            SkipWhiteSpace();

            // URL should be next (enclosed in angle brackets)
            if (m_cursor >= end || m_header[m_cursor] != '<')
            {
                m_cursor = end;
                return false;
            }
            ++m_cursor;

            // Parse the URL
            m_currentUrl = ParseUntil('>');
            if (m_cursor < end) ++m_cursor;

            SkipWhiteSpace();

            // Parse parameters
            while (m_cursor < end && m_header[m_cursor] == ';')
            {
                ++m_cursor;
                SkipWhiteSpace();

                // Parse parameter name
                string parmName = ParseUntil(s_spaceOrEq);
                SkipWhiteSpace();

                // Parse parameter value
                string parmValue = string.Empty;
                if (m_cursor < end && m_header[m_cursor] == '=')
                {
                    ++m_cursor;
                    SkipWhiteSpace();

                    // Quoted string value
                    if (m_cursor < end && m_header[m_cursor] == '"')
                    {
                        parmValue = ParseQuotedString();
                    }
                    else
                    {
                        parmValue = ParseUntil(s_spaceCommaOrSemi);
                    }
                }
                m_currentParameters.Add(parmName.ToLower(), parmValue);

                SkipWhiteSpace();
            }

            // Move to next input (if any)
            if (m_cursor < end && m_header[m_cursor] == ',')
            {
                ++m_cursor;
            }
            else
            {
                m_cursor = end;
            }

            return true;
        }

        private void SkipWhiteSpace()
        {
            while (m_cursor < m_header.Length && char.IsWhiteSpace(m_header[m_cursor])) ++m_cursor;
        }

        private string ParseUntil(char c)
        {
            int i = m_header.IndexOf(c, m_cursor);
            if (i < 0) i = m_header.Length;
            string value = m_header.Substring(m_cursor, i - m_cursor);
            m_cursor = i;
            return value;
        }

        private string ParseUntil(params char[] anyOf)
        {
            int i = m_header.IndexOfAny(anyOf, m_cursor);
            if (i < 0) i = m_header.Length;
            string value = m_header.Substring(m_cursor, i - m_cursor);
            m_cursor = i;
            return value;
        }

        static readonly char[] s_spaceOrGt = new char[] { '>', ' ', '\r', '\n', '\t' };
        static readonly char[] s_spaceOrEq = new char[] { '=', ' ', '\r', '\n', '\t' };
        static readonly char[] s_spaceCommaOrSemi = new char[] { ',', ';', ' ', '\r', '\n', '\t' };

        private string ParseQuotedString()
        {
            if (m_header[m_cursor] != '"') return string.Empty;

            var sb = new StringBuilder();
            int end = m_header.Length;
            int i = m_cursor + 1;
            while (i < end && m_header[i] != '"')
            {
                // Handle backslash escaping
                if (m_header[i] == '\\') ++i;
                sb.Append(m_header[i]);
                ++i;
            }
            if (i < end) ++i; // Skip closing quote
            m_cursor = i;
            return sb.ToString();
        }
        
    }
}
