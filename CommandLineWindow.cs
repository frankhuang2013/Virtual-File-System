using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using PFSKernel;
using Errors;
using System.IO;

namespace Window
{
    public partial class CommandLineWindow : Form
    {
        Disk disk;
        private string oldText = "";
        private string cmd = "";
        private int charEntered = 0;
        List<string> cmdRecorder = new List<string>();
        int cmdRecorderIndex;
        int specialOpCode;  // 0:NULL, 1:Close

        internal CommandLineWindow(Disk disk)
        {
            InitializeComponent();
            this.disk = disk;
        }

        private void CommandLineWindow_Load(object sender, EventArgs e)
        {
            richTextBox.Text = "";
            richTextBox.AppendText(GeneratePrompt());
            oldText = richTextBox.Text;
        }

        private string GeneratePrompt()
        {
            if (disk.status != 3)  // when the disk is not fully loaded
            {
                return "> ";
            }
            else
            {
                return "[" + disk.currentUserName + "] " + disk.currentPath + " > ";
            }
        }

        private static string[] SplitCmd(string cmd)
        {
            List<string> argsList = new List<string>();
            string tmpArg = "";
            bool quoteStarted = false;

            // clear heading spaces
            for (int i = 0; i < cmd.Length; i++)
            {
                // detect quotes
                if (cmd[i] == '\"' && quoteStarted)  // closing quote
                {
                    quoteStarted = false;
                    continue;
                }
                else if (cmd[i] == '\"' && !quoteStarted)  // starting quote
                {
                    quoteStarted = true;
                    continue;
                }

                // store all chars in quotes
                if (quoteStarted)
                {
                    tmpArg += cmd[i];
                    continue;
                }

                // if space or tab is read
                if (cmd[i] == ' ' || cmd[i] == '\t')
                {
                    if (tmpArg.Length > 0)
                    {
                        argsList.Add(tmpArg);
                        tmpArg = "";
                        continue;
                    }
                    else
                    {
                        continue;
                    }
                }

                // regular char
                tmpArg += cmd[i];
            }

            // finish un-added arg
            if (tmpArg.Length > 0)
            {
                argsList.Add(tmpArg);
            }

            // prepare for return array
            string[] args = new string[argsList.Count];
            for (int i = 0; i < argsList.Count; i++)
            {
                args[i] = argsList[i];
            }

            return args;
        }

        private string GetReturn(string cmd)
        {
            // split cmd
            string[] argv = SplitCmd(cmd);

            // get argc
            int argc = argv.Length;
            if (argc == 0)
            {
                return "";
            }

            // parse argc and argv
            if (Commands.Cmd.ContainsKey(argv[0]))
            {
                Commands.disk = disk;
                Commands.cmdWindow = this;
                return Commands.Cmd[argv[0]].commandBehavior(argc, argv);
            }
            else
            {
                return "\"" + argv[0] + "\"" + " is not a valid command. Use \"help\" to see valid commands.";
            }
        }

        private void ExecCmd(string cmd)
        {
            // display reterned text
            string ret = GetReturn(cmd);
            if (ret == "")
            {
                richTextBox.AppendText(GeneratePrompt());
            }
            else
            {
                richTextBox.AppendText(ret + "\n" + GeneratePrompt());
            }

            // process special option code
            switch (specialOpCode)
            {
                case 1:  // Close Code
                    this.Close();
                    break;
                case 2:  // clean screen
                    {
                        //Get the height of the text area.
                        int height = TextRenderer.MeasureText(richTextBox.Text, richTextBox.Font).Height;
                        //rate = visible height / Total height.
                        float rate = (1.0f * richTextBox.Height) / height;
                        //Get visible lines.
                        int visibleLines = (int)(richTextBox.Lines.Length * rate);
                        int pos = richTextBox.Text.Length;
                        String nls = "";
                        for (int i = 0; i < visibleLines; i++)
                        {
                            nls += "\n";
                        }
                        richTextBox.AppendText(nls);
                        richTextBox.SelectionStart = pos;
                        richTextBox.SelectionLength = visibleLines;
                        richTextBox.SelectedText = "";
                        richTextBox.ReadOnly = false;
                    }
                    break;
                case 3:  // clear screen
                    richTextBox.Text = "";
                    richTextBox.AppendText(GeneratePrompt());
                    richTextBox.ReadOnly = false;
                    break;
            }
            specialOpCode = 0;  // reset special code
            charEntered = 0;
        }

        private void richTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
        }

        private void richTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)Keys.Enter)
            {
                e.SuppressKeyPress = true;
                richTextBox.SelectionStart = richTextBox.Text.Length;
                richTextBox.AppendText("\n");

                cmd = richTextBox.Text.Substring(richTextBox.Text.Length - charEntered, charEntered - 1);

                // record entered command
                if (cmd.Length > 0)
                {
                    if (cmdRecorder.Count == 0)
                    {
                        cmdRecorder.Add(cmd);
                    }
                    else if (cmd != cmdRecorder[cmdRecorder.Count - 1])
                    {
                        cmdRecorder.Add(cmd);
                    }
                }

                // reset cmdRecorderIndex to 0
                cmdRecorderIndex = -1;

                // execute command
                ExecCmd(cmd);
            }
            else if (e.KeyValue == (char)Keys.Back)
            {
                if (charEntered <= 0 || ((richTextBox.SelectionStart == richTextBox.Text.Length - charEntered) && richTextBox.SelectedText.Length == 0))
                {
                    System.Media.SystemSounds.Exclamation.Play();
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyValue == (char)Keys.Left)
            {
                if (richTextBox.SelectionStart <= richTextBox.Text.Length - charEntered)
                {
                    System.Media.SystemSounds.Exclamation.Play();
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyValue == (char)Keys.Right)
            {
                if (richTextBox.SelectionStart <= richTextBox.Text.Length - charEntered - 1)
                {
                    System.Media.SystemSounds.Exclamation.Play();
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                IDataObject clipboardData = Clipboard.GetDataObject();
                if (clipboardData.GetDataPresent(DataFormats.UnicodeText))
                {
                    richTextBox.AppendText((string)clipboardData.GetData(DataFormats.UnicodeText));
                }
            }
            else if (e.KeyValue == (char)Keys.Up)
            {
                e.SuppressKeyPress = true;
                if (cmdRecorder.Count == 0)
                {
                    return;
                }
                if (cmdRecorderIndex != cmdRecorder.Count - 1)
                {
                    cmdRecorderIndex++;
                }
                if (cmdRecorderIndex <= cmdRecorder.Count - 1)
                {
                    richTextBox.SelectionStart = richTextBox.Text.Length - charEntered;
                    richTextBox.SelectionLength = charEntered;
                    richTextBox.SelectedText = cmdRecorder[cmdRecorder.Count - 1 - cmdRecorderIndex];
                    //richTextBox.Text = richTextBox.Text.Substring(0, richTextBox.Text.Length - charEntered) + cmdRecorder[cmdRecorderIndex];
                    //richTextBox.SelectionStart = richTextBox.Text.Length;
                }
            }
            else if (e.KeyValue == (char)Keys.Down)
            {
                e.SuppressKeyPress = true;
                if (cmdRecorder.Count == 0)
                {
                    return;
                }
                if (cmdRecorderIndex >= 0)
                {
                    cmdRecorderIndex--;
                }
                if (cmdRecorderIndex == -1)
                {
                    richTextBox.SelectionStart = richTextBox.Text.Length - charEntered;
                    richTextBox.SelectionLength = charEntered;
                    richTextBox.SelectedText = "";
                }
                else if (cmdRecorderIndex <= cmdRecorder.Count - 1)
                {
                    richTextBox.SelectionStart = richTextBox.Text.Length - charEntered;
                    richTextBox.SelectionLength = charEntered;
                    richTextBox.SelectedText = cmdRecorder[cmdRecorder.Count - 1 - cmdRecorderIndex];
                }
            }
            else if (e.KeyValue == (char)Keys.Tab)
            {
                e.SuppressKeyPress = true;

                int retCode;

                if (disk.status != 3)
                {
                    e.Handled = true;
                    return;
                }

                // get current input segment
                string currCmd = richTextBox.Text.Substring(richTextBox.Text.Length - charEntered);
                string currSeg;
                string[] currSegs = currCmd.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if (currSegs.Length == 0)
                {
                    e.Handled = true;
                    return;
                }
                else if (currSegs.Length == 1)
                {
                    if (currCmd.Last() == ' ')
                    {
                        currSeg = "";
                    }
                    else
                    {
                        //currSeg = currSegs[0];
                        e.Handled = true;
                        return;
                    }
                }
                else
                {
                    currSeg = currSegs.Last();
                }

                // get parent path and undone name
                Tuple<string, string> t = PFSService.IsolatePath(currSeg);
                string parentPath;
                string undoneName;
                if (t == null)
                {
                    parentPath = ".";
                    undoneName = "";
                }
                else
                {
                    parentPath = t.Item1;
                    undoneName = t.Item2;
                    if (currSeg.Length > 0 && currSeg[currSeg.Length - 1] == '/')
                    {
                        parentPath += "/" + undoneName;
                        undoneName = "";
                    }
                }
                int undoneNameLength = undoneName.Length;

                // get fr list
                FileTable parentFT;
                List<Tuple<FileRecord, FileTable>> list;
                retCode = disk.GetFileTable(parentPath, disk.currentPathFID, out parentFT);
                if (retCode != 0)
                {
                    e.Handled = true;
                    return;
                }
                retCode = disk.ListFiles(parentFT, out list);

                // get similar list
                List<string> similarList = new List<string>();
                for (int i = 2; i < list.Count; i++)  // skip dot and dotdot
                {
                    if (list[i].Item1.filename.Length >= undoneNameLength)
                    {
                        if (list[i].Item1.filename.Substring(0, undoneNameLength) == undoneName)
                        {
                            if (list[i].Item2.propertyDirectory)
                            {
                                similarList.Add(list[i].Item1.filename + "/");
                            }
                            else
                            {
                                similarList.Add(list[i].Item1.filename);
                            }
                        }
                    }
                }

                // display similar strings
                if (similarList.Count == 0)
                {
                    if (list.Count == 1)
                    {
                        richTextBox.AppendText(list[0].Item1.filename.Substring(undoneName.Length));
                    }
                    else
                    {
                        e.Handled = true;
                        return;
                    }
                }
                else if (similarList.Count == 1)  // display directly
                {
                    if (similarList[0].Contains(' '))
                    {
                        // add quotes
                        richTextBox.SelectionStart = richTextBox.Text.Length - currSeg.Length;
                        richTextBox.SelectedText = "\"";
                        richTextBox.SelectionStart = richTextBox.Text.Length;
                        richTextBox.AppendText(similarList[0].Substring(undoneName.Length) + "\"");
                    }
                    else
                    {
                        richTextBox.AppendText(similarList[0].Substring(undoneName.Length));
                    }
                }
                else  // display all possible strings
                {
                    // find similars level 2
                    string similarL2 = "";
                    for (int i = 0; i < similarList[0].Length; i++)
                    {
                        for (int j = 1; j < similarList.Count; j++)
                        {
                            if (i > similarList[j].Length - 1)
                            {
                                goto LBL_Done_Search;
                            }
                            if (similarList[0][i] != similarList[j][i])
                            {
                                goto LBL_Done_Search;
                            }
                            if (j == similarList.Count - 1)
                            {
                                similarL2 += similarList[0][i];
                            }
                        }
                    }
                    LBL_Done_Search:

                    if (similarL2.Length != 0 && similarL2 != undoneName)  // has more common string
                    {
                        if (similarL2.Contains(' '))
                        {
                            // add quotes
                            richTextBox.SelectionStart = richTextBox.Text.Length - currSeg.Length;
                            richTextBox.SelectedText = "\"";
                            richTextBox.SelectionStart = richTextBox.Text.Length;
                            richTextBox.AppendText(similarL2.Substring(undoneName.Length) + "\"");
                        }
                        else
                        {
                            richTextBox.AppendText(similarL2.Substring(undoneName.Length));
                        }
                    }
                    else  // no common string
                    {
                        //show all similars
                        richTextBox.AppendText("\n");
                        for (int i = 0; i < similarList.Count; i++)
                        {
                            richTextBox.AppendText(similarList[i].PadLeft(5 + similarList[i].Length));
                        }
                        richTextBox.AppendText("\n" + GeneratePrompt());
                        charEntered = 0;
                        richTextBox.AppendText(currCmd);
                    }
                }
            }
        }

        private void richTextBox_TextChanged(object sender, EventArgs e)
        {
            if (oldText.Length == 0)
            {
                return;
            }
            // process new characters
            if (richTextBox.Text.Length > oldText.Length && oldText.Length > 0)
            {
                charEntered += richTextBox.Text.Length - oldText.Length;
            }
            else if (richTextBox.Text.Length < oldText.Length && oldText.Length > 0)
            {
                charEntered -= oldText.Length - richTextBox.Text.Length;
            }

            // update old content
            oldText = richTextBox.Text;
        }

        private void richTextBox_Enter(object sender, EventArgs e)
        {
            oldText = richTextBox.Text;
        }
    }

    public class CommandDetails
    {
        public string commandName;
        public string commandDescription;
        public string commandUsage;
        public delegate string CommandBehavior(int argc, string[] argv);
        public CommandBehavior commandBehavior;
        public CommandDetails(string commandName, string commandDescription, string commandUsage, CommandBehavior commandBehavior)
        {
            this.commandName = commandName;
            this.commandDescription = commandDescription;
            this.commandUsage = commandUsage;
            this.commandBehavior = commandBehavior;
        }
    }

    public class Commands
    {
        public static Disk disk;
        public static CommandLineWindow cmdWindow;
        public static SortedDictionary<string, CommandDetails> Cmd = new SortedDictionary<string, CommandDetails> {
            {
                "sp",
                new CommandDetails("sp",
                                   "Display the simplified path.",
                                   "<sp> <Path>",
                                   sp)
            },
            {
                "help",
                new CommandDetails("help",
                                   "Display all commands or a specific command's description and usage.",
                                   "<help> [Command Name]",
                                   help)
            },
            {
                "ctsc",
                new CommandDetails("ctsc",
                                   "Create a new Security Certification (SC) File.",
                                   "<ctsc> [<-k> <Key Filepath>] <SC Filepath>",
                                   ctsc)
            },
            {
                "ctdisk",
                new CommandDetails("ctdisk",
                                   "Create a new Disk",
                                   "<ctdisk> <Disk Filepath> <Disk Name> <Block Size ID> <Disk Size in Byte> [SC Filepath]",
                                   ctdisk)
            },
            {
                "load",
                new CommandDetails("load",
                                   "Load a disk.",
                                   "<load> <Disk Filepath> [<-k> <SC Filepath>] <User Name> [Password]",
                                   load)
            },
            {
                "ls",
                new CommandDetails("ls",
                                   "List file(s) and directories.",
                                   "<list> [<->[a][l][h]] [Path]",
                                   ls)
            },
            {
                "cd",
                new CommandDetails("cd",
                                   "Change the current active directory.",
                                   "<cd> <Directory Path>",
                                   cd)
            },
            {
                "ctf",
                new CommandDetails("ctf",
                                   "Create a new empty file",
                                   "<ctf> <Filepath>",
                                   ctf)
            },
            {
                "ctd",
                new CommandDetails("ctd",
                                   "Create a new empty directory",
                                   "<ctd> <Filepath>",
                                   ctd)
            },
            {
                "delf",
                new CommandDetails("delf",
                                   "Delete a file",
                                   "<delf> <Filepath>",
                                   delf)
            },
            {
                "deld",
                new CommandDetails("deld",
                                   "Delete a directory",
                                   "<deld> [-R] <Filepath>",
                                   deld)
            },
            {
                "diskinfo",
                new CommandDetails("diskinfo",
                                   "display disk infomation",
                                   "<diskinfo>",
                                   diskinfo)
            },
            {
                "info",
                new CommandDetails("info",
                                   "display file or directory infomation",
                                   "<info> [Filepath]",
                                   info)
            },
            {
                "isopath",
                new CommandDetails("isopath",
                                   "display the parent path and file name of a path",
                                   "<isopath> <Filepath>",
                                   isopath)
            },
            {
                "move",
                new CommandDetails("move",
                                   "Move a file or directory to a new place",
                                   "<rename> <Filepath> <New Filepath>",
                                   move)
            },
            {
                "ul",
                new CommandDetails("ul",
                                   "Upload a file from disk to external storage",
                                   "<ul> <Source Filepath> <Destination Filepath>",
                                   ul)
            },
            {
                "ext",
                new CommandDetails("ext",
                                   "Extract a file from external storage to disk",
                                   "<ext> <Source Filepath> <Destination Filepath>",
                                   ext)
            },
            {
                "rend",
                new CommandDetails("rend",
                                   "Rename the disk name.",
                                   "<rend> <New disk name>",
                                   rend)
            },
            {
                "update",
                new CommandDetails("update",
                                   "Rename the disk name.",
                                   "<update> <Source Filepath> <Destination Filepath>",
                                   update)
            },
            {
                "usr",
                new CommandDetails("usr",
                                   "show a user's infomation.",
                                   "<usr> [<UID> | <User Name>]",
                                   usr)
            },
            {
                "renusr",
                new CommandDetails("renusr",
                                   "Rename the user name.",
                                   "<renusr> <New User Name> [Password]",
                                   renusr)
            },
            {
                "grp",
                new CommandDetails("grp",
                                   "Show all groups or a group's infomation.",
                                   "<grp> [<GID> | <Group Name>]",
                                   grp)
            },
        };

        static string help(int argc, string[] argv)
        {
            if (argc == 1)
            {
                string list = string.Empty;
                foreach (var item in Cmd.OrderBy(key => key.Key))
                {
                    list += item.Value.commandName + "  -  " + item.Value.commandDescription + "\n    Usage: " + item.Value.commandUsage + "\n";
                }
                return list.Remove(list.Length - 1);
            }
            else if (argc == 2)
            {
                if (Cmd.ContainsKey(argv[1]))
                {
                    return Cmd[argv[1]].commandName + "  -  " + Cmd[argv[1]].commandDescription + "\n    Usage: " + Cmd[argv[1]].commandUsage;
                }
                else
                {
                    return "\"" + argv[1] + "\" is not a valid command. Use \"help\" to see valid commands.";
                }
            }
            else
            {
                return "    Usage: " + Cmd["help"].commandUsage;
            }
        }

        static string ctsc(int argc, string[] argv)
        {
            byte[] SCK = null;
            int retCode = 0;
            string savetoPath = "";

            if (argc == 2)
            {
                savetoPath = argv[1];
                retCode = PFSKernel.PFSService.CreateSecurityCertification(savetoPath, ref SCK);
            }
            else if ((argc == 4) && (argv[1] == "-k"))
            {
                SCK = new byte[48];
                savetoPath = argv[3];

                // read SCK File
                try
                {
                    FileStream SCKFile = new FileStream(savetoPath, FileMode.Open, FileAccess.Read, FileShare.None);
                    if (SCKFile.Length != 48)
                    {
                        return "Error: Invalid SCK text file.";
                    }
                    SCKFile.Read(SCK, 0, 48);
                    SCKFile.Close();
                    SCKFile.Dispose();
                }
                catch
                {
                    return "An error occurred when reading SCK text file.";
                }

                retCode = PFSKernel.PFSService.CreateSecurityCertification(argv[1], ref SCK);
            }
            else
            {
                return "    Usage: " + Cmd["ctsc"].commandUsage;
            }


            if (retCode != 0)
            {
                return ErrorDescription.Errors[retCode];
            }
            else
            {
                return "New Security Certification File has been created: " + savetoPath;
            }
        }

        static string ctdisk(int argc, string[] argv)
        {
            int blockSizeID;
            ulong diskSize;
            byte[] SCK = null;
            int retCode;

            if (argc == 5 || argc == 6)
            {
                // check block size id
                try
                {
                    blockSizeID = int.Parse(argv[3]);
                }
                catch
                {
                    return "Error: \"" + argv[3] + "\"" + " is not a valid block size ID.";
                }
                if (blockSizeID < 0 && blockSizeID > 13)
                {
                    return "Error: Block size ID " + "\"" + argv[3] + "\"" + " is not in [0,13].";
                }

                // check disk size
                try
                {
                    diskSize = ulong.Parse(argv[4]);
                }
                catch
                {
                    return "Error: \"" + argv[4] + "\"" + " is not a valid block size ID.";
                }

                // set SCK file
                if (argc == 6)
                {
                    // get SCK from SC file
                    retCode = PFSService.GetSCKFromSC(argv[5], out SCK);
                    if (retCode != 0)
                    {
                        return ErrorDescription.Errors[retCode];
                    }
                }
            }
            else
            {
                return "    Usage: " + Cmd["ctdisk"].commandUsage;
            }

            // use createDisk function
            retCode = PFSService.CreateDisk(argv[1], argv[2], (PFSService.BlockSize)blockSizeID, diskSize, SCK);
            if (retCode != 0)
            {
                return ErrorDescription.Errors[retCode];
            }

            return "Disk is successfully created: " + argv[1] + "\nDisk Name: " + argv[2] + "\nDisk Size: " + PFSService.ByteToHuman(diskSize, 2) + "\nBlock Size: " + PFSService.ByteToHuman((ulong)(Math.Pow(2, blockSizeID) * 1024), 0);

        }

        static string load(int argc, string[] argv)
        {
            int retCode;
            //< load > < Disk Filepath > [< -k > < SC Filepath >] < User Name > [Password]
            if (argc == 3)
            {
                retCode = disk.LoadDisk(argv[1], null, argv[2], null);
            }
            else if (argc == 4)
            {
                retCode = disk.LoadDisk(argv[1], null, argv[2], argv[3]);
            }
            else if (argc == 5 && argv[2] == "-k")
            {
                retCode = disk.LoadDisk(argv[1], argv[3], argv[4], null);
            }
            else if (argc == 6 && argv[2] == "-k")
            {
                retCode = disk.LoadDisk(argv[1], argv[3], argv[4], argv[5]);
            }
            else
            {
                return "    Usage: " + Cmd["load"].commandUsage;
            }

            if (retCode != 0)
            {
                return "Load disk Error: " + retCode.ToString();
            }
            else
            {
                ulong used = disk.HeaderFSSize;
                ulong capacity = disk.HeaderFSCapacity;
                double usedPercentage = Math.Round((double)used / capacity * 100, 2);
                return "Disk is scuucessfully loaded.\nDisk Name: " + disk.HeaderDiskName
                    + "\nDisk Capacity: " + PFSService.ByteToHuman(capacity, 2)
                    + "\nDisk Used: " + PFSService.ByteToHuman(used, 2) + " (" + usedPercentage.ToString() + "% Used)"
                    + "\nDisk Block Size: " + PFSService.ByteToHuman((ulong)Math.Pow(2, disk.HeaderBlockSize) * 1024, 0)
                    + "\nDisk Encryption: " + disk.HeaderFlagEncryption.ToString()
                    + "\n\nUser Logged In: " + disk.currentUserName;
            }
        }

        static string loadHelper(int argc, string[] argv)
        {
            if (argc != 2)
            {
                return "Please enter a user name.";
            }

            return argv[1];
        }

        static string ls(int argc, string[] argv)
        {
            int retCode;
            string options;
            string path;
            bool option_a = false;
            bool option_l = false;
            bool option_h = false;

            if (disk.status != 3)
            {
                return "\"ls\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc == 1)  // ls
            {
                options = null;
                path = disk.currentPath;
            }
            else if (argc == 2)
            {
                if (argv[1][0] == '-')  // ls -option
                {
                    options = argv[1];
                    path = disk.currentPath;
                }
                else  // ls path
                {
                    options = null;
                    path = argv[1];
                }
            }
            else if (argc == 3 && argv[1][0] == '-')  // ls -option path
            {
                options = argv[1];
                path = argv[1];
            }
            else  // invalid command
            {
                return "    Usage: " + Cmd["ls"].commandUsage;
            }

            // analyze options
            if (options != null)
            {
                if (argv[1].Length < 2 || argv[1].Length > 4)  // option argument should between 2 to 4 chars
                {
                    return "    Usage: " + Cmd["ls"].commandUsage;
                }
                if (argv[1].Length - argv[1].ToCharArray().Distinct().Count() != 0)  // cannot have repeated options
                {
                    return "    Usage: " + Cmd["ls"].commandUsage;
                }

                // record options
                for (int i = 1; i < options.Length; i++)
                {
                    if (options[i] == 'a')
                    {
                        option_a = true;
                    }
                    if (options[i] == 'l')
                    {
                        option_l = true;
                    }
                    if (options[i] == 'h')
                    {
                        option_h = true;
                    }
                }
            }

            // call kernel methods
            FileTable ftParent;
            List<Tuple<FileRecord, FileTable>> fileList;
            retCode = disk.GetFileTable(path, disk.currentPathFID, out ftParent);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }
            retCode = disk.ListFiles(ftParent, out fileList);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            // remove not-shown items if option_a is false
            if (!option_a)
            {
                for (int i = 0; i < fileList.Count; i++)
                {
                    if (fileList[i].Item2.propertyHidden || fileList[i].Item2.propertySystem || fileList[i].Item1.filename == "." || fileList[i].Item1.filename == "..")
                    {
                        fileList.RemoveAt(i);
                        i--;
                    }
                }
            }

            // sort list in alphabetical order
            int comparer(Tuple<FileRecord, FileTable> a, Tuple<FileRecord, FileTable> b)
            {
                return string.Compare(a.Item1.filename, b.Item1.filename);
            }
            fileList.Sort(comparer);

            // prepare return string
            string result = string.Empty;
            if (!option_l)  // no option l
            {
                for (int i = 0; i < fileList.Count; i++)
                {
                    result += fileList[i].Item1.filename.PadRight(13);
                }
            }
            else  // has option l
            {
                int listCount = fileList.Count;
                if (listCount == 0)
                {
                    return "    NO ITEM";
                }
                result += "    TOTAL " + fileList.Count.ToString() + " ITEM" + (fileList.Count == 1 ? "" : "S") + "\n";
                result += "Size".PadRight(13) + "Last Modified".PadRight(23) + "Filename\n";
                for (int i = 0; i < fileList.Count; i++)
                {
                    if (option_h)
                    {
                        result += PFSService.ByteToHuman(fileList[i].Item2.fileSize, 2).PadRight(13) + fileList[i].Item2.modifyTime.ToString().PadRight(23) + fileList[i].Item1.filename + "\n";
                    }
                    else
                    {
                        result += fileList[i].Item2.fileSize.ToString().PadRight(13) + fileList[i].Item2.modifyTime.ToString().PadRight(23) + fileList[i].Item1.filename + "\n";
                    }
                }
                result = result.Remove(result.Length - 1);
            }

            return result;
        }

        static string cd(int argc, string[] argv)
        {
            int retCode;

            // check argc
            if (argc != 2)
            {
                return "    Usage: " + Cmd["cd"].commandUsage;
            }

            retCode = disk.ChangeDirectory(argv[1]);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string ctf(int argc, string[] argv)
        {
            int retCode;

            if (disk.status != 3)
            {
                return "\"ctf\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 2)
            {
                return "    Usage: " + Cmd["ctf"].commandUsage;
            }

            // get filename and parent path
            Tuple<string, string> t = PFSService.IsolatePath(argv[1]);
            retCode = disk.CreateEmptyFile(t.Item1, disk.currentPathFID, t.Item2, (ushort)Disk.FileType.File, disk.defaultNewFilePermission);

            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string ctd(int argc, string[] argv)
        {
            int retCode;

            if (disk.status != 3)
            {
                return "\"ctf\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 2)
            {
                return "    Usage: " + Cmd["ctd"].commandUsage;
            }

            // get filename and parent path
            Tuple<string, string> t = PFSService.IsolatePath(argv[1]);
            retCode = disk.CreateEmptyFile(t.Item1, disk.currentPathFID, t.Item2, (ushort)Disk.FileType.Directory, disk.defaultNewDirectoryPermission);

            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string delf(int argc, string[] argv)
        {
            int retCode;

            if (disk.status != 3)
            {
                return "\"delf\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 2)
            {
                return "    Usage: " + Cmd["delf"].commandUsage;
            }

            // get filename and parent path
            Tuple<string, string> t = PFSService.IsolatePath(argv[1]);
            if (t.Item1 == "")
            {
                return "Error: Deleting \"/\" is prohibited.";
            }
            retCode = disk.DeleteFile(t.Item1, disk.currentPathFID, t.Item2);

            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string sp(int argc, string[] argv)
        {
            // check argc
            if (argc != 2)
            {
                return "    Usage: " + Cmd["sp"].commandUsage;
            }

            string result = PFSService.SimplifyPath(argv[1]);
            if (result == "")
            {
                return "    Invalid Absolute Path";
            }
            else
            {
                return result;
            }
        }

        static string deld(int argc, string[] argv)
        {
            int retCode;

            if (disk.status != 3)
            {
                return "\"deld\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc == 2)
            {
                // get parent path and file name
                Tuple<string, string> t = PFSService.IsolatePath(argv[1]);
                if (t.Item1 == "")
                {
                    return "Error: Deleting \"/\" is prohibited.";
                }
                retCode = disk.DeleteDirectory(t.Item1, disk.currentPathFID, t.Item2, false);
            }
            else if (argc == 3 && argv[1][0] == '-')
            {
                if (argv[1] == "-R")
                {
                    // get parent path and file name
                    Tuple<string, string> t = PFSService.IsolatePath(argv[2]);
                    if (t.Item1 == "")
                    {
                        return "Error: Deleting \"/\" is prohibited.";
                    }
                    retCode = disk.DeleteDirectory(t.Item1, disk.currentPathFID, t.Item2, true);
                }
                else
                {
                    return "    Usage: " + Cmd["deld"].commandUsage;
                }
            }
            else
            {
                return "    Usage: " + Cmd["deld"].commandUsage;
            }

            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string diskinfo(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"diskinfo\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 1)
            {
                return "    Usage: " + Cmd["diskinfo"].commandUsage;
            }

            string result = "    DISK INFORMATION\n";

            ulong used = disk.HeaderFSSize;
            ulong capacity = disk.HeaderFSCapacity;
            double usedPercentage = Math.Round((double)used / capacity * 100, 2);
            result += "Disk Name: " + disk.HeaderDiskName
                + "\nDisk Capacity: " + PFSService.ByteToHuman(capacity, 2)
                + "\nDisk Used: " + PFSService.ByteToHuman(used, 2) + " (" + usedPercentage.ToString() + "% Used)"
                + "\nDisk Block Size: " + PFSService.ByteToHuman((ulong)Math.Pow(2, disk.HeaderBlockSize) * 1024, 0)
                + "\nDisk Encryption: " + disk.HeaderFlagEncryption.ToString();

            return result;
        }

        static string info(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"info\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            string filepath;
            if (argc == 1)
            {
                filepath = disk.currentPath;
            }
            else if (argc == 2)
            {
                filepath = argv[1];
            }
            else
            {
                return "    Usage: " + Cmd["info"].commandUsage;
            }

            int retCode;
            FileTable tmpFT;
            string result;

            string[] pathSeg = PFSService.SplitPath(filepath);
            if (pathSeg.Length == 0)
            {
                pathSeg = new string[] { "/" };
            }
            retCode = disk.GetFileTable(filepath, disk.currentPathFID, out tmpFT);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            if (tmpFT.propertyDirectory)
            {
                result = "Directory: ".PadLeft(16) + pathSeg[pathSeg.Length - 1] + "\n";
            }
            else
            {
                result = "File: ".PadLeft(16) + pathSeg[pathSeg.Length - 1] + "\n";
            }

            result += "Size: ".PadLeft(16) + tmpFT.fileSize.ToString() + " B  (" + PFSService.ByteToHuman(tmpFT.fileSize, 2) + ")\n"
                + "Created: ".PadLeft(16) + tmpFT.createTime.ToString() + "\n"
                + "Modified: ".PadLeft(16) + tmpFT.modifyTime.ToString() + "\n"
                + "Accessed: ".PadLeft(16) + tmpFT.accessTime.ToString() + "\n"
                + "Hidden: ".PadLeft(16) + tmpFT.propertyHidden.ToString() + "\n"
                + "Encryption: ".PadLeft(16) + tmpFT.propertyEncryption.ToString() + "\n";

            return result;
        }

        static string isopath(int argc, string[] argv)
        {
            if (argc != 2)
            {
                return "    Usage: " + Cmd["isopath"].commandUsage;
            }

            Tuple<string, string> t = PFSService.IsolatePath(argv[1]);

            if (t == null)
            {
                return "Invalid path";
            }

            return "Parent path: ".PadLeft(13) + t.Item1 + "\n" + "File name: ".PadLeft(13) + t.Item2;
        }

        static string move(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"move\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 3)
            {
                return "    Usage: " + Cmd["move"].commandUsage;
            }

            // get 2 path segaments
            Tuple<string, string> t1 = PFSService.IsolatePath(argv[1]);
            Tuple<string, string> t2 = PFSService.IsolatePath(argv[2]);

            // do kernal call
            int retCode = disk.MoveFile(t1.Item1, disk.currentPathFID, t1.Item2, t2.Item1, disk.currentPathFID, t2.Item2);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string ul(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"ul\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 3)
            {
                return "    Usage: " + Cmd["ul"].commandUsage;
            }

            // get path segaments
            Tuple<string, string> t = PFSService.IsolatePath(argv[2]);

            // do kernal call
            int retCode = disk.UploadFile(argv[1], t.Item1, disk.currentPathFID, t.Item2, (ushort)Disk.FileType.File, disk.defaultNewFilePermission);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string ext(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"ext\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 3)
            {
                return "    Usage: " + Cmd["ext"].commandUsage;
            }

            // do kernal call
            int retCode = disk.ExtractFile(disk.currentPathFID, argv[1], argv[2]);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string rend(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"rend\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 2)
            {
                return "    Usage: " + Cmd["rend"].commandUsage;
            }

            int retCode = disk.RenameDisk(argv[1]);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "Disk name has been changed to: " + argv[1];
        }

        static string update(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"update\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            if (argc != 3)
            {
                return "    Usage: " + Cmd["update"].commandUsage;
            }

            // get path segaments
            Tuple<string, string> t = PFSService.IsolatePath(argv[2]);

            // do kernal call
            int retCode = disk.UpdateFile(argv[1], t.Item1, disk.currentPathFID, t.Item2, (ushort)Disk.FileType.File, disk.defaultNewFilePermission);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string renusr(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"renusr\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            int retCode;

            if (argc == 2)  // no passward
            {
                retCode = disk.ChangeUserDetails(disk.currentUserID, null, argv[1], null, null);
            }
            else if (argc == 3)  // has password
            {
                retCode = disk.ChangeUserDetails(disk.currentUserID, argv[2], argv[1], null, null);
            }
            else  // invalid argument count
            {
                return "Usage: " + Cmd["renusr"].commandUsage;
            }

            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }

            return "";
        }

        static string usr(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"usr\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            int retCode;
            Dictionary<string, User> tmpUsers;
            Dictionary<uint, Group> tmpGroups;
            retCode = disk.GetUsers(out tmpUsers);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }
            retCode = disk.GetGroups(out tmpGroups);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }
            if (argc == 1)
            {
                string result = "User Name: ".PadLeft(20) + disk.currentUserName + "\n" +
                    "User ID: ".PadLeft(20) + tmpUsers[disk.currentUserName].UserID + "\n" +
                    "Description: ".PadLeft(20) + tmpUsers[disk.currentUserName].Description + "\n" +
                    "Created: ".PadLeft(20) + tmpUsers[disk.currentUserName].CreatedTime.ToString() + "\n" +
                    "Last Logged In: ".PadLeft(20) + tmpUsers[disk.currentUserName].LastLoggedInTime.ToString() + "\n" +
                    "Belongs to Groups: ".PadLeft(20);
                for (int i = 0; i < tmpUsers[disk.currentUserName].GroupIDs.Length; i++)
                {
                    if (tmpUsers[disk.currentUserName].GroupIDs[i] != 0)
                    {
                        result += "\n" + tmpGroups[tmpUsers[disk.currentUserName].GroupIDs[i]].GroupName.PadLeft(23);
                    }
                }
                return result;
            }
            else if (argc == 2)
            {
                uint uid;
                if (uint.TryParse(argv[1], out uid))  // uid is entered
                {
                    // get desired uid's user
                    List<User> users = tmpUsers.Where(x => x.Value.UserID == uid).Select(x => x.Value).ToList();
                    if (users.Count == 0)
                    {
                        return "Error: UID does not match any accounts.";
                    }
                    string result = "User Name: ".PadLeft(20) + users[0].Username + "\n" +
                            "User ID: ".PadLeft(20) + uid + "\n" +
                            "Description: ".PadLeft(20) + users[0].Description + "\n" +
                            "Created: ".PadLeft(20) + users[0].CreatedTime.ToString() + "\n" +
                            "Last Logged In: ".PadLeft(20) + users[0].LastLoggedInTime.ToString() + "\n" +
                            "Belongs to Groups:".PadLeft(20);
                    for (int i = 0; i < users[0].GroupIDs.Length; i++)
                    {
                        if (users[0].GroupIDs[i] != 0)
                        {
                            result += "\n" + tmpGroups[users[0].GroupIDs[i]].GroupName.PadLeft(23);
                        }
                    }
                    return result;
                }
                else  // username is entered
                {
                    // check if username exists
                    if (tmpUsers.ContainsKey(argv[1]))
                    {
                        string result = "User Name: ".PadLeft(20) + argv[1] + "\n" +
                            "User ID: ".PadLeft(20) + tmpUsers[argv[1]].UserID + "\n" +
                            "Description: ".PadLeft(20) + tmpUsers[argv[1]].Description + "\n" +
                            "Created: ".PadLeft(20) + tmpUsers[argv[1]].CreatedTime.ToString() + "\n" +
                            "Last Logged In: ".PadLeft(20) + tmpUsers[argv[1]].LastLoggedInTime.ToString() + "\n" +
                            "Belongs to Groups:".PadLeft(20);
                        for (int i = 0; i < tmpUsers[argv[1]].GroupIDs.Length; i++)
                        {
                            if (tmpUsers[argv[1]].GroupIDs[i] != 0)
                            {
                                result += "\n" + tmpGroups[tmpUsers[argv[1]].GroupIDs[i]].GroupName.PadLeft(23);
                            }
                        }
                        return result;
                    }
                    else
                    {
                        return "Error: User Name does not match any accounts.";
                    }
                }
            }
            else
            {
                return "Usage: " + Cmd["usr"].commandUsage;
            }
        }

        static string grp(int argc, string[] argv)
        {
            if (disk.status != 3)
            {
                return "\"grp\" is not allowed until a disk is loaded. Use \"help load\" to see details.";
            }

            int retCode;
            Dictionary<string, User> tmpUsers;
            Dictionary<uint, Group> tmpGroups;
            retCode = disk.GetUsers(out tmpUsers);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }
            retCode = disk.GetGroups(out tmpGroups);
            if (retCode != 0)
            {
                return "Error: " + retCode.ToString();
            }
            if (argc == 1)
            {
                // sort the group names
                List<KeyValuePair<uint, Group>> groupList = tmpGroups.ToList();
                groupList.Sort((list1, list2) => list1.Key.CompareTo(list2.Key));
                string result = "Groups on this disk: ".PadLeft(20);
                for (int i = 0; i < groupList.Count; i++)
                {
                    result += "\n    " + groupList[i].Value.GroupName;
                }
                return result;
            }
            else if (argc == 2)
            {
                uint gid;
                if (uint.TryParse(argv[1], out gid))  // gid is entered
                {
                    // check if gid is valid
                    if (!tmpGroups.ContainsKey(gid))
                    {
                        return "Error: GID does not match any Groups.";
                    }

                    string result = "Group Name: ".PadLeft(20) + tmpGroups[gid].GroupName + "\n" +
                            "Group ID: ".PadLeft(20) + gid + "\n" +
                            "Description: ".PadLeft(20) + tmpGroups[gid].Description + "\n" +
                            "Created: ".PadLeft(20) + tmpGroups[gid].CreatedTime.ToString() + "\n";
                    // get members
                    List<User> users = tmpUsers.Where(x => x.Value.GroupIDs.Contains(gid)).Select(x => x.Value).ToList();
                    result += ("Has " + users.Count.ToString() + " Member(s)").PadLeft(20);
                    if (users.Count == 0)
                    {
                        return result;
                    }
                    for (int i = 0; i < users.Count; i++)
                    {
                        result += "\n" + users[i].Username.PadLeft(23);
                    }
                    return result;
                }
                else  // groupname is entered
                {
                    // get group which has current groupname
                    List<Group> group = tmpGroups.Where(x => x.Value.GroupName == argv[1]).Select(x => x.Value).ToList();

                    // check if username exists
                    if (group.Count != 0)
                    {
                        string result = "Group Name: ".PadLeft(20) + group[0].GroupName + "\n" +
                            "Group ID: ".PadLeft(20) + group[0].GroupID + "\n" +
                            "Description: ".PadLeft(20) + group[0].Description + "\n" +
                            "Created: ".PadLeft(20) + group[0].CreatedTime.ToString() + "\n";
                        // get members
                        List<User> users = tmpUsers.Where(x => x.Value.GroupIDs.Contains(group[0].GroupID)).Select(x => x.Value).ToList();
                        result += ("Has " + users.Count.ToString() + " Member(s)").PadLeft(20);
                        if (users.Count == 0)
                        {
                            return result;
                        }
                        for (int i = 0; i < users.Count; i++)
                        {
                            result += "\n" + users[i].Username.PadLeft(23);
                        }
                        return result;
                    }
                    else
                    {
                        return "Error: Group Name does not match any Groups.";
                    }
                }
            }
            else
            {
                return "Usage: " + Cmd["grp"].commandUsage;
            }
        }
    }

}
