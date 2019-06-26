using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using CodeService;
using ErrorCata = Errors.ErrorCatagory;

namespace PFSKernel
{
    class PFSService
    {
        private const string DEFAULT_DISK_NAME = "Untitled Disk";

        internal static readonly byte[] VERIFICATION_CODE = new byte[2] { 98, 86 };
        internal static readonly byte[] FS_VERSION_CODE = new byte[3] { 0, 0, 1 };
        internal static readonly byte[] TERMINAL_BLOCK = new byte[9] { 25, 15, 21, 17, 9, 1, 15, 4, 9 };
        //internal static readonly byte[] FS_OPTIONS = new byte[2] { 0, 0 };

        internal static readonly byte[] SC_HEAD_VERIF_CODE = new byte[6] { 19, 25, 20, 16, 9, 2 };
        internal static readonly byte[] SC_TAIL_VERIF_CODE = new byte[6] { 19, 95, 3, 10, 20, 13 };
        internal static readonly int SC_FILE_LENGTH = 156;


        // header locations
        internal const int H_VERIFICATION_ST = 0;
        internal const int H_VERSION_ST = 2;
        internal const int H_TYPE_ST = 5;
        internal const int H_FLAGS_ST = 6;
        internal const int H_SVC_ST = 8;
        internal const int H_SIZE_USED_ST = 24;
        internal const int H_SIZE_CAP_ST = 32;
        internal const int H_DISKNAME_ST = 40;
        internal const int H_LAST_USER_ST = 296;
        internal const int B_ST = 300;

        public enum BlockSize { BS1K, BS2K, BS4K, BS8K, BS16K, BS32K, BS64K, BS128K, BS256K, BS512K, BS1M, BS2M, BS4M, BS8M };

        internal static bool ValidName(string s)
        {
            char[] invalidChar = { '/', '\\', '\"', '*', '?', ':', '<', '>', '|' };
            if (s.IndexOfAny(invalidChar) == -1)
            {
                if (s.Trim().Length == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        internal static void ChangeBit32(ref uint b, int n, bool set)
        {
            if (n < 1 || n > 32)
            {
                return;
            }
            uint tmp;
            if (set)
            {
                tmp = (uint)(Math.Pow(2, (n - 1)));
                b = b | tmp;
            }
            else
            {
                tmp = (uint)(4294967295 - (Math.Pow(2, (n - 1))));
                b = b & tmp;
            }
            return;
        }

        internal static void ChangeBit8(ref byte b, int n, bool set)
        {
            if (n < 1 || n > 8)
            {
                return;
            }
            byte tmp;
            if (set)
            {
                tmp = (byte)(Math.Pow(2, (n - 1)));
                b = (byte)(b | tmp);
            }
            else
            {
                tmp = (byte)(255 - (Math.Pow(2, (n - 1))));
                b = (byte)(b & tmp);
            }
            return;
        }

        internal static bool IsKthBitSet(uint n, int k)
        {
            if (k > 32 || k < 1)
                return false;
            if ((n & (1 << (k - 1))) != 0)
                return true;
            else
                return false;
        }

        internal static byte[] NumToBytes(ulong n, int startByteIndex, int byteCount)
        {
            if (startByteIndex < 0 || startByteIndex > 8 || startByteIndex + byteCount > 8)
            {
                return null;
            }

            byte[] tmp = BitConverter.GetBytes(n);
            byte[] result = new byte[byteCount];

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(tmp);
            }
            for (int i = 0; i < byteCount; i++)
            {
                result[i] = tmp[startByteIndex + i];
            }
            
            return result;
        }

        internal static ulong BytesToNum(byte[] n, int startByteIndex, int byteCount)
        {
            if (startByteIndex < 0 || startByteIndex > n.Length - 1 || startByteIndex + byteCount > n.Length)
            {
                return ulong.MaxValue;
            }
            if (byteCount != 8 && byteCount != 4 && byteCount != 2 && byteCount != 1)
            {
                return ulong.MaxValue;
            }

            byte[] tmp = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                tmp[i] = n[i + startByteIndex];
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(tmp);
            }

            if (byteCount == 8) return BitConverter.ToUInt64(tmp, 0);
            if (byteCount == 4) return BitConverter.ToUInt32(tmp, 0);
            if (byteCount == 2) return BitConverter.ToUInt16(tmp, 0);
            else return ulong.MaxValue;
        }

        internal static string ByteToHuman(ulong B, int DecimalPlaces)
        {
            string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

            //if (DecimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            //if (B < 0) { return "-" + SizeSuffix(-B); }
            if (B == 0) { return string.Format("{0:n" + DecimalPlaces + "} B", 0); }

            if (DecimalPlaces < 0) DecimalPlaces = 2; 

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(B, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)B / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, DecimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + DecimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
        
        internal static string SimplifyPath(string AbsPath)
        {
            if (AbsPath[0] != '/')
            {
                return "";
            }

            string[] pathComp = SplitPath(AbsPath);
            Stack<string> dots = new Stack<string>();
            string result = "/";

            for (int i = 0; i < pathComp.Length; i++)
            {
                if ((pathComp[i] == ".") || ((pathComp[i] == "..") && (dots.Count == 0)))
                {
                    continue;
                }
                else if (pathComp[i] == "..")
                {
                    if (dots.Peek() != "..")
                    {
                        dots.Pop();
                    }
                    else
                    {
                        dots.Push("..");
                    }
                }
                else
                {
                    dots.Push(pathComp[i]);
                }
            }

            string[] pathCompRe = dots.ToArray();
            for (int i = pathCompRe.Length - 1; i >= 0; i--)
            {
                result += pathCompRe[i] + "/";
            }
            if (result != "/")
            {
                result = result.Remove(result.Length - 1);
            }

            return result;
        }

        internal static string[] SplitPath(string path)
        {
            string[] seperator = new string[] { "/" };
            return path.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
        }

        internal static Tuple<string, string> IsolatePath(string path)
        {
            string parentPath;
            string filename;
            bool fromRoot = false;

            // check path
            if (path.Length == 0)
            {
                return null;
            }

            // remove leading and trailing sapces
            path = path.Trim();

            // check if the path is absolute
            if (path[0] == '/')
            {
                fromRoot = true;
            }

            // split the path into segaments
            string[] pathSeg = SplitPath(path);

            // process parent path and filename
            if (pathSeg.Length == 0)  // only root
            {
                parentPath = "";
                filename = "/";
            }
            else if (pathSeg.Length == 1)
            {
                if (fromRoot)
                {
                    parentPath = "/";
                    filename = pathSeg[0];
                }
                else
                {
                    parentPath = ".";
                    filename = pathSeg[0];
                }
            }
            else
            {
                if (fromRoot)
                {
                    parentPath = "/";
                }
                else
                {
                    parentPath = string.Empty;
                }
                for (int i = 0; i < pathSeg.Length - 1; i++)
                {
                    parentPath += pathSeg[i] + "/";
                }
                parentPath = parentPath.Remove(parentPath.Length - 1);
                filename = pathSeg[pathSeg.Length - 1];
            }

            return new Tuple<string, string>(parentPath, filename);
        }

        /* public static int CreateSecurityCertification(string fileName, ref byte[] SCK)
         * Create a brand new SC(Security Certification) file with the specified option.
         * SCK must be a byte array of length 48. If pass null, the method will automatically generate a random SCK.
         * Parameters:
         *   string fileName: The path and name to the SC file to be created on a physical disk.
         *   ref byte[] SCK: 48-byte Security Certification Kernel. Passing null will generate a random SCK.
         * Return: 0 if success, error code if failure.
         */
        public static int CreateSecurityCertification(string fileName, ref byte[] SCK)
        {
            // checking process
            if (SCK != null)
            {
                if (SCK.Length != 48)
                {
                    return ErrorCata.CreateSecurityCertification + 1;  // Error 2001: Failed to Create Security Certification: Invalid Security Certification Kernel.
                }
            }

            // create SC file
            FileStream scFile;
            try
            {
                scFile = File.Create(fileName);
            }
            catch (Exception)
            {
                return ErrorCata.CreateSecurityCertification + 2;  // Error 2002: Failed to Create Security Certification: Cannot create new file.
            }

            // prepare SCK
            if (SCK == null)
            {
                SCK = new byte[48];
                using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                {
                    aes.Key.CopyTo(SCK, 0);
                    aes.IV.CopyTo(SCK, 32);
                }
            }
            byte[] sc = new byte[144];
            byte[] gb1 = new byte[48];
            byte[] gb2 = new byte[48];
            using (RNGCryptoServiceProvider rng1 = new RNGCryptoServiceProvider())
            {
                rng1.GetBytes(gb1);
            }
            using (RNGCryptoServiceProvider rng2 = new RNGCryptoServiceProvider())
            {
                rng2.GetBytes(gb2);
            }
            byte[] sck = new byte[48];
            SCK.CopyTo(sck, 0);
            for (int i = 0; i < sck.Length; i++)
            {
                sck[i] = (byte)(~(sck[i] + 64));
            }
            for (int i = 0; i < sc.Length; i++)
            {
                if (i % 3 == 2)
                {
                    sc[i] = sck[i / 3];
                }
                else if (i % 3 == 1)
                {
                    sc[i] = gb2[i / 3];
                }
                else
                {
                    sc[i] = gb1[i / 3];
                }
            }

            // write to file
            try
            {
                scFile.Write(SC_HEAD_VERIF_CODE, 0, SC_HEAD_VERIF_CODE.Length);
                scFile.Write(sc, 0, sc.Length);
                scFile.Write(SC_TAIL_VERIF_CODE, 0, SC_TAIL_VERIF_CODE.Length);
            }
            catch (Exception)
            {
                return ErrorCata.CreateSecurityCertification + 3;  // Error 2003: Failed to Create Security Certification: Cannot write data.
            }

            scFile.Close();
            scFile.Dispose();

            return 0;
        }

        /* public static int GetSCKFromSC(string fileName, out byte[] SCK)
         * Get the Security Certification Kernel from a Security Certification file
         * Parameters:
         *   string fileName: The path and name to the SC file to be read on a physical disk.
         *   ref byte[] SCK: a 48-byte Security Certification Kernel to be written.
         * Return: 0 if success, error code if failure.
         */
        public static int GetSCKFromSC(string fileName, out byte[] SCK)
        {
            SCK = null;

            // open SC file
            FileStream scFile;
            try
            {
                scFile = File.OpenRead(fileName);
            }
            catch (Exception)
            {
                return ErrorCata.GetSCKFromSC + 1;  // Error 3001: Failed to get SCK from Security Certification: Cannot open SC file.
            }

            // check process
            if (scFile.Length != SC_FILE_LENGTH)
            {
                return ErrorCata.GetSCKFromSC + 2;  // Error 3002: Failed to get SCK from Security Certification: Invalid SC file.
            }
            byte[] sc = new byte[SC_FILE_LENGTH];
            try
            {
                scFile.Read(sc, 0, SC_FILE_LENGTH);
            }
            catch (Exception)
            {
                return ErrorCata.GetSCKFromSC + 3;  // Error 3003: Failed to get SCK from Security Certification: Cannot read SC file.
            }
            scFile.Close();
            scFile.Dispose();
            for (int i = 0; i < 6; i++)
            {
                if (sc[i] != SC_HEAD_VERIF_CODE[i])
                {
                    return ErrorCata.GetSCKFromSC + 2;  // Error 3002: Failed to get SCK from Security Certification: Invalid SC file.
                }
            }
            for (int i = 0; i < 6; i++)
            {
                if (sc[SC_FILE_LENGTH - 6 + i] != SC_TAIL_VERIF_CODE[i])
                {
                    return ErrorCata.GetSCKFromSC + 2;  // Error 3002: Failed to get SCK from Security Certification: Invalid SC file.
                }
            }

            // fill SCK
            SCK = new byte[48];
            for (int i = 6; i < SC_FILE_LENGTH - 6; i++)
            {
                if ((i - 6) % 3 == 2)
                {
                    SCK[(i - 6) / 3] = sc[i];
                }
            }
            for (int i = 0; i < 48; i++)
            {
                SCK[i] = (byte)((~SCK[i]) - 64);
            }

            return 0;
        }

        /* public static int CreateDisk(string diskFilename, string diskName, BlockSize blockSize, ulong capacity, byte[] SCK)
         * Create a brand new disk with the specified options. The existed file will be overwritten.
         * Parameters: 
         *   string diskFilename: the path and name to the disk file to be created on a physical disk.
         *   string diskName: the name of the disk.
         *   BlockSize blockSize: the size of one block. Use enum BlockSize, eg: BS4K.
         *   ulong capacity: the capacity of the disk in bytes.
         *   byte[] SCK: 48-byte Security Certification Kernel. Pass null if Encryption is not needed.
         * Returns: 0 if success, error code if failure.
         */
        public static int CreateDisk(string diskFilename, string diskName, BlockSize blockSize, ulong capacity, byte[] SCK)
        {
            // check process
            int blockSizeCode = (int)blockSize;
            if (blockSizeCode < 0 || blockSizeCode > 13)
            {
                return ErrorCata.CreateDisk + 1;  // Error 1001: Failed to create disk: Wrong FS version number.
            }
            if (capacity < Math.Pow(2, blockSizeCode) * 1024)
            {
                return ErrorCata.CreateDisk + 2;  // Error 1002: Failed to create disk: Disk capacity is lower than one data block.
            }
            if (capacity % (Math.Pow(2, blockSizeCode) * 1024) != 0)
            {
                return ErrorCata.CreateDisk + 3;  // Error 1003: Failed to create disk: Disk capacity doesn't make integer number of blocks.
            }
            if (capacity / (Math.Pow(2, blockSizeCode) * 1024) < 13)
            {
                return ErrorCata.CreateDisk + 4;  // Error 1004: Failed to create disk: Disk capacity doesn't make at least 13 blocks.
            }
            if (diskName.Length == 0)
            {
                diskName = DEFAULT_DISK_NAME;
            }
            else if (diskName.Length > 256)
            {
                return ErrorCata.CreateDisk + 5;  // Error 1005: Failed to create disk: Disk name length is greater than 256 Bytes.
            }
            if (!ValidName(diskName))
            {
                return ErrorCata.CreateDisk + 6;  // Error 1006: Failed to create disk: Disk name contains invalid character(s).
            }
            if (SCK != null && SCK.Length != 48)
            {
                return ErrorCata.CreateDisk + 7;  // Error 1007: Failed to create disk: Invalid Security Certification Kernel.
            }

            // create or overwrite the disk file
            FileStream disk;
            try
            {
                disk = File.Create(diskFilename);
            }
            catch (Exception)
            {
                return ErrorCata.CreateDisk + 8;  // Error 1008: Failed to create disk: Cannot create new file.
            }
            BinaryWriter writer = new BinaryWriter(disk);

            try
            {
                // write header: verification code
                disk.Write(VERIFICATION_CODE, 0, 2);

                // write header: FS Version code
                disk.Write(FS_VERSION_CODE, 0, 3);

                // write header: FS Type code
                disk.WriteByte(((byte)blockSize));

                // write header: FS status flags
                uint tmp = 0;
                if (SCK != null)
                {
                    ChangeBit32(ref tmp, 8, true);
                }
                writer.Write((ushort)tmp);

                // write header: SVC(Security Verification Code)
                byte[] SVC;
                if (SCK != null)
                {
                    SVC = Hash.HashMD5(SCK);  // get the hashed SVC from SCK
                }
                else
                {
                    // zero fill the SVC area
                    SVC = new byte[16];
                    Array.Clear(SVC, 0, SVC.Length);
                }
                disk.Write(SVC, 0, 16);

                // write header: size info
                ulong zero64 = 0;
                writer.Write(zero64);
                writer.Write(capacity);


                // write header, disk name
                byte[] diskname;
                if (diskName.Length != 256)
                {
                    diskname = Encoding.Default.GetBytes(diskName + "\0");
                }
                else
                {
                    diskname = Encoding.Default.GetBytes(diskName + "\0");
                }
                disk.Write(diskname, 0, diskname.Length);
                disk.Seek(H_LAST_USER_ST, SeekOrigin.Begin);


                // write header, last user id
                uint zero32 = 0;
                writer.Write(zero32);

                // calculate block number
                ulong blockNumber = capacity / (ulong)((Math.Pow(2, (int)blockSize) * 1024));
                byte unfilledBit = (byte)(blockNumber % 8);
                ulong bitmap_length = blockNumber / 8;
                if (unfilledBit != 0)
                {
                    bitmap_length++;
                }

                // write block bitmap
                byte bitmapTmp = 0;
                for (ulong i = 0; i < bitmap_length - 1; i++)
                {
                    disk.WriteByte(bitmapTmp);
                }
                if (unfilledBit == 0)
                {
                    disk.WriteByte(bitmapTmp);
                }
                else
                {
                    bitmapTmp = (byte)(Math.Pow(2, 8 - unfilledBit) - 1);
                    disk.WriteByte(bitmapTmp);
                }

                // write (reserve) file table
                ulong totalFileTableByte = FileTable.Size * blockNumber;
                disk.Seek((long)totalFileTableByte, SeekOrigin.Current);

                // write (reserve) 10 blocks
                ulong totalBlocksByte = 10 * ((ulong)Math.Pow(2, (int)blockSize) * 1024 + 8);
                disk.Seek((long)totalBlocksByte, SeekOrigin.Current);

                // write Terminal Block
                disk.Write(TERMINAL_BLOCK, 0, 9);

                // set bitmap
                disk.Seek(B_ST, SeekOrigin.Begin);
                byte bitmaptmp = bitmapTmp;
                if (bitmap_length > 1)
                {
                    bitmaptmp = 0;
                }
                ChangeBit8(ref bitmaptmp, 8, true);
                disk.WriteByte(bitmaptmp);

                // go to file table area
                disk.Seek((long)(B_ST + bitmap_length), SeekOrigin.Begin);

                // prepare the file table for root
                DateTime timeNow = DateTime.Now;
                FileTable root = new FileTable
                {
                    propertyDirectory = true,
                    propertySystem = true,
                    permissionUserRead = true,
                    permissionUserWrite = true,
                    permissionUserExec = true,
                    permissionGroupRead = true,
                    permissionGroupExec = true,
                    permissionOtherRead = true,
                    permissionOtherExec = true,
                    owneruid = 0,
                    ownergid = 1,
                    createTime = timeNow,
                    modifyTime = timeNow,
                    accessTime = timeNow,
                    fileSize = 512,
                    firstBlockID = 0
                };

                // write file table for root
                disk.Write(root.Combine(), 0, FileTable.Size);

                // write "dots" DirRec to the root directory data block
                FileRecord dot = new FileRecord
                {
                    filename = ".",
                    FID = 0
                };
                FileRecord dotdot = new FileRecord
                {
                    filename = "..",
                    FID = 0
                };
                disk.Seek((long)(B_ST + bitmap_length + totalFileTableByte), SeekOrigin.Begin);
                disk.Write(dot.Combine(), 0, FileRecord.Size);
                disk.Write(dotdot.Combine(), 0, FileRecord.Size);

                // modify disk size
                ulong newSize = (ulong)((Math.Pow(2, (int)blockSize) * 1024));
                disk.Seek(H_SIZE_USED_ST, SeekOrigin.Begin);
                writer.Write(newSize);
            }
            catch (Exception)
            {
                return ErrorCata.CreateDisk + 9;  // Error 1009: Failed to create disk: Cannot write data.
            }

            disk.Close();
            writer.Close();
            disk.Dispose();
            writer.Dispose();

            return 0;
        }
    }

    public class Disk
    {
        public int status;  // 0: Null, 1: operatable, 2: valid disk, 3: Loaded
        private string diskFilePath;
        private FileStream disk;
        private BinaryReader reader;
        private BinaryWriter writer;
        private byte[] SCK;
        public ulong BitMapByteNum;
        public ulong BlockNum;
        public ulong BlockSize;
        public ulong FT_ST;
        public ulong D_ST;
        public string currentPath;
        public ulong currentPathFID;
        public uint currentUserID;
        public uint currentGroupID;
        public string currentUserName;
        public string currentGroupName;
        public ushort defaultNewFilePermission = 64000;
        public ushort defaultNewDirectoryPermission = 64000;
        public string ErrorString;
        public enum Permission { Read = 4, Write = 2, Execute = 1};
        public enum FileType { File = 0, Directory = 32768, System = 16384, Encrypted = 8192};
        public enum FileTablePermission { u0 = 0, u1 = 8192 , u2 = 16384 , u3 = 24576 , u4 = 32768 , u5 = 40960 , u6 = 49152 , u7 = 57344 , g0 = 0 , g1 = 1024 , g2 = 2048 , g3 = 3072 , g4 = 4096 , g5 = 5120 , g6 = 6144 , g7 = 7168 , o0 = 0 , o1 = 128 , o2 = 256 , o3 = 384 , o4 = 512 , o5 = 640 , o6 = 768 , o7 = 896};

        private bool ValidateDisk()
        {
            // check if it's right time to call this method
            if (status < 1)
            {
                return false;
            }

            bool result = true;
            byte[] v = HeaderVerificationCode;
            byte[] t = HeaderTerminalCode;

            for (int i = 0; i < PFSService.VERIFICATION_CODE.Length; i++)
            {
                if (v[i] != PFSService.VERIFICATION_CODE[i])
                {
                    result = false;
                    break;
                }
            }
            for (int i = 0; i < PFSService.TERMINAL_BLOCK.Length; i++)
            {
                if (result == false && t[i] != PFSService.TERMINAL_BLOCK[i])
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        private bool VerifySecurity()
        {
            // check if it's right time to call this method
            if (status < 2)
            {
                return false;
            }

            if (!HeaderFlagEncryption)
            {
                return true;
            }

            byte[] SVC = HeaderSVC;

            return Hash.VerifyMD5(SCK, SVC);
        }

        private bool HasPermission(FileTable ft, Permission permission)
        {
            if (permission == Permission.Read)
            {
                if (ft.owneruid == currentUserID && ft.permissionUserRead == true)
                {
                    return true;
                }
                else if (ft.ownergid == currentGroupID && ft.permissionGroupRead == true)
                {
                    return true;
                }
                else if (ft.permissionOtherRead == true)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (permission == Permission.Write)
            {
                if (ft.owneruid == currentUserID && ft.permissionUserWrite == true)
                {
                    return true;
                }
                else if (ft.ownergid == currentGroupID && ft.permissionGroupWrite == true)
                {
                    return true;
                }
                else if (ft.permissionOtherWrite == true)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (permission == Permission.Execute)
            {
                if (ft.owneruid == currentUserID && ft.permissionUserExec == true)
                {
                    return true;
                }
                else if (ft.ownergid == currentGroupID && ft.permissionGroupExec == true)
                {
                    return true;
                }
                else if (ft.permissionOtherExec == true)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        
        public Disk()
        {
            status = 0;
            disk = null;
            reader = null;
            writer = null;
            ErrorString = "";
            SCK = new byte[48];
        }

        public int LoadDisk(string DiskFileName, string SCFileName, string UserName, string Password)
        {
            // check if it's right time to call this method
            if (status != 0 && status != 3)
            {
                return + 1;
            }

            // dispose everything if have been created
            if (disk != null)
            {
                disk.Dispose();
            }
            if (reader != null)
            {
                reader.Dispose();
            }
            if (writer != null)
            {
                writer.Dispose();
            }
            if (SCK != null)
            {
                SCK = null;
            }

            // open file stream and binary reader/writer
            try
            {
                disk = new FileStream(DiskFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (Exception)
            {
                return + 2;
            }
            reader = new BinaryReader(disk);
            writer = new BinaryWriter(disk);

            // set disk file path
            diskFilePath = DiskFileName;

            // process SCK
            if(SCFileName != null)
            {
                if (PFSService.GetSCKFromSC(SCFileName, out SCK) != 0)
                {
                    return +3;
                }
            }

            // set status to 1
            status = 1;

            // validate the disk file
            if (!ValidateDisk())
            {
                status = 0;
                return + 4;
            }

            // set status to 2
            status = 2;

            // check security
            if (!VerifySecurity())
            {
                status = 0;
                return + 5;
            }

            // set BitMapByteNum
            BlockSize = (ulong)(Math.Pow(2, HeaderBlockSize) * 1024);
            BlockNum = HeaderFSCapacity / BlockSize;
            BitMapByteNum = BlockNum / 8;
            if (BlockNum % 8 != 0)
            {
                BitMapByteNum++;
            }
            FT_ST = PFSService.B_ST + BitMapByteNum;
            D_ST = FT_ST + FileTable.Size * BlockNum;

            // initialize disk if not initialized
            if (!HeaderFlagInitialized)
            {
                // set current user id and group id
                currentUserID = 0;
                currentGroupID = 1;
                currentUserName = "ROOT";
                currentGroupName = "Administrators";

                //set current path and path FID
                currentPath = "/";
                currentPathFID = 0;

                // create system folder
                CreateEmptyFile("/", 0, "SYSTEM", 53248, 57344);
                // create user file
                CreateEmptyFile("/SYSTEM", 0, "USERS", 20480, 57344);
                // create group file
                CreateEmptyFile("/SYSTEM", 0, "GROUPS", 20480, 57344);
                // create users folder
                CreateEmptyFile("/", 0, "Users", 49152, 63104);

                // prepare new user and group structure
                User userRoot = new User();
                userRoot.UserID = 0;
                userRoot.GroupIDs = new uint[16] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                userRoot.HomeDirectoryFID = 0;
                userRoot.Username = "ROOT";
                userRoot.Description = "ROOT";
                userRoot.CreatedTime = userRoot.LastLoggedInTime = DateTime.Now;
                userRoot.Reserved = new byte[8];
                userRoot.PasswordHash = null;
                Group groupAdmin = new Group();
                groupAdmin.GroupID = 1;
                groupAdmin.GroupName = "Administrators";
                groupAdmin.Description = "Administrators";
                groupAdmin.CreatedTime = DateTime.Now;
                groupAdmin.Reserved = new byte[52];
                Group groupUser = new Group();
                groupUser.GroupID = 2;
                groupUser.GroupName = "Users";
                groupUser.Description = "Users";
                groupUser.CreatedTime = DateTime.Now;
                groupUser.Reserved = new byte[52];

                // add new user root
                FileTable tmpFT;
                GetFileTable("/SYSTEM/USERS", 0, out tmpFT);
                disk.Seek((long)(D_ST + tmpFT.firstBlockID * (BlockSize + 8)), SeekOrigin.Begin);
                byte[] userFileHead = new byte[512];
                PFSService.NumToBytes(65535ul, 0, 8).CopyTo(userFileHead, 0);
                disk.Write(userFileHead, 0, 512);
                disk.Write(userRoot.Combine(), 0, User.Size);
                tmpFT.fileSize = 1024;
                disk.Seek((long)(FT_ST + tmpFT.firstBlockID * FileTable.Size), SeekOrigin.Begin);
                disk.Write(tmpFT.Combine(), 0, FileTable.Size);

                // add new group Administrators
                GetFileTable("/SYSTEM/GROUPS", 0, out tmpFT);
                disk.Seek((long)(D_ST + tmpFT.firstBlockID * (BlockSize + 8)), SeekOrigin.Begin);
                byte[] groupFileHead = new byte[256];
                PFSService.NumToBytes(0ul, 0, 8).CopyTo(groupFileHead, 0);
                disk.Write(groupFileHead, 0, 256);
                disk.Write(groupAdmin.Combine(), 0, Group.Size);
                disk.Write(groupUser.Combine(), 0, Group.Size);
                tmpFT.fileSize = 768;
                disk.Seek((long)(FT_ST + tmpFT.firstBlockID * FileTable.Size), SeekOrigin.Begin);
                disk.Write(tmpFT.Combine(), 0, FileTable.Size);

                // set initialized flag to true
                HeaderFlagInitialized = true;
            }

            // log in process
            Dictionary<string, User> userDictionary;
            Dictionary<uint, Group> groupDictionary;
            int retCode = GetUsers(out userDictionary);
            if (retCode != 0)
            {
                status = 0;
                return retCode;
            }
            retCode = GetGroups(out groupDictionary);
            if (retCode != 0)
            {
                status = 0;
                return retCode;
            }
            if (!userDictionary.ContainsKey(UserName))
            {
                status = 0;
                return +6;  // user not exist
            }
            if (userDictionary[UserName].PasswordHash != null)
            {
                if (!CodeService.Hash.VerifyMD5(Encoding.Default.GetBytes(Password), userDictionary[UserName].PasswordHash))
                {
                    status = 0;
                    return +7;  // password wrong
                }
            }
            else
            {
                if (Password != null)
                {
                    status = 0;
                    return +7;  // password wrong
                }
            }

            // update last logged in time
            ChangeUserDetails(userDictionary[UserName].UserID, "", null, null, DateTime.Now);
            
            currentUserName = UserName;
            currentUserID = userDictionary[UserName].UserID;
            currentGroupID = userDictionary[UserName].GroupIDs[0];
            if (currentGroupID == 0)
            {
                currentGroupName = "";
            }
            else
            {
                if (groupDictionary.ContainsKey(currentGroupID))
                {
                    currentGroupName = groupDictionary[currentGroupID].GroupName;
                }
                else
                {
                    currentGroupName = "";
                    currentGroupID = 0;
                }
            }
            currentPathFID = userDictionary[UserName].HomeDirectoryFID;
            if (userDictionary[UserName].UserID == 0)
            {
                currentPath = "/";
            }
            else
            {
                currentPath = "/Users/" + UserName + "/";
            }
            
            // set status to 3
            status = 3;
            
            return 0;
        }

        public int GetUsers(out Dictionary<string, User> UserDictionary)
        {
            int retCode;
            UserDictionary = null;

            // get Users file table
            FileTable usersFT = new FileTable();
            retCode = GetFileTable("/SYSTEM/USERS", 0, out usersFT);
            if (retCode != 0)
            {
                return retCode;
            }


            try
            {
                // read files and get structures
                UserDictionary = new Dictionary<string, User>();
                byte[] tmpUserArr = new byte[User.Size];
                User tmpUser;
                disk.Seek((long)(D_ST + usersFT.firstBlockID * (BlockSize + 8) + 512), SeekOrigin.Begin);
                for (ulong remainSize = usersFT.fileSize - 512; remainSize > 0;)
                {
                    // read a user
                    disk.Read(tmpUserArr, 0, User.Size);
                    tmpUser = new User();
                    tmpUser.Isolate(tmpUserArr);
                    UserDictionary.Add(tmpUser.Username, tmpUser);

                    remainSize -= (ulong)User.Size;

                    // switch block
                    byte[] tmpNextBlockPtr = new byte[8];
                    if ((usersFT.fileSize - remainSize) % BlockSize == 0 && remainSize != 0)
                    {
                        disk.Read(tmpNextBlockPtr, 0, 8);
                        disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                    }
                }
            }
            catch (Exception)
            {
                return +1;
            }

            return 0;
        }

        public int GetGroups(out Dictionary<uint, Group> GroupDictionary)
        {
            int retCode;
            GroupDictionary = null;

            // get Groups file table
            FileTable groupsFT = new FileTable();
            retCode = GetFileTable("/SYSTEM/GROUPS", 0, out groupsFT);
            if (retCode != 0)
            {
                return retCode;
            }
            
            try
            {
                // read files and get structures
                GroupDictionary = new Dictionary<uint, Group>();
                byte[] tmpUserArr = new byte[Group.Size];
                Group tmpGroup;
                disk.Seek((long)(D_ST + groupsFT.firstBlockID * (BlockSize + 8) + 256), SeekOrigin.Begin);
                for (ulong remainSize = groupsFT.fileSize - 256; remainSize > 0;)
                {
                    // read a user
                    disk.Read(tmpUserArr, 0, Group.Size);
                    tmpGroup = new Group();
                    tmpGroup.Isolate(tmpUserArr);
                    GroupDictionary.Add(tmpGroup.GroupID, tmpGroup);

                    remainSize -= (ulong)Group.Size;

                    // switch block
                    byte[] tmpNextBlockPtr = new byte[8];
                    if ((groupsFT.fileSize - remainSize) % BlockSize == 0 && remainSize != 0)
                    {
                        disk.Read(tmpNextBlockPtr, 0, 8);
                        disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                    }
                }
            }
            catch (Exception)
            {
                return +1;
            }

            return 0;
        }

        private bool verifyPassword(string password, byte[] passwordHash)
        {
            if (currentUserID == 0)
            {
                return true;
            }
            if (password == null && passwordHash != null)  // no password
            {
                return false;
            }
            else if (!Hash.VerifyMD5(Encoding.Default.GetBytes(password), passwordHash))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public int ChangeUserDetails(uint UID, string password, string username, string description, DateTime? lastLogin)
        {
            // goto user file and locate to the user
            FileTable userFT;
            User tmpUser = new User();
            bool found = false;
            byte[] tmpUserArr = new byte[User.Size];
            GetFileTable("/SYSTEM/USERS", 0, out userFT);
            try
            {
                disk.Seek((long)(D_ST + userFT.firstBlockID * (BlockSize + 8)) + 512, SeekOrigin.Begin);
                for (ulong remainSize = userFT.fileSize - 512; remainSize > 0;)
                {
                    disk.Read(tmpUserArr, 0, User.Size);
                    tmpUser.Isolate(tmpUserArr);
                    remainSize -= (ulong)User.Size;
                    if (tmpUser.UserID == UID)
                    {
                        found = true;
                        disk.Seek(-User.Size, SeekOrigin.Current);
                        break;
                    }
                }
            }
            catch (Exception)
            {

                return +1;  // error when reading users file
            }

            if (!found)
            {
                return +2;  // user specified not found
            }

            // check user's password
            if (!verifyPassword(password, tmpUser.PasswordHash))
            {
                return +3;  // password not correct
            }

            // change details
            if (username != null)
            {
                tmpUser.Username = username;
            }
            if (description != null)
            {
                tmpUser.Description = description;
            }
            if (lastLogin != null)
            {
                tmpUser.LastLoggedInTime = (DateTime)lastLogin;
            }

            // write back to disk
            try
            {
                disk.Write(tmpUser.Combine(), 0, User.Size);
            }
            catch (Exception)
            {

                return +4;  // error when write back modified user structure
            }

            // update current user's info if uid is current user
            if (UID == currentUserID)
            {
                if (username != null)
                {
                    currentUserName = username;
                }
            }

            return 0;
        }

        public int AddUser(string username, string description, string password, bool admin)
        {
            int retCode;
            Dictionary<string, User> UserDictionary;

            // get Users file table
            FileTable usersFT = new FileTable();
            retCode = GetFileTable("/SYSTEM/USERS", 0, out usersFT);
            if (retCode != 0)
            {
                return retCode;
            }

            // get users
            retCode = GetUsers(out UserDictionary);
            if (retCode != 0)
            {
                return retCode;
            }

            // check name collision
            if (UserDictionary.ContainsKey(username))
            {
                return +1;  // name collision
            }

            // go to users file and get new user's id
            byte[] userFileHeader = new byte[512];
            disk.Seek((long)(D_ST + usersFT.firstBlockID * (BlockSize + 8)), SeekOrigin.Begin);
            disk.Read(userFileHeader, 0, 512);
            uint adminMaxID = (uint)PFSService.BytesToNum(userFileHeader, 0, 4);
            uint userMaxID = (uint)PFSService.BytesToNum(userFileHeader, 4, 4);

            // TODO switch to root

            // create user's home folder
            retCode = CreateEmptyFile("/Users/", 0, username, 49152, 63104);
            if(retCode != 0)
            {
                return retCode;
            }

            // get new home folder's FT
            FileTable newUserFT = new FileTable();
            retCode = GetFileTable("/Users/" + username, 0, out newUserFT);
            if (retCode != 0)
            {
                return retCode;
            }

            //TODO
            // prepare new user
            User newUser = new User();
            newUser.Username = username;
            newUser.GroupIDs = new uint[8];
            newUser.HomeDirectoryFID = newUserFT.firstBlockID;

            // to be deleted
            return 0;

        }

        

        // TODO: MODIFY ACCESSED TIME
        public int ChangeDirectory(string directoryPath)
        {
            int retCode;
            FileTable dirFT;

            // get file table
            retCode = GetFileTable(directoryPath, currentPathFID, out dirFT);
            if (retCode != 0)
            {
                return +retCode;
            }

            // check if it is a directory
            if (!dirFT.propertyDirectory)
            {
                return +1;  // not a directory 
            }

            // check permission
            if (!HasPermission(dirFT, Permission.Execute))
            {
                return +2;  // no permission
            }

            // set current path and fid
            if (directoryPath[0] == '/')  // if it is absolute path
            {
                currentPath = PFSService.SimplifyPath(directoryPath);
            }
            else  // if it is relative path
            {
                currentPath = currentPath = PFSService.SimplifyPath(currentPath + "/" + directoryPath);
            }
            if (currentPath[currentPath.Length - 1] != '/')
            {
                currentPath += "/";
            }
            currentPathFID = dirFT.firstBlockID;

            return 0;
        }

        public int ListFiles(FileTable ft, out List<Tuple<FileRecord, FileTable>> fileList)
        {
            FileRecord tmpFR;
            FileTable tmpFT;
            byte[] tmpFRArr = new byte[FileRecord.Size];
            byte[] tmpFTArr = new byte[FileTable.Size];
            fileList = new List<Tuple<FileRecord, FileTable>>();

            // go to destination folder's first block
            disk.Seek((long)(D_ST + ft.firstBlockID * (BlockSize + 8)), SeekOrigin.Begin);

            // loop through all the file records
            for (ulong sizeRemain = ft.fileSize; sizeRemain > 0;)
            {
                // create new FileRecord and FileTable space
                tmpFR = new FileRecord();
                tmpFT = new FileTable();

                // read next record
                disk.Read(tmpFRArr, 0, FileRecord.Size);
                tmpFR.Isolate(tmpFRArr);
                sizeRemain -= FileRecord.Size;

                // switch read pointer if one block is totally read
                if ((ft.fileSize - sizeRemain) % BlockSize == 0)
                {
                    byte[] tmpNextBlockPtr = new byte[8];
                    disk.Read(tmpNextBlockPtr, 0, 8);
                    disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                }

                // skip invalid record
                if (tmpFR.FID == 0 && tmpFR.filename != "." && tmpFR.filename != "..")
                {
                    ///sizeRemain += FileRecord.Size;
                    continue;
                }

                // get FT from FR
                long tmpPos = disk.Position;  // save position
                disk.Seek((long)(FT_ST + tmpFR.FID * FileTable.Size), SeekOrigin.Begin);
                disk.Read(tmpFTArr, 0, FileTable.Size);
                tmpFT.Isolate(tmpFTArr);
                disk.Seek(tmpPos, SeekOrigin.Begin);  // go back to temp position

                // add FT and FR to Tuple and then to List
                fileList.Add(new Tuple<FileRecord, FileTable>(tmpFR, tmpFT));
            }

            return 0;
        }

        public int GetFRCount(FileTable ft, out ulong FRCount)
        {
            FRCount = 0;

            // check if fr is a directory
            if (!ft.propertyDirectory)
            {
                return +1;
            }
            
            try
            {
                // iterate through the whole directory
                byte[] tmpFRArr = new byte[FileRecord.Size];
                FileRecord tmpFR = new FileRecord();
                disk.Seek((long)(D_ST + ft.firstBlockID * (BlockSize + 8)), SeekOrigin.Begin);
                for (ulong sizeRemain = ft.fileSize; sizeRemain > 0;)
                {
                    // read next record
                    disk.Read(tmpFRArr, 0, FileRecord.Size);
                    tmpFR.Isolate(tmpFRArr);
                    sizeRemain -= FileRecord.Size;

                    // switch read pointer if one block is totally read
                    if ((ft.fileSize - sizeRemain) % BlockSize == 0)
                    {
                        byte[] tmpNextBlockPtr = new byte[8];
                        disk.Read(tmpNextBlockPtr, 0, 8);
                        disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                    }

                    // skip invalid record
                    if (tmpFR.FID == 0 && tmpFR.filename != "." && tmpFR.filename != "..")
                    {
                        ///sizeRemain += FileRecord.Size;
                        continue;
                    }

                    // count + 1
                    FRCount++;
                }
            }
            catch (Exception)
            {
                FRCount = 0;
                return +2;  // disk error
            }

            return 0;
        }

        public int GetFID(string path, ulong startFID, out ulong resultFID)
        {
            ulong currFID;
            FileTable tmpFT = new FileTable();
            byte[] tmpFTArr = new byte[FileTable.Size];
            FileRecord tmpFR = new FileRecord();
            byte[] tmpFRArr = new byte[FileRecord.Size];
            string[] pathSeg;
            ulong tmpSizeRemain;
            List<Tuple<FileRecord, FileTable>> tmpFileList;
            int tmpRetCode;
            bool tmpFRFound;

            resultFID = ulong.MaxValue;

            // check path validity
            if (path.Length == 0)
            {
                return +1;
            }

            // set start FID
            if (path[0] == '/')
            {
                currFID = 0;
                path = path.Remove(0, 1);
            }
            else
            {
                currFID = startFID;
            }

            pathSeg = PFSService.SplitPath(path);

            // loop through each level of the path
            for (int i = 0; i < pathSeg.Length; i++)
            {
                // go and get current fid FT
                disk.Seek((long)(FT_ST + currFID * FileTable.Size), SeekOrigin.Begin);
                disk.Read(tmpFTArr, 0, FileTable.Size);
                tmpFT.Isolate(tmpFTArr);

                // check read permission
                if (!HasPermission(tmpFT, Permission.Read))
                {
                    return +2;
                }

                // set remain size to file size
                tmpSizeRemain = tmpFT.fileSize;

                tmpRetCode = ListFiles(tmpFT, out tmpFileList);
                if (tmpRetCode != 0)
                {
                    return +tmpRetCode;
                }

                tmpFRFound = false;
                for (int j = 0; j < tmpFileList.Count; j++)
                {
                    if (tmpFileList[j].Item1.filename == pathSeg[i])
                    {
                        // set current FID
                        currFID = tmpFileList[j].Item1.FID;
                        tmpFRFound = true;
                        break;
                    }
                }

                if (!tmpFRFound)
                {
                    return +3; // record not found
                }
            }

            resultFID = currFID;
            return 0;
        }

        public int GetFileTable(string path, ulong startFID, out FileTable resultFT)
        {
            int retCode;
            ulong pathFID;
            byte[] FTArr;
            resultFT = null;

            // get FID of the path
            retCode = GetFID(path, startFID, out pathFID);
            if (retCode != 0)
            {
                return +retCode;
            }

            // get FileTable
            FTArr = new byte[FileTable.Size];
            disk.Seek((long)(FT_ST + pathFID * FileTable.Size), SeekOrigin.Begin);
            disk.Read(FTArr, 0, FTArr.Length);
            resultFT = new FileTable();
            resultFT.Isolate(FTArr);

            return 0;
        }

        public int OpenNewBlocks(ulong numBlocks, out ulong[] newBlockIDs)
        {
            byte tmpByte;
            ulong blocksRemain = numBlocks;
            newBlockIDs = null;

            // check if enough blocks are available
            if (numBlocks > ((HeaderFSCapacity - HeaderFSSize) / BlockSize))
            {
                return +1;
            }

            newBlockIDs = new ulong[numBlocks];
            if (numBlocks == 0)
            {
                return 0;
            }

            // go to Bitmap start point
            disk.Seek(PFSService.B_ST, SeekOrigin.Begin);

            for (ulong i = 0; i < BitMapByteNum; i++)
            {
                tmpByte = (byte)disk.ReadByte();
                // skip to next byte if all 8 blocks are occupied
                if (tmpByte == 255)
                {
                    continue;
                }

                // check all bits one by one
                for (int j = 8; j >= 1; j--)
                {
                    // one unused block found
                    if (!PFSService.IsKthBitSet(tmpByte, j))
                    {
                        ulong newBlockID = i * 8 + 8 - (ulong)j;
                        PFSService.ChangeBit8(ref tmpByte, j, true);
                        newBlockIDs[numBlocks - blocksRemain] = newBlockID;
                        blocksRemain--;

                        if (blocksRemain == 0)
                        {
                            break;
                        }
                    }
                }

                // write modified byte back to file
                disk.Position -= 1;
                disk.WriteByte(tmpByte);

                if (blocksRemain == 0)
                {
                    break;
                }
            }

            // expand the disk file if too short
            ulong lastBlockID = newBlockIDs[numBlocks - 1];
            ulong neededLength = D_ST + (BlockSize + 8) * (lastBlockID + 1) + (ulong)PFSService.TERMINAL_BLOCK.Length;
            if (disk.Length < (long)neededLength)
            {
                disk.SetLength((long)neededLength);
                disk.Seek(-PFSService.TERMINAL_BLOCK.Length, SeekOrigin.End);
                disk.Write(PFSService.TERMINAL_BLOCK, 0, PFSService.TERMINAL_BLOCK.Length);  // write terminal code
            }

            // change the size of the disk
            HeaderFSSize += numBlocks * BlockSize;

            return 0;
        }

        public int RecycleBlocks(ulong[] BlockIDs)
        {
            byte tmpByte;

            // sort the Block ID array
            Array.Sort(BlockIDs);

            // put "same byte" blocks into one list in hashmap
            Dictionary<ulong, List<ulong>> types = new Dictionary<ulong, List<ulong>>();  // byteNum -> List(BlockIDs)
            for (int i = 0; i < BlockIDs.Length; i++)
            {
                // cannot recycle reserved block id 0
                if (BlockIDs[i] == 0)
                {
                    return +1;
                }

                if (types.ContainsKey(BlockIDs[i] / 8))
                {
                    types[BlockIDs[i] / 8].Add(BlockIDs[i]);
                }
                else
                {
                    types.Add(BlockIDs[i] / 8, new List<ulong>() { BlockIDs[i] });
                }
            }

            // iterate through the dictionary
            foreach (var tmpBlockID in types.Keys)
            {
                // read the byte from the bitmap
                disk.Seek((long)(PFSService.B_ST + tmpBlockID), SeekOrigin.Begin);
                tmpByte = (byte)disk.ReadByte();

                // iterate through the list
                for (int i = 0; i < types[tmpBlockID].Count; i++)
                {
                    PFSService.ChangeBit8(ref tmpByte, (int)(8 - types[tmpBlockID][i] % 8), false);
                }

                // write the byte to the disk
                disk.Seek(-1, SeekOrigin.Current);
                disk.WriteByte(tmpByte);
            }

            // change the size of the disk
            HeaderFSSize -= ((ulong)(BlockIDs.Length) * BlockSize);

            return 0;
        }

        public int InsertFileRecord(FileTable ft, FileRecord fr)
        {
            int retCode;

            // check permission
            if (!HasPermission(ft, Permission.Write) || !HasPermission(ft, Permission.Read))
            {
                return +1;  // no permission
            }

            // check whether ft is a directory
            if (!ft.propertyDirectory)
            {
                return +2;  // not a directory
            }

            // check if fr is dot or dotdot
            if (fr.filename == "." || fr.filename == "..")
            {
                return +3;  // cannot insert dots
            }

            // go to first block and skip dot and dotdot
            disk.Seek((long)(D_ST + ft.firstBlockID * (BlockSize + 8) + FileRecord.Size * 2), SeekOrigin.Begin);

            // find an unused space and insert FR
            ulong sizeRemain;
            byte[] tmpFRArr = new byte[FileRecord.Size];
            FileRecord tmpFR = new FileRecord();
            long sparePos = 0;
            for (sizeRemain = ft.fileSize - FileRecord.Size * 2; sizeRemain > 0;)
            {
                // read a FileRecord
                disk.Read(tmpFRArr, 0, FileRecord.Size);
                tmpFR.Isolate(tmpFRArr);
                sizeRemain -= FileRecord.Size;
                
                if (tmpFR.FID == 0)  // check if it is spare
                {
                    if (sparePos == 0)
                    {
                        sparePos = disk.Position - FileRecord.Size;
                    }
                }
                else  // check collision
                {
                    if (tmpFR.filename == fr.filename)
                    {
                        return +4;  // name collision
                    }
                }

                // change block if current block is fully read
                if (((ft.fileSize - sizeRemain) % BlockSize == 0) && (sizeRemain > 0))
                {
                    byte[] tmpNextBlockPtr = new byte[8];
                    disk.Read(tmpNextBlockPtr, 0, 8);
                    disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                }
            }

            // if a spare found, insert the FR directly
            if (sparePos != 0)
            {
                disk.Seek(sparePos, SeekOrigin.Begin);
                disk.Write(fr.Combine(), 0, FileRecord.Size);
            }
            else  // need new space for fr
            {
                if (ft.fileSize % BlockSize != 0)  // need not new block
                {
                    disk.Write(fr.Combine(), 0, FileRecord.Size);
                }
                else  // need a new block
                {
                    // accquire a new block
                    long tmpPos = disk.Position;  // save current position: then end of a block before nextBlockPtr
                    ulong[] newBlock;
                    retCode = OpenNewBlocks(1, out newBlock);
                    if (retCode != 0)
                    {
                        return retCode;
                    }

                    // write next block pointer
                    disk.Seek(tmpPos, SeekOrigin.Begin);
                    disk.Write(PFSService.NumToBytes(newBlock[0], 0, 8), 0, 8);

                    // go to the next block
                    disk.Seek((long)(D_ST + newBlock[0] * (BlockSize + 8)), SeekOrigin.Begin);

                    // write the new fr
                    disk.Write(fr.Combine(), 0, FileRecord.Size);
                }
            }

            // change the propertoes of parent file table
            ft.modifyTime = ft.accessTime = DateTime.Now;
            if (sparePos == 0)
            {
                ft.fileSize += FileRecord.Size;
            }

            // update parent file table
            disk.Seek((long)(FT_ST + ft.firstBlockID * FileTable.Size), SeekOrigin.Begin);
            disk.Write(ft.Combine(), 0, FileTable.Size);

            return 0;
        }

        public int CreateEmptyFile(string parentPath, ulong startFID, string FileName, ushort FileProperty, ushort FilePermission)
        {
            int retCode;
            FileTable parentFT;

            // get parent directory's FileTable
            retCode = GetFileTable(parentPath, startFID, out parentFT);
            if (retCode != 0)
            {
                return +retCode;
            }

            // check permission
            if (!HasPermission(parentFT, Permission.Write))
            {
                return +1;  // no permission
            }

            // check whether disk has at least 2 blocks
            if ((HeaderFSCapacity - HeaderFSSize) / BlockSize < 2)
            {
                return +2;  // no enough disk space
            }

            // prepare new file's FileRecord except FID
            FileRecord newFR = new FileRecord();
            newFR.filename = FileName;

            // prepare new file's File Table except first block pointer
            FileTable newFT = new FileTable();
            newFT.SetProperty(FileProperty);
            newFT.SetPermission(FilePermission);
            newFT.owneruid = currentUserID;
            newFT.ownergid = currentGroupID;
            newFT.createTime = newFT.modifyTime = newFT.accessTime = DateTime.Now;
            if (newFT.propertyDirectory)
            {
                newFT.fileSize = FileRecord.Size * 2;
            }
            else
            {
                newFT.fileSize = 0;
            }

            // Open up a new block
            ulong[] newBlockIDs;
            retCode = OpenNewBlocks(1, out newBlockIDs);
            if (retCode != 0)
            {
                return +retCode;
            }

            // assign the first block pointer to the FileTable and FileRecord
            newFR.FID = newFT.firstBlockID = newBlockIDs[0];

            // insert FileRecord to parent directory
            retCode = InsertFileRecord(parentFT, newFR);
            if (retCode != 0)
            {
                RecycleBlocks(newBlockIDs);
                return retCode;
            }

            // if directory, write dot and dotdot
            FileRecord dot = new FileRecord { FID = newFR.FID, filename = "." };
            FileRecord dotdot = new FileRecord { FID = parentFT.firstBlockID, filename = ".." };
            disk.Seek((long)(D_ST + newFT.firstBlockID * (BlockSize + 8)), SeekOrigin.Begin);
            disk.Write(dot.Combine(), 0, FileRecord.Size);
            disk.Write(dotdot.Combine(), 0, FileRecord.Size);

            // write FileTable to disk
            disk.Seek((long)(FT_ST + newFR.FID * FileTable.Size), SeekOrigin.Begin);
            disk.Write(newFT.Combine(), 0, FileTable.Size);

            return 0;
        }

        public int DeleteFile(string parentPath, ulong startFID, string FileName)
        {
            int retCode;
            FileTable parentFT;
            FileTable fileFT;

            // get parent directory's FileTable
            retCode = GetFileTable(parentPath, startFID, out parentFT);
            if (retCode != 0)
            {
                return +retCode;
            }

            // check permission
            if (!HasPermission(parentFT, Permission.Write))
            {
                return +1;  // no permission
            }
            
            try
            {
                // get file's FR
                disk.Seek((long)(D_ST + parentFT.firstBlockID * (BlockSize + 8) + FileRecord.Size * 2), SeekOrigin.Begin);  // go to first block and skip dot and dotdot
                ulong sizeRemain;
                byte[] tmpFRArr = new byte[FileRecord.Size];
                FileRecord tmpFR = new FileRecord();
                long frPos = 0;
                for (sizeRemain = parentFT.fileSize - FileRecord.Size * 2; sizeRemain > 0;)
                {
                    // read a FileRecord
                    disk.Read(tmpFRArr, 0, FileRecord.Size);
                    tmpFR.Isolate(tmpFRArr);
                    sizeRemain -= FileRecord.Size;

                    // skip invalid record
                    if (tmpFR.FID == 0 && tmpFR.filename != "." && tmpFR.filename != "..")
                    {
                        // change block if current block is fully read
                        if (((parentFT.fileSize - sizeRemain) % BlockSize == 0) && (sizeRemain > 0))
                        {
                            byte[] tmpNextBlockPtr = new byte[8];
                            disk.Read(tmpNextBlockPtr, 0, 8);
                            disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                        }
                        continue;
                    }

                    // check if it is the file name
                    if (tmpFR.filename == FileName)
                    {
                        frPos = disk.Position - 8;
                        break;
                    }

                    // change block if current block is fully read
                    if (((parentFT.fileSize - sizeRemain) % BlockSize == 0) && (sizeRemain > 0))
                    {
                        byte[] tmpNextBlockPtr = new byte[8];
                        disk.Read(tmpNextBlockPtr, 0, 8);
                        disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                    }
                }

                // get file's FT
                disk.Seek((long)(FT_ST + tmpFR.FID * FileTable.Size), SeekOrigin.Begin);
                fileFT = new FileTable();
                byte[] fileFTArr = new byte[FileTable.Size];
                disk.Read(fileFTArr, 0, FileTable.Size);
                fileFT.Isolate(fileFTArr);

                // check if it is a file
                if (fileFT.propertyDirectory)
                {
                    return +2;  // file is a directory
                }

                // recycle blocks one by one
                ulong numBlock = fileFT.fileSize / BlockSize;
                if ((fileFT.fileSize % BlockSize != 0) || (fileFT.fileSize == 0))
                {
                    numBlock++;
                }
                ulong[] BIDs = new ulong[numBlock];
                BIDs[0] = fileFT.firstBlockID;
                disk.Seek((long)(D_ST + BIDs[0] * (BlockSize + 8) + BlockSize), SeekOrigin.Begin);
                byte[] tmpPtr = new byte[8];
                for (int i = 1; i < BIDs.Length; i++)
                {
                    byte[] tmpNextBlockPtr = new byte[8];
                    disk.Read(tmpNextBlockPtr, 0, 8);
                    BIDs[i] = PFSService.BytesToNum(tmpNextBlockPtr, 0, 8);
                    disk.Seek((long)(D_ST + BIDs[i] * (BlockSize + 8) + BlockSize), SeekOrigin.Begin);
                }
                retCode = RecycleBlocks(BIDs);
                if (retCode != 0)
                {
                    return +retCode;
                }

                // invalidate fr
                disk.Seek(frPos, SeekOrigin.Begin);
                writer.Write(0ul);

                // modify parent ft
                //parentFT.fileSize -= FileRecord.Size;
                parentFT.modifyTime = parentFT.accessTime = DateTime.Now;
                disk.Seek((long)(FT_ST + parentFT.firstBlockID * FileTable.Size), SeekOrigin.Begin);
                disk.Write(parentFT.Combine(), 0, FileTable.Size);
            }
            catch (Exception)
            {
                return +3;  // Error when writing file
            }

            return 0;
        }

        public int DeleteDirectory(string parentPath, ulong startFID, string directoryName, bool recursive)
        {
            int retCode;
            FileTable parentFT;
            FileTable fileFT;

            // get parent directory's FileTable
            retCode = GetFileTable(parentPath, startFID, out parentFT);
            if (retCode != 0)
            {
                return +retCode;
            }

            // check permission
            if (!HasPermission(parentFT, Permission.Write))
            {
                return +1;  // no permission
            }

            try
            {
                // get file's FR and record that FR's disk position
                disk.Seek((long)(D_ST + parentFT.firstBlockID * (BlockSize + 8) + FileRecord.Size * 2), SeekOrigin.Begin);  // go to first block and skip dot and dotdot
                ulong sizeRemain;
                byte[] tmpFRArr = new byte[FileRecord.Size];
                FileRecord tmpFR = new FileRecord();
                long frPos = 0;
                for (sizeRemain = parentFT.fileSize - FileRecord.Size * 2; sizeRemain > 0;)
                {
                    // read a FileRecord
                    disk.Read(tmpFRArr, 0, FileRecord.Size);
                    tmpFR.Isolate(tmpFRArr);
                    sizeRemain -= FileRecord.Size;

                    // skip invalid record
                    if (tmpFR.FID == 0 && tmpFR.filename != "." && tmpFR.filename != "..")
                    {
                        // change block if current block is fully read
                        if (((parentFT.fileSize - sizeRemain) % BlockSize == 0) && (sizeRemain > 0))
                        {
                            byte[] tmpNextBlockPtr = new byte[8];
                            disk.Read(tmpNextBlockPtr, 0, 8);
                            disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                        }
                        continue;
                    }

                    // check if it is the file name
                    if (tmpFR.filename == directoryName)
                    {
                        frPos = disk.Position - 8;
                        break;
                    }

                    // change block if current block is fully read
                    if (((parentFT.fileSize - sizeRemain) % BlockSize == 0) && (sizeRemain > 0))
                    {
                        byte[] tmpNextBlockPtr = new byte[8];
                        disk.Read(tmpNextBlockPtr, 0, 8);
                        disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                    }
                }
                if (frPos == 0)
                {
                    return +2;  // directory not found 
                }

                // get file's FT
                disk.Seek((long)(FT_ST + tmpFR.FID * FileTable.Size), SeekOrigin.Begin);
                fileFT = new FileTable();
                byte[] fileFTArr = new byte[FileTable.Size];
                disk.Read(fileFTArr, 0, FileTable.Size);
                fileFT.Isolate(fileFTArr);
                if (!fileFT.propertyDirectory)
                {
                    return +3;  // directory is a file
                }

                ulong frCount;
                retCode = GetFRCount(fileFT, out frCount);
                if (retCode != 0)
                {
                    return +4;  // Fail to get directory contents
                }
                if ((!recursive) || frCount == 2)
                {
                    if (frCount > 2)
                    {
                        return +5;  // directory not empty
                    }

                    // recycle blocks one by one
                    ulong numBlock = fileFT.fileSize / BlockSize;
                    if ((fileFT.fileSize % BlockSize != 0) || (fileFT.fileSize == 0))
                    {
                        numBlock++;
                    }
                    ulong[] BIDs = new ulong[numBlock];
                    BIDs[0] = fileFT.firstBlockID;
                    disk.Seek((long)(D_ST + BIDs[0] * (BlockSize + 8) + BlockSize), SeekOrigin.Begin);
                    byte[] tmpPtr = new byte[8];
                    for (int i = 1; i < BIDs.Length; i++)
                    {
                        byte[] tmpNextBlockPtr = new byte[8];
                        disk.Read(tmpNextBlockPtr, 0, 8);
                        BIDs[i] = PFSService.BytesToNum(tmpNextBlockPtr, 0, 8);
                        disk.Seek((long)(D_ST + BIDs[i] * (BlockSize + 8) + BlockSize), SeekOrigin.Begin);
                    }
                    retCode = RecycleBlocks(BIDs);
                    if (retCode != 0)
                    {
                        return +retCode;
                    }
                }
                else  // delete directory recursively
                {
                    // get all the files and directories in the directory
                    List<Tuple<FileRecord, FileTable>> list;
                    retCode = ListFiles(fileFT, out list);

                    // process each item in the list recursively
                    for (int i = 2; i < list.Count; i++)
                    {
                        if (!list[i].Item2.propertyDirectory)
                        {
                            retCode = DeleteFile(".", fileFT.firstBlockID, list[i].Item1.filename);
                        }
                        else
                        {
                            retCode = DeleteDirectory(".", fileFT.firstBlockID, list[i].Item1.filename, true);
                        }
                        if (retCode != 0)
                        {
                            return +retCode;
                        }
                    }

                    // delete current directory
                    retCode = DeleteDirectory("..", fileFT.firstBlockID, directoryName, false);
                    if (retCode != 0)
                    {
                        return +retCode;
                    }
                }

                // invalidate fr
                disk.Seek(frPos, SeekOrigin.Begin);
                writer.Write(0ul);

                // modify parent ft
                //parentFT.fileSize -= FileRecord.Size;
                parentFT.modifyTime = parentFT.accessTime = DateTime.Now;
                disk.Seek((long)(FT_ST + parentFT.firstBlockID * FileTable.Size), SeekOrigin.Begin);
                disk.Write(parentFT.Combine(), 0, FileTable.Size);
            }
            catch (Exception)
            {
                return +6;  // Error when writing file
            }

            return 0;
        }

        public int MoveFile(string parentPath, ulong startFID, string FileName, string newParentPath, ulong newStartFID, string newFileName)
        {
            int retCode;
            FileTable parentFT;
            FileTable newParentFT;
            FileTable fileFT;
            FileRecord movedFR = null;

            // get parent directory's FileTable
            retCode = GetFileTable(parentPath, startFID, out parentFT);
            if (retCode != 0)
            {
                return +retCode;
            }

            // check permission
            if (!HasPermission(parentFT, Permission.Write))
            {
                return +1;  // no permission
            }

            try
            {
                // get file's FR
                disk.Seek((long)(D_ST + parentFT.firstBlockID * (BlockSize + 8) + FileRecord.Size * 2), SeekOrigin.Begin);  // go to first block and skip dot and dotdot
                ulong sizeRemain;
                byte[] tmpFRArr = new byte[FileRecord.Size];
                FileRecord tmpFR = new FileRecord();
                long frPos = 0;
                for (sizeRemain = parentFT.fileSize - FileRecord.Size * 2; sizeRemain > 0;)
                {
                    // read a FileRecord
                    disk.Read(tmpFRArr, 0, FileRecord.Size);
                    tmpFR.Isolate(tmpFRArr);
                    sizeRemain -= FileRecord.Size;

                    // skip invalid record
                    if (tmpFR.FID == 0 && tmpFR.filename != "." && tmpFR.filename != "..")
                    {
                        // change block if current block is fully read
                        if (((parentFT.fileSize - sizeRemain) % BlockSize == 0) && (sizeRemain > 0))
                        {
                            byte[] tmpNextBlockPtr = new byte[8];
                            disk.Read(tmpNextBlockPtr, 0, 8);
                            disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                        }
                        continue;
                    }

                    // check if it is the file name
                    if (tmpFR.filename == FileName)
                    {
                        frPos = disk.Position - 8;
                        disk.Seek(-FileRecord.Size, SeekOrigin.Current);
                        movedFR = new FileRecord();
                        movedFR.Isolate(reader.ReadBytes(FileRecord.Size));
                        break;
                    }

                    // change block if current block is fully read
                    if (((parentFT.fileSize - sizeRemain) % BlockSize == 0) && (sizeRemain > 0))
                    {
                        byte[] tmpNextBlockPtr = new byte[8];
                        disk.Read(tmpNextBlockPtr, 0, 8);
                        disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                    }
                }

                if (movedFR == null)
                {
                    return +2;  // file not found
                }

                // get new filepath FT
                retCode = GetFileTable(newParentPath, newStartFID, out newParentFT);
                if (retCode != 0)
                {
                    return +retCode;
                }

                // check permission
                if (!HasPermission(newParentFT, Permission.Write))
                {
                    return +3;  // no permission
                }

                // insert new fr
                movedFR.filename = newFileName;
                retCode = InsertFileRecord(newParentFT, movedFR);
                if (retCode != 0)
                {
                    return retCode;
                }

                // check if two parentFRs are the same location
                if (parentFT.firstBlockID == newParentFT.firstBlockID)
                {
                    parentFT = newParentFT;
                }

                // invalidate original fr
                disk.Seek(frPos, SeekOrigin.Begin);
                writer.Write(0ul);

                // modify parent ft
                parentFT.modifyTime = parentFT.accessTime = DateTime.Now;
                disk.Seek((long)(FT_ST + parentFT.firstBlockID * FileTable.Size), SeekOrigin.Begin);
                disk.Write(parentFT.Combine(), 0, FileTable.Size);
            }
            catch (Exception)
            {
                return +4;  // Error when writing file
            }

            return 0;
        }

        public int UploadFile(string newFilePath, string parentPath, ulong startFID, string FileName, ushort FileProperty, ushort FilePermission)
        {
            int retCode;
            FileTable parentFT;

            // open the new file to be uploaded
            FileStream newFile = new FileStream(newFilePath, FileMode.Open, FileAccess.Read, FileShare.None);

            // get parent directory's FileTable
            retCode = GetFileTable(parentPath, startFID, out parentFT);
            if (retCode != 0)
            {
                return +retCode;
            }

            // check permission
            if (!HasPermission(parentFT, Permission.Write))
            {
                return +1;  // no permission
            }

            // check whether disk has enough blocks + 1
            ulong newFileBlockNum = (ulong)(newFile.Length) / BlockSize;
            if ((ulong)(newFile.Length) % BlockSize != 0)
            {
                newFileBlockNum++;
            }
            if ((HeaderFSCapacity - HeaderFSSize) / BlockSize < (newFileBlockNum + 1))
            {
                return +2;  // no enough disk space
            }

            // prepare new file's FileRecord except FID
            FileRecord newFR = new FileRecord();
            newFR.filename = FileName;

            // prepare new file's File Table except first block pointer
            FileTable newFT = new FileTable();
            newFT.SetProperty(FileProperty);
            newFT.SetPermission(FilePermission);
            newFT.owneruid = currentUserID;
            newFT.ownergid = currentGroupID;
            newFT.createTime = File.GetCreationTime(newFilePath);
            newFT.modifyTime = File.GetLastWriteTime(newFilePath);
            newFT.accessTime = File.GetLastAccessTime(newFilePath);
            newFT.fileSize = (ulong)newFile.Length;
            
            // Open new blocks
            ulong[] newBlockIDs;
            retCode = OpenNewBlocks(newFileBlockNum, out newBlockIDs);
            if (retCode != 0)
            {
                return +retCode;
            }

            // assign the first block pointer to the FileTable and FileRecord
            newFR.FID = newFT.firstBlockID = newBlockIDs[0];

            // insert FileRecord to parent directory
            retCode = InsertFileRecord(parentFT, newFR);
            if (retCode != 0)
            {
                RecycleBlocks(newBlockIDs);
                return retCode;
            }

            // write FileTable to disk
            disk.Seek((long)(FT_ST + newFR.FID * FileTable.Size), SeekOrigin.Begin);
            disk.Write(newFT.Combine(), 0, FileTable.Size);

            // write new file data block by block
            long remainSize = newFile.Length;
            newFile.Seek(0, SeekOrigin.Begin);
            byte[] tmpBlock = new byte[BlockSize];
            byte[] nextBlockPtr = new byte[8];
            for (int i = 0; i < newBlockIDs.Length; i++)
            {
                if ((ulong)remainSize < BlockSize)
                {
                    newFile.Read(tmpBlock, 0, (int)remainSize);
                    disk.Seek((long)(D_ST + newBlockIDs[i] * (BlockSize + 8)), SeekOrigin.Begin);
                    disk.Write(tmpBlock, 0, (int)remainSize);
                }
                else
                {
                    newFile.Read(tmpBlock, 0, (int)BlockSize);
                    disk.Seek((long)(D_ST + newBlockIDs[i] * (BlockSize + 8)), SeekOrigin.Begin);
                    disk.Write(tmpBlock, 0, (int)BlockSize);
                    remainSize -= (long)BlockSize;
                    if (i != newBlockIDs.Length - 1)
                    {
                        disk.Write(PFSService.NumToBytes(newBlockIDs[i + 1], 0, 8), 0, 8);
                    }
                }
            }

            return 0;
        }

        public int ExtractFile(ulong startFID, string SourceFilePath, string DestinationFilePath)
        {
            int retCode;
            FileTable sourceFT;
            FileTable fileFT;

            // get source FileTable
            retCode = GetFileTable(SourceFilePath, startFID, out sourceFT);
            if (retCode != 0)
            {
                return +retCode;
            }

            // check if it is a file
            if (sourceFT.propertyDirectory)
            {
                return +1;  // not a file
            }

            // check permission
            if (!HasPermission(sourceFT, Permission.Read))
            {
                return +2;  // no permission
            }

            try
            {
                // open output file
                FileStream newFile = new FileStream(DestinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

                // extract file
                byte[] tmpBlock = new byte[BlockSize];
                disk.Seek((long)(D_ST + sourceFT.firstBlockID * (BlockSize + 8)), SeekOrigin.Begin);
                byte[] tmpNextBlockPtr = new byte[8];
                for (ulong remainSize = sourceFT.fileSize; remainSize > 0;)
                {
                    if (remainSize < BlockSize)
                    {
                        disk.Read(tmpBlock, 0, (int)remainSize);
                        newFile.Write(tmpBlock, 0, (int)remainSize);
                        remainSize = 0;
                    }
                    else
                    {
                        disk.Read(tmpBlock, 0, (int)BlockSize);
                        newFile.Write(tmpBlock, 0, (int)BlockSize);
                        remainSize -= BlockSize;
                        disk.Read(tmpNextBlockPtr, 0, 8);
                        disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextBlockPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);
                    }
                }

                newFile.Close();
                newFile.Dispose();

                // modofy newfile property
                File.SetCreationTime(DestinationFilePath, sourceFT.createTime);
                File.SetLastWriteTime(DestinationFilePath, sourceFT.modifyTime);
                File.SetLastAccessTime(DestinationFilePath, sourceFT.accessTime);
            }
            catch (Exception)
            {
                return +3;  // error when writing

            }

            return 0;
        }

        public int UpdateFile(string newFilePath, string parentPath, ulong startFID, string FileName, ushort FileProperty, ushort FilePermission)
        {
            int retCode;
            FileTable parentFT;

            // open the new file to be uploaded
            FileStream newFile = new FileStream(newFilePath, FileMode.Open, FileAccess.Read, FileShare.None);

            // get parent directory's FileTable
            retCode = GetFileTable(parentPath, startFID, out parentFT);
            if (retCode != 0)
            {
                return +retCode;
            }

            // check permission
            if (!HasPermission(parentFT, Permission.Write))
            {
                return +1;  // no permission
            }

            // get old file's file table
            FileTable oldFileFT;
            retCode = GetFileTable(FileName, parentFT.firstBlockID, out oldFileFT);
            if (retCode != 0)
            {
                return +2;  // unable to find original file
            }

            // get new and old file's needed blocks
            ulong newFileBlockNum = (ulong)(newFile.Length) / BlockSize;
            if ((ulong)(newFile.Length) % BlockSize != 0)
            {
                newFileBlockNum++;
            }
            ulong oldFileBlockNum = oldFileFT.fileSize / BlockSize;
            if (oldFileFT.fileSize  % BlockSize != 0)
            {
                oldFileBlockNum++;
            }
            if (newFileBlockNum == 0)  // empty file still needs one block;
            {
                newFileBlockNum = 1;
            }
            if (oldFileBlockNum == 0)  // empty file still needs one block;
            {
                oldFileBlockNum = 1;
            }

            // set difference of blocks
            ulong moreBlockNum = 0;
            ulong lessBlockNum = 0;
            if (newFileBlockNum > oldFileBlockNum)
            {
                moreBlockNum = newFileBlockNum - oldFileBlockNum;
            }
            else
            {
                lessBlockNum = oldFileBlockNum - newFileBlockNum;
            }

            // check whether disk has enough blocks
            if (moreBlockNum > 0 && (HeaderFSCapacity - HeaderFSSize) / BlockSize < moreBlockNum)
            {
                return +2;  // no enough disk space
            }

            // apply new blocks if needed
            ulong[] newBlocks;
            retCode = OpenNewBlocks(moreBlockNum, out newBlocks);  // array length could be 0
            if (retCode != 0)
            {
                return retCode;
            }

            // update old file's FT
            oldFileFT.modifyTime = DateTime.Now;
            oldFileFT.fileSize = (ulong)newFile.Length;
            disk.Seek((long)(FT_ST + oldFileFT.firstBlockID * FileTable.Size), SeekOrigin.Begin);
            disk.Write(oldFileFT.Combine(), 0, FileTable.Size);

            // write new file data block by block
            byte[] tmpBlock = new byte[BlockSize];
            byte[] tmpNextPtr = new byte[8];
            newFile.Position = 0;
            int newBlockPtrRecorded = 0;
            bool needNewBlocks = false;
            disk.Seek((long)(D_ST + oldFileFT.firstBlockID * (BlockSize + 8)), SeekOrigin.Begin);
            long remainSize = newFile.Length;
            while (remainSize > 0)
            {
                // read new file a block
                if ((ulong)remainSize <= BlockSize)
                {
                    newFile.Read(tmpBlock, 0, (int)remainSize);
                    disk.Write(tmpBlock, 0, (int)remainSize);
                    break;
                }
                else
                {
                    newFile.Read(tmpBlock, 0, (int)BlockSize);
                    disk.Write(tmpBlock, 0, (int)BlockSize);
                    remainSize -= (long)BlockSize;
                    // write next block ptr
                    if (!needNewBlocks && (ulong)(newFile.Length - remainSize) >= oldFileFT.fileSize && newBlockPtrRecorded < newBlocks.Length)
                    {
                        needNewBlocks = true;
                    }
                    if (needNewBlocks)
                    {
                        disk.Write(PFSService.NumToBytes((ulong)newBlocks[newBlockPtrRecorded], 0, 8), 0, 8);
                        disk.Seek((long)(D_ST + newBlocks[newBlockPtrRecorded] * (BlockSize + 8)), SeekOrigin.Begin);  // go to next block (new block)
                        newBlockPtrRecorded++;
                    }
                    else
                    {
                        disk.Read(tmpNextPtr, 0, 8);
                        disk.Seek((long)(D_ST + PFSService.BytesToNum(tmpNextPtr, 0, 8) * (BlockSize + 8)), SeekOrigin.Begin);  // go to next block (old block)
                    }
                }
            }

            // recycle extra blocks
            if (lessBlockNum > 0)
            {
                ulong[] extraBlocks = new ulong[lessBlockNum];
                disk.Seek((long)BlockSize - remainSize, SeekOrigin.Current);  // seek to the next ptr position
                for (ulong i = 0; i < lessBlockNum; i++)
                {
                    disk.Read(tmpNextPtr, 0, 8);
                    extraBlocks[i] = PFSService.BytesToNum(tmpNextPtr, 0, 8);
                    disk.Seek((long)(D_ST + extraBlocks[i] * (BlockSize + 8) + BlockSize), SeekOrigin.Begin);
                }

                retCode = RecycleBlocks(extraBlocks);
                if (retCode != 0)
                {
                    return retCode;
                }
            }

            return 0;
        }

        public int ModifyFile(string parentPath, ulong startFID, string FileName, ulong startByteNum, byte[] data, ulong startDataNum, ulong dataLength)
        {
            // to be deleted
            return 0;
        }

        public int RenameDisk(string newDiskName)
        {
            // check new name
            if (newDiskName.Length == 0 || newDiskName.Length > 256)
            {
                return +1;  // name cannot be empty
            }
            if (!PFSService.ValidName(newDiskName))
            {
                return +2;  // name not validate
            }

            // change disk name
            HeaderDiskName = newDiskName;

            // check if disk name is changed
            if (HeaderDiskName != newDiskName)
            {
                return +3;  // error when write disk
            }

            return 0;
        }
        

        public byte[] HeaderVerificationCode
        {
            get
            {
                byte[] result = new byte[2];
                try
                {
                    disk.Seek(PFSService.H_VERIFICATION_ST, SeekOrigin.Begin);
                    disk.Read(result, 0, result.Length);
                }
                catch (Exception)
                {
                    return null;
                }
                return result;
            }
        }
        public byte[] HeaderFSVersion
        {
            get
            {
                byte[] result = new byte[3];
                try
                {
                    disk.Seek(PFSService.H_VERSION_ST, SeekOrigin.Begin);
                    disk.Read(result, 0, result.Length);
                }
                catch (Exception)
                {
                    return null;
                }
                return result;
            }
        }
        public byte HeaderBlockSize
        {
            get
            {
                byte result;
                try
                {
                    disk.Seek(PFSService.H_TYPE_ST, SeekOrigin.Begin);
                    result = (byte)disk.ReadByte();
                }
                catch (Exception)
                {
                    return 0;
                }
                return result;
            }
        }
        public byte[] HeaderFSFlags
        {
            get
            {
                byte[] result = new byte[2];
                try
                {
                    disk.Seek(PFSService.H_FLAGS_ST, SeekOrigin.Begin);
                    disk.Read(result, 0, result.Length);
                }
                catch (Exception)
                {
                    return null;
                }
                return result;
            }
        }
        public byte[] HeaderSVC
        {
            get
            {
                byte[] result = new byte[16];
                try
                {
                    disk.Seek(PFSService.H_SVC_ST, SeekOrigin.Begin);
                    disk.Read(result, 0, result.Length);
                }
                catch (Exception)
                {
                    return null;
                }
                return result;
            }
        }
        public ulong HeaderFSSize
        {
            get
            {
                try
                {
                    disk.Seek(PFSService.H_SIZE_USED_ST, SeekOrigin.Begin);
                    return reader.ReadUInt64();
                    
                }
                catch (Exception)
                {
                    return ulong.MaxValue;
                }
            }
            set
            {
                disk.Seek(PFSService.H_SIZE_USED_ST, SeekOrigin.Begin);
                writer.Write(value);
            }
        }
        public ulong HeaderFSCapacity
        {
            get
            {
                try
                {
                    disk.Seek(PFSService.H_SIZE_CAP_ST, SeekOrigin.Begin);
                    return reader.ReadUInt64();

                }
                catch (Exception)
                {
                    return ulong.MaxValue;
                }
            }
        }
        public string HeaderDiskName
        {
            get
            {
                byte[] buffer = new byte[256];
                int nullIndex = 256;
                try
                {
                    disk.Seek(PFSService.H_DISKNAME_ST, SeekOrigin.Begin);
                    disk.Read(buffer, 0, buffer.Length);
                }
                catch (Exception)
                {
                    return "";
                }
                for (int i = 0; i < 256; i++)
                {
                    if (buffer[i] == 0)
                    {
                        nullIndex = i;
                        break;
                    }
                }

                return Encoding.Default.GetString(buffer, 0, nullIndex);
            }
            set
            {
                try
                {
                    disk.Seek(PFSService.H_DISKNAME_ST, SeekOrigin.Begin);
                    if (value.Length >= 256)
                    {
                        disk.Write(Encoding.Default.GetBytes(value), 0, 256);
                    }
                    else
                    {
                        disk.Write(Encoding.Default.GetBytes(value), 0, value.Length);
                        disk.WriteByte(0);
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        public uint HeaderLastUser
        {
            get
            {
                try
                {
                    disk.Seek(PFSService.H_LAST_USER_ST, SeekOrigin.Begin);
                    return reader.ReadUInt32();

                }
                catch (Exception)
                {
                    return uint.MaxValue;
                }
            }
        }
        public byte[] HeaderTerminalCode
        {
            get
            {
                byte[] result = new byte[9];
                try
                {
                    disk.Seek(-9, SeekOrigin.End);
                    disk.Read(result, 0, result.Length);
                }
                catch (Exception)
                {
                    return null;
                }
                return result;
            }
        }

        public bool HeaderFlagEncryption
        {
            get { return PFSService.IsKthBitSet(HeaderFSFlags[0], 8); }
        }
        
        public bool HeaderFlagInitialized
        {
            get { return PFSService.IsKthBitSet(HeaderFSFlags[1], 8); }
            set
            {
                byte tmpByte;
                disk.Seek(7, SeekOrigin.Begin);
                tmpByte = (byte)disk.ReadByte();
                PFSService.ChangeBit8(ref tmpByte, 8, value);
                disk.Seek(7, SeekOrigin.Begin);
                disk.WriteByte(tmpByte);
            }
        }
    }

    public class FileTable
    {
        public const int Size = 52;

        // property: 2 Bytes
        public bool propertyDirectory = false;
        public bool propertySystem = false;
        public bool propertyEncryption = false;
        public bool propertyHidden = false;
        public bool propertyReserved5 = false;
        public bool propertyReserved6 = false;
        public bool propertyReserved7 = false;
        public bool propertyReserved8 = false;
        public bool propertyReserved9 = false;
        public bool propertyReserved10 = false;
        public bool propertyReserved11 = false;
        public bool propertyReserved12 = false;
        public bool propertyReserved13 = false;
        public bool propertyReserved14 = false;
        public bool propertyReserved15 = false;
        public bool propertyReserved16 = false;
        // permission: 2 Bytes
        public bool permissionUserRead = false;
        public bool permissionUserWrite = false;
        public bool permissionUserExec = false;
        public bool permissionGroupRead = false;
        public bool permissionGroupWrite = false;
        public bool permissionGroupExec = false;
        public bool permissionOtherRead = false;
        public bool permissionOtherWrite = false;
        public bool permissionOtherExec = false;
        public bool permissionReserved10 = false;
        public bool permissionReserved11 = false;
        public bool permissionReserved12 = false;
        public bool permissionReserved13 = false;
        public bool permissionReserved14 = false;
        public bool permissionReserved15 = false;
        public bool permissionReserved16 = false;
        // owner uid: 4 Bytes
        public uint owneruid = 0;
        // owner gid: 4 Bytes
        public uint ownergid = 0;
        // create time: 8 Bytes
        public DateTime createTime;
        // modify time: 8 Bytes
        public DateTime modifyTime;
        // access time: 8 Bytes
        public DateTime accessTime;
        // file size: 8 Bytes
        public ulong fileSize;
        // first block id: 8 Bytes
        public ulong firstBlockID;

        public void SetProperty(ushort property)
        {
            propertyDirectory = PFSService.IsKthBitSet((property), 16);
            propertySystem = PFSService.IsKthBitSet((property), 15);
            propertyEncryption = PFSService.IsKthBitSet((property), 14);
            propertyHidden = PFSService.IsKthBitSet((property), 13);
            propertyReserved5 = PFSService.IsKthBitSet((property), 12);
            propertyReserved6 = PFSService.IsKthBitSet((property), 11);
            propertyReserved7 = PFSService.IsKthBitSet((property), 10);
            propertyReserved8 = PFSService.IsKthBitSet((property), 9);
            propertyReserved9 = PFSService.IsKthBitSet((property), 8);
            propertyReserved10 = PFSService.IsKthBitSet((property), 7);
            propertyReserved11 = PFSService.IsKthBitSet((property), 6);
            propertyReserved12 = PFSService.IsKthBitSet((property), 5);
            propertyReserved13 = PFSService.IsKthBitSet((property), 4);
            propertyReserved14 = PFSService.IsKthBitSet((property), 3);
            propertyReserved15 = PFSService.IsKthBitSet((property), 2);
            propertyReserved16 = PFSService.IsKthBitSet((property), 1);
        }

        public void SetPermission(ushort permission)
        {
            permissionUserRead = PFSService.IsKthBitSet((permission), 16);
            permissionUserWrite = PFSService.IsKthBitSet((permission), 15);
            permissionUserExec = PFSService.IsKthBitSet((permission), 14);
            permissionGroupRead = PFSService.IsKthBitSet((permission), 13);
            permissionGroupWrite = PFSService.IsKthBitSet((permission), 12);
            permissionGroupExec = PFSService.IsKthBitSet((permission), 11);
            permissionOtherRead = PFSService.IsKthBitSet((permission), 10);
            permissionOtherWrite = PFSService.IsKthBitSet((permission), 9);
            permissionOtherExec = PFSService.IsKthBitSet((permission), 8);
            permissionReserved10 = PFSService.IsKthBitSet((permission), 7);
            permissionReserved11 = PFSService.IsKthBitSet((permission), 6);
            permissionReserved12 = PFSService.IsKthBitSet((permission), 5);
            permissionReserved13 = PFSService.IsKthBitSet((permission), 4);
            permissionReserved14 = PFSService.IsKthBitSet((permission), 3);
            permissionReserved15 = PFSService.IsKthBitSet((permission), 2);
            permissionReserved16 = PFSService.IsKthBitSet((permission), 1);
        }

        public void Isolate(byte[] data)
        {
            if (data.Length != Size)
            {
                throw new SystemException("Input data has invalid length.");
            }

            //ushort property = BitConverter.ToUInt16(data, 0);
            ushort property = (ushort)PFSService.BytesToNum(data, 0, 2);
            SetProperty(property);
            
            ushort permission = (ushort)PFSService.BytesToNum(data, 2, 2);
            SetPermission(permission);

            owneruid = (uint)PFSService.BytesToNum(data, 4, 4);

            ownergid = (uint)PFSService.BytesToNum(data, 8, 4);

            createTime = DateTime.FromBinary((long)PFSService.BytesToNum(data, 12,8));

            modifyTime = DateTime.FromBinary((long)PFSService.BytesToNum(data, 20, 8));

            accessTime = DateTime.FromBinary((long)PFSService.BytesToNum(data, 28, 8));

            fileSize = PFSService.BytesToNum(data, 36, 8);

            firstBlockID = PFSService.BytesToNum(data, 44, 8);
        }

        public byte[] Combine()
        {
            byte[] result = new byte[Size];

            uint property = 0;
            PFSService.ChangeBit32(ref property, 16, propertyDirectory);
            PFSService.ChangeBit32(ref property, 15, propertySystem);
            PFSService.ChangeBit32(ref property, 14, propertyEncryption);
            PFSService.ChangeBit32(ref property, 13, propertyHidden);
            PFSService.ChangeBit32(ref property, 12, propertyReserved5);
            PFSService.ChangeBit32(ref property, 11, propertyReserved6);
            PFSService.ChangeBit32(ref property, 10, propertyReserved7);
            PFSService.ChangeBit32(ref property, 9, propertyReserved8);
            PFSService.ChangeBit32(ref property, 8, propertyReserved9);
            PFSService.ChangeBit32(ref property, 7, propertyReserved10);
            PFSService.ChangeBit32(ref property, 6, propertyReserved11);
            PFSService.ChangeBit32(ref property, 5, propertyReserved12);
            PFSService.ChangeBit32(ref property, 4, propertyReserved13);
            PFSService.ChangeBit32(ref property, 3, propertyReserved14);
            PFSService.ChangeBit32(ref property, 2, propertyReserved15);
            PFSService.ChangeBit32(ref property, 1, propertyReserved16);
            PFSService.NumToBytes(property, 6, 2).CopyTo(result, 0);

            uint permission = 0;
            PFSService.ChangeBit32(ref permission, 16, permissionUserRead);
            PFSService.ChangeBit32(ref permission, 15, permissionUserWrite);
            PFSService.ChangeBit32(ref permission, 14, permissionUserExec);
            PFSService.ChangeBit32(ref permission, 13, permissionGroupRead);
            PFSService.ChangeBit32(ref permission, 12, permissionGroupWrite);
            PFSService.ChangeBit32(ref permission, 11, permissionGroupExec);
            PFSService.ChangeBit32(ref permission, 10, permissionOtherRead);
            PFSService.ChangeBit32(ref permission, 9, permissionOtherWrite);
            PFSService.ChangeBit32(ref permission, 8, permissionOtherExec);
            PFSService.ChangeBit32(ref permission, 7, permissionReserved10);
            PFSService.ChangeBit32(ref permission, 6, permissionReserved11);
            PFSService.ChangeBit32(ref permission, 5, permissionReserved12);
            PFSService.ChangeBit32(ref permission, 4, permissionReserved13);
            PFSService.ChangeBit32(ref permission, 3, permissionReserved14);
            PFSService.ChangeBit32(ref permission, 2, permissionReserved15);
            PFSService.ChangeBit32(ref permission, 1, permissionReserved16);
            PFSService.NumToBytes(permission, 6, 2).CopyTo(result, 2);

            PFSService.NumToBytes(owneruid, 4, 4).CopyTo(result, 4);

            PFSService.NumToBytes(ownergid, 4, 4).CopyTo(result, 8);

            PFSService.NumToBytes((ulong)createTime.ToBinary(), 0, 8).CopyTo(result, 12);

            PFSService.NumToBytes((ulong)modifyTime.ToBinary(), 0, 8).CopyTo(result, 20);

            PFSService.NumToBytes((ulong)accessTime.ToBinary(), 0, 8).CopyTo(result, 28);

            PFSService.NumToBytes(fileSize, 0, 8).CopyTo(result, 36);

            PFSService.NumToBytes(firstBlockID, 0, 8).CopyTo(result, 44);

            return result;
        }
    }

    public class FileRecord
    {
        public const int Size = 256;
        public const int MaxFilenameLength = 248;

        public string filename;
        public ulong FID;

        public void Isolate(byte[] data)
        {
            if (data.Length != Size)
            {
                throw new SystemException("Input data has invalid length.");
            }

            int nullIndex = MaxFilenameLength;
            for (int i = 0; i < MaxFilenameLength; i++)
            {
                if (data[i] == 0)
                {
                    nullIndex = i;
                    break;
                }
            }
            //filename = BitConverter.ToString(data, 0, nullIndex);
            filename = Encoding.Default.GetString(data, 0, nullIndex);
            FID = PFSService.BytesToNum(data, MaxFilenameLength, 8);
        }

        public byte[] Combine()
        {
            byte[] result = new byte[Size];

            Encoding.Default.GetBytes(filename).CopyTo(result, 0);
            if (filename.Length < MaxFilenameLength)
            {
                result[filename.Length] = 0;
            }
            PFSService.NumToBytes(FID, 0, 8).CopyTo(result, MaxFilenameLength);

            return result;
        }
    }

    public class User
    {
        public static int Size = 512;
        public static int MaxUserNameLength = 64;
        public static int MaxDescriptionLength = 128;

        public uint UserID;  // 4B
        public uint[] GroupIDs;  // 64B
        public ulong HomeDirectoryFID;  // 8B
        public string Username;  // 64B
        public string Description;  // 128B
        public DateTime CreatedTime;  // 8B
        public DateTime LastLoggedInTime;  // 8B
        public byte[] Reserved;  // 100B
        public byte[] PasswordHash;  // 128B

        public void Isolate(byte[] data)
        {
            if (data.Length != Size)
            {
                throw new SystemException("Input data has invalid length.");
            }

            UserID = (uint)PFSService.BytesToNum(data, 0, 4);
            GroupIDs = new uint[16];
            for (int i = 0; i < 16; i++)
            {
                GroupIDs[i] = (uint)PFSService.BytesToNum(data, 4 + 4 * i, 4);
            }
            HomeDirectoryFID = PFSService.BytesToNum(data, 68, 8);
            int nullIndex = MaxUserNameLength;
            for (int i = 0; i < MaxUserNameLength; i++)
            {
                if (data[76+i] == 0)
                {
                    nullIndex = i;
                    break;
                }
            }
            Username = Encoding.Default.GetString(data, 76, nullIndex);
            nullIndex = MaxDescriptionLength;
            for (int i = 0; i < MaxDescriptionLength; i++)
            {
                if (data[140+i] == 0)
                {
                    nullIndex = i;
                    break;
                }
            }
            Description = Encoding.Default.GetString(data, 140, nullIndex);
            CreatedTime = DateTime.FromBinary((long)PFSService.BytesToNum(data, 268, 8));
            LastLoggedInTime = DateTime.FromBinary((long)PFSService.BytesToNum(data, 276, 8));
            Reserved = new byte[100];
            Array.Copy(data, 284, Reserved, 0, 100);
            PasswordHash = null;
            for (int i = 384; i < 512; i++)
            {
                if (data[i] != 0)
                {
                    PasswordHash = new byte[128];
                    Array.Copy(data, 384, PasswordHash, 0, 128);
                    break;
                }
            }
        }

        public byte[] Combine()
        {
            if (Username == null || Description == null || Reserved == null || Reserved.Length != 100 || (PasswordHash != null && PasswordHash.Length != 128) || GroupIDs == null || (GroupIDs != null && GroupIDs.Length != 16))
            {
                throw new SystemException("Not all fields initialized.");
            }

            byte[] result = new byte[Size];

            PFSService.NumToBytes(UserID, 4, 4).CopyTo(result, 0);
            for (int i = 0; i < 16; i++)
            {
                PFSService.NumToBytes(GroupIDs[i], 4, 4).CopyTo(result, 4 + i * 4);
            }
            PFSService.NumToBytes(HomeDirectoryFID, 0, 8).CopyTo(result, 68);
            Encoding.Default.GetBytes(Username).CopyTo(result, 76);
            if (Username.Length < MaxUserNameLength)
            {
                result[76 + Username.Length] = 0;
            }
            Encoding.Default.GetBytes(Description).CopyTo(result, 140);
            if (Description.Length < MaxDescriptionLength)
            {
                result[140 + Description.Length] = 0;
            }
            PFSService.NumToBytes((ulong)CreatedTime.ToBinary(), 0, 8).CopyTo(result, 268);
            PFSService.NumToBytes((ulong)LastLoggedInTime.ToBinary(), 0, 8).CopyTo(result, 276);
            Reserved.CopyTo(result, 284);
            if (PasswordHash == null)
            {
                for (int i = 384; i < 512; i++)
                {
                    result[i] = 0;
                }
            }
            else
            {
                PasswordHash.CopyTo(result, 384);
            }

            return result;
        }
    }

    public class Group
    {
        public static int Size = 256;
        public static int MaxGroupNameLength = 64;
        public static int MaxDescriptionLength = 128;

        public uint GroupID;  // 4B
        public string GroupName;  // 64B
        public string Description;  // 128B
        public DateTime CreatedTime;  // 8B
        public byte[] Reserved;  // 52B

        public void Isolate(byte[] data)
        {
            if (data.Length != Size)
            {
                throw new SystemException("Input data has invalid length.");
            }

            GroupID = (uint)PFSService.BytesToNum(data, 0, 4);
            int nullIndex = MaxGroupNameLength;
            for (int i = 0; i < MaxGroupNameLength; i++)
            {
                if (data[4+i] == 0)
                {
                    nullIndex = i;
                    break;
                }
            }
            GroupName = Encoding.Default.GetString(data, 4, nullIndex);
            for (int i = 0; i < MaxDescriptionLength; i++)
            {
                if (data[68+i] == 0)
                {
                    nullIndex = i;
                    break;
                }
            }
            Description = Encoding.Default.GetString(data, 68, nullIndex);
            CreatedTime = DateTime.FromBinary((long)PFSService.BytesToNum(data, 196, 8));
            Reserved = new byte[52];
            Array.Copy(data, 204, Reserved, 0, 52);
        }

        public byte[] Combine()
        {
            if (GroupName == null || Reserved == null || (Reserved != null && Reserved.Length != 52))
            {
                throw new SystemException("Not all fields initialized.");
            }

            byte[] result = new byte[Size];

            PFSService.NumToBytes(GroupID, 4, 4).CopyTo(result, 0);
            Encoding.Default.GetBytes(GroupName).CopyTo(result, 4);
            if (GroupName.Length < MaxGroupNameLength)
            {
                result[4 + GroupName.Length] = 0;
            }
            Encoding.Default.GetBytes(Description).CopyTo(result, 68);
            if (Description.Length < MaxDescriptionLength)
            {
                result[68 + Description.Length] = 0;
            }
            PFSService.NumToBytes((ulong)CreatedTime.ToBinary(), 0, 8).CopyTo(result, 196);
            Reserved.CopyTo(result, 204);

            return result;
        }
    }
}