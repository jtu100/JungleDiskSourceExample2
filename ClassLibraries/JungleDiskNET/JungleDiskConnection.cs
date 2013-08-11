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
using System.Xml;
using System.Threading;

namespace JungleDisk
{
    public enum BucketType { Compatibility, Legacy, Advanced };

    public class JungleDiskBucket
    {
        public BucketType TypeOfBucket; /// 
        public string DisplayName;      /// Human-friendly bucket descriptor.
        public string BucketPath;       /// Path to this bucket.  For advanced buckets, this is equivalent to S3Bucket/DisplayName.
        public string S3Bucket;         /// Name of the Amazon S3 bucket.
        public JungleDiskBucket(BucketType type, string s3Bucket, string path, string displayName)
        {
            TypeOfBucket = type;
            S3Bucket = s3Bucket;
            BucketPath = path;
            DisplayName = displayName;
        }
        public override string ToString()
        {
            return string.Format("{0} ({1})", DisplayName, TypeOfBucket);
        }
        // Returns the name of the actual JD2.0 S3 bucket for the given access key.
        public static string S3BucketFromAccessKey(string accessKey)
        {
            string accessKeyHash = Crypto.HexString((MD5.Create()).ComputeHash(Encoding.Default.GetBytes(accessKey)));
            return "jd2-" + accessKeyHash + "-us";
        }
    }

    public class JungleDiskConnection
    {
        public delegate string PasswordRequiredDelegate(string description);

        protected S3Connection s3;

        // Key File data (0.key)
        protected bool encryptFilenames = false;
        protected bool encryptNewFiles = false;
        protected string encryptionKey = "";
        protected string filenameKey = "";
        public PasswordRequiredDelegate passwordRequiredDelegate = null;
        public bool EncryptFilenames { get { return encryptFilenames; } }
        public bool EncryptNewFiles { get { return encryptNewFiles; } }
		public string AccessKey { get { return s3.accessKey; } }
		public string SecretKey { get { return s3.secretKey; } }

        public JungleDiskConnection(string accessKey, string secretKey)
        {
            s3 = new S3Connection(accessKey, secretKey);
            s3.passwordRequiredDelegate = PasswordRequiredFromS3;
        }

        internal string PasswordRequiredFromS3(string details)
        {
            if (passwordRequiredDelegate != null)
                if(details.EndsWith(s3.pathPrefix + "0.key"))
                    return passwordRequiredDelegate("Bucket Password");
                else
                    return passwordRequiredDelegate(details);
            else
                return null;
        }

        public void AddKey(string key)
        {
            s3.Keys.Add(key);
        }
        public bool BucketExists(JungleDiskBucket bucket)
        {
            bool s3BucketExists;
            return BucketExists(bucket, out s3BucketExists);
        }
        public bool BucketExists(JungleDiskBucket bucket, out bool s3BucketExists)
        {
            s3BucketExists = true;
            try
            {
                // Check that the S3 bucket exists at all before we bother with SetBucket().
                // Because SetBucket() tries to fetch objects from the bucket, if the S3 bucket does not exist it could create a negative cache entry for this bucket
                // and cause a delay until it is 'found' again.
                if (!s3.BucketExists(bucket.S3Bucket))
                {
                    s3BucketExists = false;
                    return false;
                }
                SetBucket(bucket);
            }
            catch (CryptographicException)
            {
                return true;
            }
            catch (Exception)
            {
            }

            try
            {
                // We do this expecting them to throw:
                s3.GetObjectAsString("0.dir");
                return true;
            }
            catch (S3Exception ex)
            {
                if (ex.Code != "NoSuchKey" && ex.Code != "NoSuchBucket")
                    throw ex;
            }
            catch (CryptographicException)
            {
                return true;
            }

            try
            {
                s3.GetObjectAsString("0.key");
                return true;
            }
            catch (S3Exception ex)
            {
                if (ex.Code != "NoSuchKey" && ex.Code != "NoSuchBucket")
                    throw ex;
            }
            catch (CryptographicException)
            {
                return true;
            }
            return false;
        }
        // Creates the S3 bucket which will contain the JD2.0 buckets.
        public void PrepareBucket()
        {
            string s3BucketName = JungleDiskBucket.S3BucketFromAccessKey(AccessKey);
            s3.CreateBucket(s3BucketName);
        }
        public void CreateBucket(JungleDiskBucket bucket, string bucketPassword, bool newEncryptFilenames)
        {
            // Essentially we just create the 0.dir file (and 0.key if necessary).  If 0.dir already exists, throw.

            // SetBucket(bucket); -- Don't call SetBucket() here; it will be called by BucketExists().
            bool s3BucketExists;
            if (BucketExists(bucket, out s3BucketExists))
                throw new Exception("Bucket already exists");

            if (!s3BucketExists)
            {
                s3.CreateBucket(bucket.S3Bucket);

                // Make sure the bucket is visible before we continue:
                for (int retries = 0; retries < 20; retries++)
                {
                    if (s3.BucketExists(bucket.S3Bucket))
                        break;
                    Thread.Sleep(5000); //need to wait some period of time for the bucket to be visible?
                }
				SetBucket(bucket); // Call it now because if the base S3 bucket did not exist in BucketExists, SetBucket() did not get called.
            }

			if (bucketPassword != null)
            {
                // This will establish the 0.key file.
                ChangeBucketPassword(bucket, bucketPassword, newEncryptFilenames);
            }
            s3.PutObject("0.dir", new byte[] { }, bucketPassword);
        }

        public void ChangeBucketPassword(JungleDiskBucket bucket, string newPassword)
        {
            SetBucket(bucket);
            ChangeBucketPassword(bucket, newPassword, EncryptFilenames);
        }
        protected void ChangeBucketPassword(JungleDiskBucket bucket, string newPassword, bool newEncryptFilenames)
        {
            SetBucket(bucket);
            string keyFileRaw = null;
            try
            {
                keyFileRaw = s3.GetObjectAsString("0.key");

                XmlNode node;
                XmlDocument doc = new XmlDocument(); // Parse the XML to make sure the key file is valid.  If not, an exception will be thrown.
                doc.LoadXml(keyFileRaw);

                // Note: encryptfilenames cannot be changed after bucket creation, but we will perform a sanity check here anyway.
                node = doc.SelectSingleNode("//encryptfilenames");
                if ((node.InnerText == "1" ? true : false) != newEncryptFilenames)
                    throw new Exception("ChangeBucketPassword: newEncryptFilenames, "+newEncryptFilenames+" != existing value, " + node.InnerText);

                node = doc.SelectSingleNode("//encryptnewfiles");
                node.InnerText = (newPassword != null) ? "1" : "0";

                keyFileRaw = doc.InnerXml;
            }
            catch (S3Exception ex)
            {
                if (ex.Code == "NoSuchKey")
                {
                    // There is no 0.key file.  We must create one.
                    keyFileRaw = CreateKeyFile((newPassword != null), newEncryptFilenames);

                    // We might like to just ReadKeyFile() to update the flags later on, but since S3 doesn't guarantee that it will be immediately available, we'll just set them by hand:
                    encryptNewFiles = (newPassword != null);
                    encryptFilenames = newEncryptFilenames;
                    encryptionKey = newPassword;
                }
                else
                    throw ex;
            }
            s3.PutObject("0.key", Encoding.Default.GetBytes(keyFileRaw), newPassword);
            AddKey(newPassword);
        }
        // Generate an encryption for use in the KeyFile.
        protected string GenerateEncryptionKey()
        {
            byte[] masterKey = new byte[32];
            RandomNumberGenerator.Create().GetBytes(masterKey);
            return Crypto.Base64Encode(masterKey, '_', '[', ']');
        }

        protected string CreateKeyFile(bool encryptNewFiles, bool encryptFilenames)
        {
            // Presently does not support adding legacy keys.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml("<keyfile></keyfile>");
            XmlNode keyFile = doc.SelectSingleNode("/keyfile");

            string newEncryptionKey = GenerateEncryptionKey();

            keyFile.AppendChild(doc.CreateElement("encryptnewfiles"));
            keyFile.LastChild.AppendChild(doc.CreateTextNode(encryptNewFiles ? "1" : "0"));
            keyFile.AppendChild(doc.CreateElement("encryptfilenames"));
            keyFile.LastChild.AppendChild(doc.CreateTextNode(encryptFilenames ? "1" : "0"));
            keyFile.AppendChild(doc.CreateElement("encryptionkey"));
            keyFile.LastChild.AppendChild(doc.CreateTextNode(newEncryptionKey));
            keyFile.AppendChild(doc.CreateElement("decryptionkeys"));
            // here we would append the value elements, and inside them the keys.

            return doc.InnerXml;
        }

        protected void ReadKeyFile()
        {
            try
            {
                string keyFileRaw = s3.GetObjectAsString("0.key");
                XmlDocument doc = new XmlDocument();
                try
                {
                    doc.LoadXml(keyFileRaw);
                }
                catch (XmlException ex)
                {
                    throw new CryptographicException("Unable to load key file.  Invalid bucket password.", ex);
                }
                encryptFilenames = doc.SelectSingleNode("//encryptfilenames").InnerText == "1";
                encryptNewFiles = doc.SelectSingleNode("//encryptnewfiles").InnerText == "1";
                encryptionKey = doc.SelectSingleNode("//encryptionkey").InnerText;
                s3.Keys.Add(encryptionKey);

                byte[] keyBytes = new byte[32];
                Crypto.EVP_BytesToKey(new byte[0], Encoding.Default.GetBytes(encryptionKey), 1, keyBytes, new byte[0]);
                filenameKey = Encoding.Default.GetString(keyBytes);
            }
            catch (S3Exception ex)
            {
                if (ex.Code != "NoSuchKey")
                    throw ex;
                // If 0.key was not found, then no encryption.
                encryptFilenames = false;
                encryptNewFiles = false;
                encryptionKey = "";
                filenameKey = "";
            }
        }

        protected void AddSubBuckets(string s3Bucket, List<JungleDiskBucket> output)
        {
            string marker = "";
            string[] pair = s3Bucket.Split(new char[] { '/' });
            s3Bucket = pair[0];
            string subBucket = pair.Length > 1 ? pair[1] : "";
            s3.SetBucket(s3Bucket, subBucket, s3Bucket.EndsWith("-us"));
            S3Connection.ReceivePrefixDelegate prefixDelegate = delegate(string prefix)
            {
                string name = prefix.Substring(0, prefix.Length - 1);
                output.Add(new JungleDiskBucket(BucketType.Advanced, s3Bucket, s3Bucket + "/" + name, name));
            };
            s3.GetObjectList(s3.pathPrefix, ref marker, 0, "/", null, prefixDelegate);
        }
        public enum BucketFilter
        {
            AllBuckets,
            LegacyOnly,
            AdvancedOnly,
            CompatOnly
        }
        public List<JungleDiskBucket> GetBucketList(BucketFilter filter)
        {
            string accessKeyHash = Crypto.HexString((MD5.Create()).ComputeHash(Encoding.Default.GetBytes(s3.accessKey)));
            string legacyAccessKeyRegexString = accessKeyHash.Replace("0", "0?"); // Some legacy buckets weren't padded out properly.
            Regex advancedBucketRegex = new Regex("jd2-"+accessKeyHash+"-(us|eu)");//"jd2-[0-9a-f]{32}-(us|eu)");
            Regex legacyBucketRegex = new Regex(legacyAccessKeyRegexString + "-(.+)"); //"[0-9a-f]{16,32}-(.*)");
            List<JungleDiskBucket> bucketList = new List<JungleDiskBucket>();
            List<string> rawBucketList = s3.GetBucketList();
            foreach (string bucketName in rawBucketList)
                if (advancedBucketRegex.IsMatch(bucketName))
                {
                    if (filter == BucketFilter.AllBuckets || filter == BucketFilter.AdvancedOnly)
                        AddSubBuckets(bucketName, bucketList);
                }
                else if(legacyBucketRegex.IsMatch(bucketName)) // Legacy bucket
                {
                    if (filter == BucketFilter.AllBuckets || filter == BucketFilter.LegacyOnly)
                        bucketList.Add(new JungleDiskBucket(BucketType.Legacy, bucketName, bucketName, bucketName.Substring(bucketName.IndexOf('-')+1)));
                }
                else // Compatibility
                {
                    if (filter == BucketFilter.AllBuckets || filter == BucketFilter.CompatOnly)
                        bucketList.Add(new JungleDiskBucket(BucketType.Compatibility, bucketName, bucketName, bucketName));
                }
            return bucketList;
        }

        // Important: Caller must close the returned stream!
        public Stream ReadFile(JungleDiskBucket bucket, string path)
        {
            SetBucket(bucket);
            string objPath = "FILES/" + GetMarkerForPath(bucket, path) + "/0";
            return s3.GetObject(objPath);
        }

        public void WriteFile(JungleDiskBucket bucket, string path, Stream file)
        {
            string directory = path.Remove(path.LastIndexOf('/'));
            string filepart = path.Substring(path.LastIndexOf('/') + 1);
            SetBucket(bucket);

            // Setup the file pointer object
            string parentMarker = GetMarkerForPath(bucket, directory);
            string selfMarker = GetNewMarker();
            int blockSize = 0; // for now
            string attributes = "";
            string[] parts = new string[] { parentMarker, selfMarker, "file", FilterFileNameWrite(filepart, selfMarker), file.Length.ToString(), blockSize.ToString(), attributes }; 
            string filePointer = string.Join("/", parts);
            s3.PutObject(filePointer, new byte[0], null); // empty object

            // Setup the file object itself
            try
            {
                parts = new string[] { "FILES", selfMarker, 0.ToString() };
                string fileObject = string.Join("/", parts);
                s3.PutObject(fileObject, file, encryptNewFiles ? encryptionKey : null);
            }
            catch (Exception ex)
            {
                // If we failed to create the actual file object, delete the pointer we created.
                s3.DeleteObject(filePointer);
                throw ex;
            }
        }

        public void DeleteFile(JungleDiskBucket bucket, string path)
        {
            DeleteFile(bucket, GetDirectoryItemForPath(bucket, path));
        }
        public void DeleteFile(JungleDiskBucket bucket, DirectoryItem item)
        {
            s3.DeleteObject(item.PointerKey);
            s3.DeleteObject(item.FileKey);
        }

        protected void SetBucket(JungleDiskBucket bucket)
        {
            if (bucket.TypeOfBucket != BucketType.Advanced)
                throw new NotSupportedException("Only Advanced buckets are presently supported.");
            if(s3.SetBucket(bucket.S3Bucket, bucket.DisplayName, bucket.S3Bucket.EndsWith("-us")))
                ReadKeyFile();
        }

        /// <summary>
        /// Generates a 256-bit (32 byte) random hex string.
        /// </summary>
        /// <returns>32 character random hex string.</returns>
        protected string GetNewMarker()
        {
            byte[] markerBytes = new byte[16];
            RandomNumberGenerator.Create().GetBytes(markerBytes);
            int index = 0;
            char[] charArray = new char[32];
            foreach (byte b in markerBytes)
            {
                charArray[index++] = (b >> 4).ToString("x")[0];
                charArray[index++] = (b & 0xf).ToString("x")[0];
            }
            return new string(charArray);
        }

        // Returns null if the path is the root path.
        protected DirectoryItem GetDirectoryItemForPath(JungleDiskBucket bucket, string path)
        {
            DirectoryItem item = null;
            string[] pathParts = path.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length == 0)
                return null;
            DirectoryContents contents = GetDirectoryListing(bucket, "");
            foreach (string pathPart in pathParts)
            {
                item = contents.ItemFor(pathPart);
                if (item.IsDirectory == false)
                    return item;
                contents = GetDirectoryListingForMarker(bucket, item.Marker);
            }
            return item;
        } 
        protected string GetMarkerForPath(JungleDiskBucket bucket, string path)
        {
            DirectoryItem item = GetDirectoryItemForPath(bucket, path);
            if (item == null)
                return "ROOT";
            else
                return item.Marker;
        }

        public DirectoryContents GetDirectoryListing(JungleDiskBucket bucket, string path)
        {
            string parentMarker = "";
            path = path.Trim(new char[] { '/', '\\' });
            if (path.Length == 0)
                parentMarker = "ROOT";
            else
                parentMarker = GetMarkerForPath(bucket, path);
            return GetDirectoryListingForMarker(bucket, parentMarker);
        }
        protected DirectoryContents GetDirectoryListingForMarker(JungleDiskBucket bucket, string parentMarker)
        {
            DirectoryContents output = new DirectoryContents();
            string resource = parentMarker + "/";
            SetBucket(bucket);
            string marker = ""; // Different kind of marker for continuing listings.

            List<string> markers = new List<string>();
            Regex filePointerRegex = new Regex("^([a-f0-9]{32})/file/(.+?)/(\\d+)/(\\d+)/(.*)$"); // Does not contain parent marker since it has been stripped as part of the prefix. ([a-f0-9]{32}|ROOT)/
            Regex dirPointerRegex = new Regex("^([a-f0-9]{32})/dir/(.+?)(/(.+))?$");
            S3Connection.ReceiveContentsDelegate markerDelegate = delegate(string key)
            {
                Match m;
                m = filePointerRegex.Match(key);
                if (m.Success)
                {
                    string selfMarker = m.Groups[1].Value;
                    string name = FilterFileNameRead(m.Groups[2].Value, selfMarker);
                    int size = Convert.ToInt32(m.Groups[3].Value);
                    string attributes = m.Groups[5].Value;
                    output.Add(new DirectoryItem(name, selfMarker, parentMarker, false, size, attributes, resource + key, "FILES/" + selfMarker + "/0"));
                }
                m = dirPointerRegex.Match(key);
                if (m.Success)
                {
                    string selfMarker = m.Groups[1].Value;
                    string name = FilterFileNameRead(m.Groups[2].Value, selfMarker);
                    string attributes = m.Groups[3].Success ? m.Groups[4].Value : ""; // this may not work
                    output.Add(new DirectoryItem(name, selfMarker, parentMarker, true, 0, attributes, resource + key, null));
                }
            };
            s3.GetObjectList(resource, ref marker, 0, null, markerDelegate, null);
            return output;
        }


        protected string FilterFileNameRead(string name, string marker)
        {
            if (!encryptFilenames)
                return name;

            byte[] rawName = Crypto.Base64Decode(name); // Convert.FromBase64String(name);

            RijndaelManaged aes = new RijndaelManaged();
            aes.IV = Crypto.IvecFromMarker(marker);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = Encoding.Default.GetBytes(filenameKey);

            MemoryStream memoryStream = new MemoryStream(rawName);
            CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            StreamReader streamReader = new StreamReader(cryptoStream);
            string output = streamReader.ReadToEnd();
			int idx = output.IndexOf('\0');
			if (idx >= 0)
				output = output.Remove(idx);
            return output;
        }
        protected string FilterFileNameWrite(string name, string marker)
        {
            if (!encryptFilenames)
                return name;

            RijndaelManaged aes = new RijndaelManaged();
            aes.IV = Crypto.IvecFromMarker(marker);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = Encoding.Default.GetBytes(filenameKey);

            // Round out the encoding block size:
            const int aesBlockSize = 16;
            int encryptLength = name.Length;
            if ((encryptLength % aesBlockSize) != 0)
                encryptLength += aesBlockSize - (encryptLength % aesBlockSize);
            byte[] encryptMe = new byte[encryptLength];
            Array.Clear(encryptMe, 0, encryptLength); // Probably not necessary
            Array.Copy(Encoding.Default.GetBytes(name), encryptMe, name.Length);

            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cryptoStream.Write(encryptMe, 0, encryptMe.Length);
            cryptoStream.Flush();
            byte[] output = new byte[memoryStream.Length];
            memoryStream.Position = 0; // Rewind to the beginning so we can read out of it.
            int bytesRead = memoryStream.Read(output, 0, output.Length);
            return Crypto.Base64Encode(output, '_', '[', ']');
        }
    }
}
