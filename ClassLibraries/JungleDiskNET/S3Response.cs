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

namespace JungleDisk
{
    /// <summary>
    /// Encapsulates the WebResponse and handles decryption of the stream based on keys maintained by S3Connection.
    /// </summary>
    public class S3Response
    {
        protected WebResponse webResponse;
        protected S3Connection connection;
        internal S3Response(WebResponse response, S3Connection connection)
        {
            this.webResponse = response;
            this.connection = connection;
        }
        protected bool IsEncrypted
        {
            get { return webResponse.Headers["x-amz-meta-crypt"] != null; }
        }
        public void Close()
        {
            webResponse.Close();
        }
        public Stream GetResponseStream()
        {
            if (!IsEncrypted)
                return webResponse.GetResponseStream();

            // We must decrypt the stream.
            string cryptType = webResponse.Headers["x-amz-meta-crypt"];
            string keySalt = webResponse.Headers["x-amz-meta-crypt-salt"];
            if (!cryptType.StartsWith("aes_salted_"))
                throw new Exception("AES encryption expected");

            // Get key from hash:
            string keyHash = cryptType.Substring(11);
            string details = webResponse.ResponseUri.AbsolutePath;
            string key = connection.GetKeyFromHash(keyHash, details);
            if (key == null)
                throw new CryptographicException("Unable to find key for: " + details);

            RijndaelManaged aes = new RijndaelManaged();
            
            // 
            string keyString = key + keySalt;
            byte[] keyBytes = new byte[32]; // 256 bits - destination for the key
            Crypto.EVP_BytesToKey(new byte[0], Encoding.Default.GetBytes(keyString), 1, keyBytes, new byte[0]); 
            aes.Key = keyBytes;
            aes.Mode = CipherMode.ECB;

            // Hash keySalt as initialization vector
            byte[] keySaltBytes = new byte[keySalt.Length];
            for (int i = 0; i < keySalt.Length; i++) keySaltBytes[i] = (byte)keySalt[i];
            MD5 md5 = MD5.Create();
            aes.IV = md5.ComputeHash(keySaltBytes);

            CryptoStream stream = new CryptoStream(webResponse.GetResponseStream(), new AESCTREncryptor(aes), CryptoStreamMode.Read);
            return stream;
        }

    }
}
