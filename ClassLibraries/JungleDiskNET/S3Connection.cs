/**
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License version 2 as published by
 *  the Free Software Foundation
 *  
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *  
 *  You should have received a copy of the GNU General Public License
 *  along with this program; if not, write to the Free Software
 *  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Net;
using System.Xml;
using System.IO;
using System.Security.Cryptography;

namespace JungleDisk
{
    public class S3Connection
    {
        public delegate string PasswordRequiredDelegate(string description);

        internal string accessKey;
        internal string secretKey;
        internal string host = "s3.amazonaws.com";
        internal bool IsUS = true;

        // These make indexing to buckets easier:
        internal string bucket = "";
        internal string pathPrefix = "";
        internal string bucketPath = "";

        internal PasswordRequiredDelegate passwordRequiredDelegate = null;

        public S3Connection(string accessKey, string secretKey)
        {
            this.accessKey = accessKey;
            this.secretKey = secretKey;
        }

        protected string RequestPassword(string details)
        {
            if (passwordRequiredDelegate != null)
                return passwordRequiredDelegate(details);
            else
                return null;
        }

        public bool SetBucket(string rootBucket, string subBucket, bool isUS)
        {
            if (bucket == rootBucket && (pathPrefix.Length >= subBucket.Length ? subBucket == pathPrefix.Substring(0, subBucket.Length) : false))
                return false; // Return false if no change.
            bucket = rootBucket;
            pathPrefix = subBucket;
            if (pathPrefix.Length > 0)
                pathPrefix += "/";
            IsUS = isUS;
            bucketPath = "/";
            if (IsUS)
            {
                host = "s3.amazonaws.com";
                bucketPath += bucket + "/";
            }
            else
            {
                host = bucket + "." + "s3.amazonaws.com";
                if (pathPrefix.Length > 0)
                    bucketPath += pathPrefix;
            }
            return true;
        }

        public List<string> GetBucketList()
        {
            List<string> output = new List<string>();

            S3Request request = new S3Request(this);
            request.Method = S3Request.RequestMethod.rmGet;
            request.Resource = "/";

            S3Response response = null;
            response = request.Perform();

            XmlDocument doc = new XmlDocument();
            doc.Load(response.GetResponseStream());
            response.Close();

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("s3", doc.LastChild.NamespaceURI); //"http://s3.amazonaws.com/doc/2006-03-01/");

            XmlNodeList xmlBuckets = doc.SelectNodes("/s3:ListAllMyBucketsResult/s3:Buckets/s3:Bucket/s3:Name", nsmgr);
            foreach(XmlNode xmlBucketName in xmlBuckets)
                output.Add(xmlBucketName.InnerText);
            return output;
        }

        public bool BucketExists(string bucketName)
        {
            List<string> buckets = GetBucketList();
            foreach(string bucket in buckets)
            {
                if (bucket == bucketName)
                    return true;
            }
            return false;
        }

        public delegate void ReceivePrefixDelegate(string prefix);
        public delegate void ReceiveContentsDelegate(string contents);

        public List<string> GetObjectList(string prefix, ref string marker, int maxEntries, string delimiter, ReceiveContentsDelegate contentsDelegate, ReceivePrefixDelegate prefixDelegate)
        {
            bool cancelled = false; // True if we have been asked to stop (not presently used).
            bool truncated = false; // True if there were too many entries to list in one go.
            string lastKey = marker;
            List<string> output = new List<string>();
            do
            {
                string signPath = "/";
                if (IsUS)
                    signPath += bucket;
                string path = signPath + "?prefix=" + HttpUtility.UrlEncode(pathPrefix + prefix);
                int prefixLength = (pathPrefix + prefix).Length;

                if (marker.Length > 0)
                    path += "&marker=" + HttpUtility.UrlEncode(marker);
                if (delimiter != null && delimiter.Length > 0)
                    path += "&delimiter=" + HttpUtility.UrlEncode(delimiter);
                
                S3Request request = new S3Request(this);
                request.Resource = path;
                request.Method = S3Request.RequestMethod.rmGet;
                S3Response response = request.Perform();

                XmlDocument doc = new XmlDocument();
                doc.Load(response.GetResponseStream());
                response.Close();
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("s3", doc.LastChild.NamespaceURI); //"http://s3.amazonaws.com/doc/2006-03-01/");

                XmlNode truncatedNode = doc.SelectSingleNode("//s3:IsTruncated", nsmgr);
                truncated = truncatedNode.InnerText.ToUpper().StartsWith("T");

                XmlNodeList contentNodes = doc.SelectNodes("//s3:Contents", nsmgr);
                foreach (XmlNode contentNode in contentNodes)
                {
                    string key = contentNode.SelectSingleNode("./s3:Key", nsmgr).InnerText;
                    if(contentsDelegate != null)
                        contentsDelegate(key.Remove(0, prefixLength));
                    lastKey = key;
                }
                XmlNodeList prefixNodes = doc.SelectNodes("//s3:CommonPrefixes/s3:Prefix", nsmgr);
                if(prefixDelegate != null)
                    foreach (XmlNode prefixNode in prefixNodes)
                        prefixDelegate(prefixNode.InnerText.Remove(0, prefixLength));

            } while (truncated && (maxEntries == 0 || output.Count < maxEntries) && !cancelled);

            marker = truncated ? lastKey : "";
            return output;
        }

        /// <summary>
        /// Returns appropriately URL-encoded path for the specified object.
        /// </summary>
        protected string GetPathForObject(string obj)
        {
            string resource = "";
            if (IsUS)
                resource += "/" + bucket;
            resource += "/" + pathPrefix + obj;
            return resource;
        }

        // Important: Caller must close the returned Stream!
        public Stream GetObject(string path)
        {
            S3Request request = new S3Request(this);
            request.Resource = GetPathForObject(path);
            request.Method = S3Request.RequestMethod.rmGet;
            S3Response response = request.Perform();
            return response.GetResponseStream();
        }

        public static string SlurpInputStream(Stream stream)
        {
            System.Text.Encoding encode =
                System.Text.Encoding.GetEncoding("utf-8");
            StreamReader readStream = new StreamReader(stream, encode);
            const int stride = 4096;
            Char[] read = new Char[stride];

            int count = readStream.Read(read, 0, stride);
            StringBuilder data = new StringBuilder();
            while (count > 0)
            {
                string str = new string(read, 0, count);
                data.Append(str);
                count = readStream.Read(read, 0, stride);
            }
            stream.Close();
            return data.ToString();
        }

        public string GetObjectAsString(string path)
        {
            return SlurpInputStream(GetObject(path));
        }

        public void PutObject(string path, byte[] contents, string encryptionKey)
        {
            PutObject(path, new MemoryStream(contents != null ? contents : new byte[0]), encryptionKey);
        }
        public void PutObject(string path, Stream stream, string encryptionKey)
        {
            S3Request request = new S3Request(this);
            request.Resource = GetPathForObject(path);
            request.Method = S3Request.RequestMethod.rmPut;
            request.EncryptionKey = encryptionKey;
            S3Response response = request.Perform(stream);
            response.Close();
        }

        public void DeleteObject(string path)
        {
            S3Request request = new S3Request(this);
            request.Resource = GetPathForObject(path);
            request.Method = S3Request.RequestMethod.rmDelete;
            S3Response response = request.Perform(null);
            response.Close();
        }

        #region Encryption

        public List<string> Keys = new List<string>();
        public string GetKeyFromHash(string keyHash, string details)
        {
            if (keyHash.Length != 8 + 32)
                throw new Exception("Invalid encryption hash length");
            string saltStr = keyHash.Substring(0, 8);
            uint salt = Convert.ToUInt32(saltStr, 16);
            foreach (string key in Keys)
            {
                string hash = Crypto.CreateSaltedKeyHash(salt, key, false);
                if (hash == keyHash)
                    return key;
                hash = Crypto.CreateSaltedKeyHash(salt, key, true);
                if (hash == keyHash)
                    return key;
            }
            // If we are here, no qualifying key was found.
            string password = RequestPassword(details);
            if (password != null)
            {
                Keys.Add(password);
                return GetKeyFromHash(keyHash, details);
            }
            return null;
        }
        
        #endregion

		public void CreateBucket(string bucketName)
		{
			S3Request request = new S3Request(this);
			request.Resource = "/" + bucketName;
			request.Method = S3Request.RequestMethod.rmPut;

            S3Response response = null;

            try
            {
                response = request.Perform(null);
            }
            catch (S3Exception ex)
            {
                // Check for "BucketAlreadyOwnedByYou" server code, which is okay.
                if (ex.Code != "BucketAlreadyOwnedByYou")
                    throw ex;
            }

            if(response != null)
    			response.Close();
		}
	}

    public class S3Exception : Exception
    {
        public string Code = null;
        public new string Message = null;
        public string Response = null;
        public S3Exception(string response)
        {
            Response = response;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Response);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("s3", doc.LastChild.NamespaceURI);

            Code = doc.SelectSingleNode("/s3:Error/s3:Code", nsmgr).InnerText;
            Message = doc.SelectSingleNode("/s3:Error/s3:Message", nsmgr).InnerText;
        }
        public override string ToString()
        {
            return "S3 Code: " + Code + ", " + Message;
        }
    }
}
