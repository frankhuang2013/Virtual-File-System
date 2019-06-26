using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Security.Cryptography;
using System.IO;

namespace CodeService
{
    class Cryption
    {
        public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 128;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.Zeros;

                aes.Key = key;
                aes.IV = iv;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    return PerformCryptography(data, encryptor);
                }
            }
        }

        public static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 128;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.Zeros;

                aes.Key = key;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    return PerformCryptography(data, decryptor);
                }
            }
        }

        private static byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
        {
            using (var ms = new MemoryStream())
            using (var cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write))
            {
                cryptoStream.Write(data, 0, data.Length);
                cryptoStream.FlushFinalBlock();

                return ms.ToArray();
            }
        }
    }

    class Hash
    {
        public static byte[] HashMD5(byte[] plainText)
        {
            byte[] hashed;
            using (MD5 md5Hash = MD5.Create())
            {
                hashed = md5Hash.ComputeHash(plainText);
            }
            return hashed;
        }

        public static bool VerifyMD5(byte[] plainText, byte[] hashedText)
        {
            bool result = true;
            byte[] hashed;
            using (MD5 md5Hash = MD5.Create())
            {
                hashed = md5Hash.ComputeHash(plainText);
            }
            for (int i = 0; i < hashedText.Length; i++)
            {
                if (hashedText[i] != hashed[i])
                {
                    result = false;
                    break;
                }
            }
            return result;
        }
    }

    class Debug
    {
        public static void printBytesHex(byte[] data)
        {
            foreach (byte b in data)
            {
                Console.Write("{0:X2} ", b);
            }
            Console.WriteLine("");
        }

        public static void printBytesDec(byte[] data)
        {
            if (data == null)
            {
                Console.WriteLine("NULL");
                return;
            }
            foreach (byte b in data)
            {
                Console.Write("{0:D} ", b);
            }
            Console.WriteLine("");
        }
    }
}