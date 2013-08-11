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
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Diagnostics;

namespace JungleDisk
{
    public class S3Request
    {
        internal string AccessKey;
        internal string SecretKey;
        internal bool IsUS;
        internal string Bucket;
        internal string Host;
        internal string EncryptionKey = null;
        internal string KeySalt = null;
        internal S3Connection Connection;
        
        public S3Request(S3Connection connection)
        {
            this.Connection = connection;
            this.AccessKey = connection.accessKey;
            this.SecretKey = connection.secretKey;
            this.Bucket = connection.bucket;
            this.IsUS = connection.IsUS;
            this.Host = connection.host;
        }

        public enum RequestMethod { rmGet, rmPut, rmDelete };
        public string RequestMethodToString(RequestMethod m)
        {
            switch(m)
            {
                case RequestMethod.rmGet: return "GET";
                case RequestMethod.rmPut: return "PUT";
                case RequestMethod.rmDelete: return "DELETE";
                default:
                    throw new Exception(string.Format("Request method {0} not recognized", m));
            }
        }

        public RequestMethod Method;
        protected string resource;
        public string Resource
        {
            get { return resource; }
            set {
                int questionMark = value.IndexOf('?');
                if (questionMark >= 0)
                    resource = Uri.EscapeUriString(value.Substring(0, questionMark)) + value.Substring(questionMark); // Don't want to double-encode any variables.
                else
                    resource = Uri.EscapeUriString(value);
            }
        }

        public S3Response Perform()
        {
            return Perform(null);
        }

        // This function throws an S3Exception in the event of a WebException status ProtocolError (HTTP error 404, 403, etc.).  Other errors (timeouts, etc.) will be thrown as WebException.
        public S3Response Perform(Stream dataToSend)
        {
            Uri uri = MakeURI();
            WebRequest request = WebRequest.Create(uri);	
#if DEBUG
            request.Timeout = 20000; // 20 seconds
#endif
            request.Method = RequestMethodToString(Method);
            if (request.Headers["x-amz-date"] == null)
                request.Headers.Add("x-amz-date", GetHttpDate());
            if (dataToSend != null)
            {
                request.ContentLength = dataToSend.Length;
                request.ContentType = "application/octet-stream"; // TODO: Be more sophisticated about setting this.
                if (EncryptionKey != null)
                {
                    byte[] saltBytes = new byte[4];
                    RandomNumberGenerator.Create().GetBytes(saltBytes);
                    uint salt = (uint)((saltBytes[3] << 24) | (saltBytes[2] << 16) | (saltBytes[1] << 8) | (saltBytes[0] << 0));
                    string keyHash = Crypto.CreateSaltedKeyHash(salt, EncryptionKey, false);
                    KeySalt = Crypto.CreateSalt(16);
                    request.Headers.Add("x-amz-meta-crypt", "aes_salted_" + keyHash);
                    request.Headers.Add("x-amz-meta-crypt-salt", KeySalt);
                }
            }
            string canonicalString = MakeCanonicalString(Resource, request);
            string encodedCanonical = Encode(SecretKey, canonicalString, false);

            request.Headers.Add("Authorization", "AWS " + AccessKey + ":" + encodedCanonical);

            
            if (Method == RequestMethod.rmPut && dataToSend != null)
            {
                Stream dest = request.GetRequestStream();
                if (EncryptionKey == null)
                    Spoonfeed(dataToSend, dest);
                else
                    WriteEncryptedData(dataToSend, dest);
            }

            try
            {
                return new S3Response(request.GetResponse(), Connection);
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    // Pull the string out here to make debugging easier because we won't be able to use the ResponseStream again.
                    string responseString = S3Connection.SlurpInputStream(ex.Response.GetResponseStream()); // We do it this way so we can inspect the response.
                    throw new S3Exception(responseString);
                }
                else
                    throw ex;
            }
        }

        protected void WriteEncryptedData(Stream src, Stream dest)
        {
            RijndaelManaged aes = new RijndaelManaged();

            string keyString = EncryptionKey + KeySalt;
            byte[] keyBytes = new byte[32]; // 256 bits - destination for the key
            Crypto.EVP_BytesToKey(new byte[0], Encoding.Default.GetBytes(keyString), 1, keyBytes, new byte[0]);
            aes.Key = keyBytes;
            aes.Mode = CipherMode.ECB;

            // Hash keySalt as initialization vector
            byte[] keySaltBytes = new byte[KeySalt.Length];
            for (int i = 0; i < KeySalt.Length; i++) keySaltBytes[i] = (byte)KeySalt[i];
            MD5 md5 = MD5.Create();
            aes.IV = md5.ComputeHash(keySaltBytes);

            CryptoStream cryptoStream = new CryptoStream(dest, new AESCTREncryptor(aes), CryptoStreamMode.Write);
            Spoonfeed(src, cryptoStream);
            cryptoStream.Close();
        }

        public static void Spoonfeed(Stream from, Stream to)
        {
            Debug.Assert(from.CanRead && to.CanWrite);
            byte[] buffer = new byte[4096];
            int bytesRead = 0;
            while ((bytesRead = from.Read(buffer, 0, buffer.Length)) != 0)
                to.Write(buffer, 0, bytesRead);
        }

        public string MakeCanonicalString(string resource, WebRequest request)
        {
            SortedList<string,string> headers = new SortedList<string,string>();
            foreach (string key in request.Headers)
            {
                headers.Add(key, request.Headers[key]);
            }
            if (!headers.ContainsKey("Content-Type"))
            {
                headers.Add("Content-Type", request.ContentType);
            }
            return MakeCanonicalString(request.Method, resource, headers, null);
        }
        public string MakeCanonicalString(string verb, string resource,
                                                  SortedList<string, string> headers, string expires)
        {
            StringBuilder buf = new StringBuilder();
            buf.Append(verb);
            buf.Append("\n");

            SortedList<string,string> interestingHeaders = new SortedList<string,string>();
            if (headers != null)
            {
                foreach (string key in headers.Keys)
                {
                    string lk = key.ToLower();
                    if (lk.Equals("content-type") ||
                         lk.Equals("content-md5") ||
                         lk.Equals("date") ||
                         lk.StartsWith("x-amz-"))
                    {
                        interestingHeaders.Add(lk, headers[key]);
                    }
                }
            }
            if (interestingHeaders.ContainsKey("x-amz-date"))
            {
                interestingHeaders.Add("date", "");
            }

            // if the expires is non-null, use that for the date field.  this
            // trumps the x-amz-date behavior.
            if (expires != null)
            {
                interestingHeaders.Add("date", expires);
            }

            // these headers require that we still put a new line after them,
            // even if they don't exist.
            {
                string[] newlineHeaders = { "content-type", "content-md5" };
                foreach (string header in newlineHeaders)
                {
                    if (interestingHeaders.IndexOfKey(header) == -1)
                    {
                        interestingHeaders.Add(header, "");
                    }
                }
            }

            // Finally, add all the interesting headers (i.e.: all that startwith x-amz- ;-))
            foreach (string key in interestingHeaders.Keys)
            {
                if (key.StartsWith("x-amz-"))
                {
                    buf.Append(key).Append(":").Append((interestingHeaders[key] as string).Trim());
                }
                else
                {
                    buf.Append(interestingHeaders[key]);
                }
                buf.Append("\n");
            }

            // Do not include the query string parameters
            int queryIndex = resource.IndexOf('?');
            string path = queryIndex == -1 ? resource : resource.Substring(0, queryIndex);
            if (IsUS)
                buf.Append(path);
            else
                buf.Append("/" + Bucket + path);

            Regex aclQueryStringRegEx = new Regex(".*[&?]acl($|=|&).*");
            Regex torrentQueryStringRegEx = new Regex(".*[&?]torrent($|=|&).*");
            if (aclQueryStringRegEx.IsMatch(resource))
            {
                buf.Append("?acl");
            }
            else if (torrentQueryStringRegEx.IsMatch(resource))
            {
                buf.Append("?torrent");
            }

            return buf.ToString();
        }
        public static string Encode(string awsSecretAccessKey, string canonicalString, bool urlEncode)
        {
            Encoding ae = new UTF8Encoding();
            HMACSHA1 signature = new HMACSHA1(ae.GetBytes(awsSecretAccessKey));
            string b64 = Convert.ToBase64String(signature.ComputeHash(ae.GetBytes(canonicalString.ToCharArray())));

            if (urlEncode)
                return HttpUtility.UrlEncode(b64);
            else
                return b64;
        }
        public static string GetHttpDate()
        {
            // Setting the Culture will ensure we get a proper HTTP Date.
            string date = System.DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss ", System.Globalization.CultureInfo.InvariantCulture) + "GMT";
            return date;
        }
        private Uri MakeURI()
        {
            Uri baseUri = new Uri("http://" + Host);
            return new Uri(baseUri, Resource);
        }
    }
}
