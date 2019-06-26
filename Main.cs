using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text;
using System.IO;

using PFSKernel;
using CodeService;
using ErrorDes = Errors.ErrorDescription;
using Window;


namespace Main
{
    static class MainClass
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Console.WriteLine("This is the very begining!");
            Application.Run(new CommandLineWindow(new Disk()));

            /*
            string ori = "git -d asdf -f a -n \"this is a file\" -s \"new 666\"\"old 777\" everyone";
            string[] args = splitCmd(ori);
            Console.WriteLine(ori);
            for (int i = 0; i < args.Length; i++)
            {
                Console.WriteLine(args[i]);
            }
            Console.WriteLine("over");
            */

            /*
            Console.WriteLine(PFSService.SimplifyPath("/huang"));
            Console.WriteLine(PFSService.SimplifyPath("/huang/a"));
            Console.WriteLine(PFSService.SimplifyPath("/a/b/c/../.."));
            Console.WriteLine(PFSService.SimplifyPath("/a/b/"));
            Console.WriteLine(PFSService.SimplifyPath("/a/c/d/././."));
            Console.WriteLine(PFSService.SimplifyPath("/a/b/c/./"));
            Console.WriteLine(PFSService.SimplifyPath("/a/b/../.."));
            Console.WriteLine(PFSService.SimplifyPath("/a/b/../../../"));
            */

            /*
            string[] paths = PFSService.SplitPath("a/b/new folder/c黄/d");
            for (int i = 0; i < paths.Length; i++)
            {
                Console.WriteLine(paths[i]);
            }
            */

            /*
            byte[] SCK = null;// new byte[48] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48};
            PFSService.CreateSecurityCertification("SCK", ref SCK);

            Console.WriteLine("CreateDisk returns: " + PFSService.CreateDisk("newdisk", "a哈哈", PFSService.BlockSize.BS4K, 40960, SCK));

            Disk disk = new Disk();
            Console.WriteLine("Load returns: " + disk.LoadDisk("newdisk", "SCK"));

            Console.WriteLine(disk.HeaderDiskName);
            Console.WriteLine(disk.HeaderFSSize);
            Console.WriteLine(disk.HeaderFSCapacity);
            Console.WriteLine(disk.HeaderBlockSize);
            Console.WriteLine(disk.HeaderLastUser);
            Debug.printBytesDec(disk.HeaderVerificationCode);
            Debug.printBytesDec(disk.HeaderFSFlags);
            Debug.printBytesDec(disk.HeaderFSVersion);
            Debug.printBytesDec(disk.HeaderSVC);
            Debug.printBytesDec(disk.HeaderTerminalCode);
            Console.WriteLine("");

            FileTable ft;
            List<FileRecord> frs;

            disk.GetFileTable("/../..", 0, out ft);
            disk.ListFileRecords(ft, out frs);
            Console.WriteLine("List /");
            foreach (var item in frs)
            {
                Console.WriteLine(item.filename);
            }

            disk.CreateEmptyFile("/", 0, "newerFile", (ushort)Disk.FileType.File, (ushort)(Disk.FileTablePermission.u7) + (ushort)(Disk.FileTablePermission.g4) + (ushort)(Disk.FileTablePermission.o4));
            disk.CreateEmptyFile("/", 0, "newnewFile", (ushort)Disk.FileType.File, (ushort)(Disk.FileTablePermission.u7) + (ushort)(Disk.FileTablePermission.g4) + (ushort)(Disk.FileTablePermission.o4));
            disk.CreateEmptyFile("/", 0, "newFolder", (ushort)Disk.FileType.Directory, (ushort)(Disk.FileTablePermission.u7) + (ushort)(Disk.FileTablePermission.g4) + (ushort)(Disk.FileTablePermission.o4));

            disk.GetFileTable("/../..", 0, out ft);
            disk.ListFileRecords(ft, out frs);
            Console.WriteLine("List /");
            foreach (var item in frs)
            {
                Console.WriteLine(item.filename);
            }
            */

            /*
            byte[] plainText = new byte[10];
            for (int i = 0; i < plainText.Length; i++)
            {
                plainText[i] = (byte)(i+65);
            }
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            byte[] encrypted = Cryption.Encrypt(plainText, aes.Key, aes.IV);
            byte[] roundTrip = Cryption.Decrypt(encrypted, aes.Key, aes.IV);

            Console.WriteLine(plainText.Length);
            Console.WriteLine(encrypted.Length);
            Console.WriteLine(roundTrip.Length);

            Console.WriteLine("\nTest:");
            for (int i = 0; i < plainText.Length; i++)
            {
                if (plainText[i] == roundTrip[i])
                {
                    continue;
                }
                else
                {
                    Console.WriteLine("Failed at: " + i);
                }
            }
            Console.WriteLine("Finished. Pass if no error occur.");
            Console.WriteLine("KEY and IV length:");
            Console.WriteLine(aes.Key.Length);
            Console.WriteLine(aes.IV.Length);
            
            Console.WriteLine("MD5 Test:");
            Console.WriteLine("PlainText:");
            for (int i = 0; i < plainText.Length; i++)
            {
                Console.WriteLine(plainText[i]);
            }
            Console.WriteLine("Hashed code:");
            byte[] hashedCode = Hash.HashMD5(plainText);
            for (int i = 0; i < hashedCode.Length; i++)
            {
                Console.WriteLine("Hex: {0:X}", hashedCode[i]);
            }
            Console.WriteLine("Verifying MD5...");
            Console.WriteLine(Hash.VerifyMD5(plainText,hashedCode));

            Console.WriteLine("Test error codes:");
            Console.WriteLine(ErrorDes.Errors[1005]);
            */


        }
        
    }

}
