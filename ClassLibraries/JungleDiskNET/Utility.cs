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
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace JungleDisk
{
    public class DirectoryItem
    {
        public string Name;
        public string Marker;
        public string ParentMarker;
        public int Size;
        public bool IsDirectory;
        public string Attributes;
        public string PointerKey; // Amazon S3 pointer key
        public string FileKey;    // Amazon S3 file object key

        public DirectoryItem(string name, string marker, string parentMarker, bool isDirectory, int size, string attributes, string pointerKey, string fileKey)
        {
            this.Name = name;
            this.Marker = marker;
            this.ParentMarker = parentMarker;
            this.IsDirectory = isDirectory;
            this.Size = size;
            this.Attributes = attributes;
            this.PointerKey = pointerKey;
            this.FileKey = fileKey;
        }
        internal static int Compare(DirectoryItem a, DirectoryItem b)
        {
            return a.Name.CompareTo(b.Name);
        }
    }
    public class DirectoryContents
    {
        public Dictionary<string, DirectoryItem> Directories = new Dictionary<string, DirectoryItem>();
        public Dictionary<string, DirectoryItem> Files = new Dictionary<string, DirectoryItem>();
        public List<DirectoryItem> SortedDirectories { get { return SortedDirectoryItems(Directories); } }
        public List<DirectoryItem> SortedFiles { get { return SortedDirectoryItems(Files); } }

        internal static List<DirectoryItem> SortedDirectoryItems(Dictionary<string, DirectoryItem> dict)
        {
            List<DirectoryItem> items = new List<DirectoryItem>();
            foreach (DirectoryItem item in dict.Values)
                items.Add(item);
            items.Sort(DirectoryItem.Compare);
            return items;
        }

        public DirectoryItem ItemFor(string name)
        {
            if (Directories.ContainsKey(name))
                return Directories[name];
            else if (Files.ContainsKey(name))
                return Files[name];
            else
                return null;
        }
        public void Add(DirectoryItem item)
        {
            if (item.IsDirectory)
                Directories[item.Name] = item;
            else
                Files[item.Name] = item;
        }
    }

    public class Crypto
    {
        public static string CreateSalt(int length)
        {
            byte[] randBytes = new byte[length];
            RandomNumberGenerator.Create().GetBytes(randBytes);
            byte[] md5 = (MD5.Create()).ComputeHash(randBytes);
            int index = 0;
            char[] charArray = new char[md5.Length * 2];
            foreach (byte b in md5)
            {
                charArray[index++] = (b >> 4).ToString("x")[0];
                charArray[index++] = (b & 0xf).ToString("x")[0];
            }
            return new string(charArray);
        }
        // Used by CreateSaltedKeyHash:
        [DllImport("ws2_32.dll", SetLastError = false)]
        static extern uint htonl(uint n);

        public static uint SwapBytes(uint src)
        {
            return ((src >> 24) & 0xff) | ((src >> 8) & 0xff00) | ((src << 8) & 0xff0000) | ((src << 24) & 0xff000000);
        }
        public static string CreateSaltedKeyHash(uint salt, string key, bool swapSaltBytes)
        {
            uint saltBytes = htonl(salt);
            if (swapSaltBytes)
                saltBytes = SwapBytes(saltBytes);
            byte[] keyBytes = new byte[1024 + 4];
            keyBytes[0] = (byte)(saltBytes >> 24);
            keyBytes[1] = (byte)(saltBytes >> 16);
            keyBytes[2] = (byte)(saltBytes >> 8);
            keyBytes[3] = (byte)saltBytes;
            Array.Copy(Encoding.Default.GetBytes(key), 0, keyBytes, 4, key.Length);

            string output = salt.ToString("x8"); // %08x
            byte[] md5 = (MD5.Create()).ComputeHash(keyBytes, 0, 4 + key.Length);
            output += HexString(md5);
            return output;
        }

        public static string HexString(byte[] bytes)
        {
            string output = "";
            foreach (byte b in bytes)
                output += b.ToString("x2");
            return output;
        }
        public static void EVP_BytesToKey(byte[] salt, byte[] data, int count, byte[] key, byte[] iv)
        {
            int addmd = 0;
            byte[] inBuffer = new byte[data.Length + 16 + salt.Length];
            byte[] md_buf = null;
            int i;
            int keypos = 0;
            int ivpos = 0;

            int nkey = key.Length;
            int niv = iv.Length;

            while (true)
            {
                MD5 md = MD5CryptoServiceProvider.Create();
                int inPos = 0;
                if (addmd++ > 0)
                {
                    md_buf.CopyTo(inBuffer, 0);
                    inPos += md_buf.Length;
                }
                data.CopyTo(inBuffer, inPos);
                inPos += data.Length;
                salt.CopyTo(inBuffer, inPos);
                inPos += salt.Length;
                md_buf = md.ComputeHash(inBuffer, 0, inPos);
                i = 0;
                if (nkey > 0)
                {
                    while (true)
                    {
                        if (nkey == 0)
                            break;
                        if (i == md_buf.Length)
                            break;
                        key[keypos++] = md_buf[i];
                        nkey--;
                        i++;
                    }
                }
                if (niv > 0 && i != md_buf.Length)
                {
                    while (true)
                    {
                        if (niv == 0)
                            break;
                        if (i == md_buf.Length)
                            break;
                        iv[ivpos++] = md_buf[i];
                        niv--;
                        i++;
                    }
                }
                if (nkey == 0 && niv == 0)
                    break;
            }
        }

        public static byte[] IvecFromMarker(string marker)
        {
            byte[] ivec = new byte[16];
            for (int i = 0; i < marker.Length - 1 && i / 2 < 16; i += 2)
            {
                ivec[i / 2] = (byte)((HexToDec(marker[i]) << 4) + HexToDec(marker[i + 1]));
            }
            return ivec;
        }

        protected static byte HexToDec(char hex)
        {
            if (hex >= 'a' && hex <= 'f')
                return (byte)(10 + hex - 'a');
            if (hex >= '0' && hex <= '9')
                return (byte)(hex - '0');
            throw new Exception("Invalid hex character");
        }

        protected static char B64DECODECHAR(char c)
        {
            return ((char)(
                (c >= '0' && c <= '9') ? c + 4 :
                (c >= 'a' && c <= 'z') ? c - 71 :
                (c >= 'A' && c <= 'Z') ? c - 65 :
                (c == '+' || c == '[') ? 62 :
                (c == '/' || c == ']') ? 63 : 0));
        }

        public static byte[] Base64Decode(string inputBuffer)
        {
            int i, j;
            int inputBufferLen = inputBuffer.Length;
            //determine the output size by looking at the last few bytes
            if (inputBufferLen < 4)
                return new byte[0];

            int retLen = inputBufferLen / 4 * 3;
            //check the last two bytes for the term character
            for (i = 1; i <= 2; i++)
                if (inputBuffer[inputBufferLen - i] == '=' || inputBuffer[inputBufferLen - i] == '_')
                    retLen--;

            byte[] outputBuffer = new byte[retLen];

            for (i = 0, j = 0; j < retLen; i += 4, j += 3)
            {
                outputBuffer[j] = (byte)(B64DECODECHAR(inputBuffer[i]) << 2 | B64DECODECHAR(inputBuffer[i + 1]) >> 4);
                if (retLen - j > 1)
                    outputBuffer[j + 1] = (byte)((B64DECODECHAR(inputBuffer[i + 1]) & 0x0F) << 4 | B64DECODECHAR(inputBuffer[i + 2]) >> 2);
                if (retLen - j > 2)
                    outputBuffer[j + 2] = (byte)((B64DECODECHAR(inputBuffer[i + 2]) & 0x3) << 6 | B64DECODECHAR(inputBuffer[i + 3]));
            }
            return outputBuffer;
        }
        public static string Base64Encode(byte[] inputBuffer, char pad, char c62, char c63)
        {
	        int retLen = inputBuffer.Length / 3 * 4;
            int inputCount = inputBuffer.Length;
	        if (inputCount % 3 != 0) //if it's not divis by 3
		        retLen += 4;
            char[] retArray = new char[retLen];
            byte[] tripTemp = new byte[] { 0, 0, 0 };
	        int i,j;
	        int inbytes;
	        for ( i = 0, j = 0; i < inputCount ; i += 3, j += 4)
	        {
		        inbytes = Math.Min(3, inputCount - i);
		        for (int k = 0 ; k < 3 ; k++)
		        {
			        if (k < inbytes)
				        tripTemp[k] = inputBuffer[i + k];
			        else
				        tripTemp[k] = 0;
		        }
		        retArray[j + 0] = (char)(tripTemp[0] >> 2);
		        retArray[j + 1] =  (char)(((tripTemp[0] & 3) << 4) | (tripTemp[1] >> 4));
		        retArray[j + 2] = (char)((tripTemp[1] & 0x0F) << 2 | (tripTemp[2] >> 6));
		        retArray[j + 3] = (char)(tripTemp[2] & 0x3F);
        			 
	        }
	        int lastChar = retLen;
	        if (inputCount % 3 == 1)
		        lastChar -= 2;
	        else if (inputCount % 3 == 2)
		        lastChar -= 1;
	        for (i = 0 ; i < retLen; i++)
	        {
		        retArray[i] = (char)((i >= lastChar) ? pad : (retArray[i] <= 25) ? retArray[i] + 65 : (retArray[i] <= 51) ? retArray[i] + 71 : (retArray[i] <= 61) ? retArray[i] + 48 - 52 : (retArray[i] == 62) ? c62: c63);
	        }
            return new string(retArray, 0, retLen);
        }
    }

    /// <summary>
    /// Implements an ICryptoTransform for AES in CTR mode (not supported by .Net libraries).
    /// </summary>
    public class AESCTREncryptor : ICryptoTransform
    {
        protected RijndaelManaged aes;
        protected byte[] evec;
        protected byte[] ivec;
        protected int blockIndex = 0;
        protected int blockOffset = 0;
        public AESCTREncryptor(RijndaelManaged aes)
        {
            this.aes = aes;
            evec = new byte[aes.IV.Length];
            ivec = aes.IV;
            blockIndex = 0;
            blockOffset = 0;
        }
        public bool CanReuseTransform { get { return false; } }
        public bool CanTransformMultipleBlocks { get { return true; } }
        public int InputBlockSize { get { return 1024; } }
        public int OutputBlockSize { get { return 1024; } }
        public void Dispose() { }

        public int TransformBlock(byte[] inputBuffer,
                                    int inputOffset,
                                    int inputCount,
                                    byte[] outputBuffer,
                                    int outputOffset)
        {
            byte[] output = AESCTREncrypt(aes.CreateEncryptor(), inputBuffer, inputCount, ivec, evec, ref blockOffset, ref blockIndex);
            Array.Copy(output, 0, outputBuffer, outputOffset, output.Length);
            return output.Length;
        }
        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            byte[] output = AESCTREncrypt(aes.CreateEncryptor(), inputBuffer, inputCount, ivec, evec, ref blockOffset, ref blockIndex);
            return output;
        }

        static byte[] AESCTREncrypt(ICryptoTransform aes, byte[] buffer, int bufferLen, byte[] ivec, byte[] evec, ref int blockOffset, ref int blockIndex)
        {
            byte[] ret = new byte[bufferLen];
            for (int i = 0; i < bufferLen; i++)
            {
                if (blockOffset == 0) //calculate the encrypted block
                {
                    //increment the ivec as if it were a 128-bit big endian number
                    byte[] newIvec = (byte[])ivec.Clone();
                    long val = BitConverter.ToInt64(ivec, 8);
                    val = IPAddress.HostToNetworkOrder(IPAddress.NetworkToHostOrder(val) + blockIndex);
                    byte[] valarray = BitConverter.GetBytes(val);
                    valarray.CopyTo(newIvec, 8);
                    aes.TransformBlock(newIvec, 0, newIvec.Length, evec, 0);
                    blockIndex++;
                }
                ret[i] = (byte)(buffer[i] ^ evec[blockOffset]);
                blockOffset = (blockOffset + 1) % aes.OutputBlockSize;
            }
            return ret;
        }
    }
}
