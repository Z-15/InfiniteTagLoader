﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InfiniteTagLoader.Controls;
using System.Threading;

namespace InfiniteTagLoader
{
    // TODO:
    // Figure out how to load tags from other modules.
    // Make the search bar work.
    // Maybe find a way to seperate tag names by the module they are in. It'll be easier to know which tags you can load that way.

    public partial class MainWindow : Window
    {
        #region Control Buttons
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CommandBinding_Executed_Minimize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void CommandBinding_Executed_Maximize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
            RestoreButton.Visibility = Visibility.Visible;
            MaximizeButton.Visibility = Visibility.Collapsed;
        }

        private void CommandBinding_Executed_Restore(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
            RestoreButton.Visibility = Visibility.Collapsed;
            MaximizeButton.Visibility = Visibility.Visible;
        }

        private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        private void Move_Window(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            GetTagNames();
        }

        private void GetTagNames()
        {
            foreach (string line in File.ReadAllLines(@".\Files\tagnames.txt"))
            {

                string[] temp = line.Split(":");
                string tagID = temp[0].Trim();

                if (!tagIDs.ContainsKey(tagID) && tagID.Length > 1)
                {
                    string tagPath = temp[1].Trim();
                    string tagName = tagPath.Split("\\").Last().Trim();
                    string tagFolder = temp[1].Split('.')[1].Trim();

                    TagInfo t = new TagInfo();
                    t.tagID = tagID;
                    t.tagName = tagName;
                    t.tagFolder = tagFolder;
                    t.tagPath = tagPath;
                    tagIDs.Add(tagID, t);

                    if (tagFolders.ContainsKey(tagFolder))
                    {
                        TagType tagType = tagFolders[tagFolder];
                        tagType.tags.Add(t);
                    }
                    else
                    {
                        TagType tagType = new TagType();
                        tagType.type = tagFolder;
                        tagType.tags.Add(t);
                        tagType.tags.Sort();
                        tagFolders.Add(tagFolder, tagType);

                    }


                }
            }
        }



        public class TagInfo
        {
            public string tagID = "";
            public string tagName = "";
            public string tagPath = "";
            public string tagFolder = "";
        }

        public class TagType
        {
            public string type = "";
            public List<TagInfo> tags = new List<TagInfo>();
        }

        #region Module Reading and Writing
        private int tagListStart = 0;
        private string moduleFileName = "";
        private FileStream moduleStream;
        private Dictionary<int, string> tagList = new Dictionary<int, string>();
        private SortedDictionary<string, TagInfo> tagIDs = new SortedDictionary<string, TagInfo>();
        private SortedDictionary<string, TagType> tagFolders = new SortedDictionary<string, TagType>();

        private async void OpenModuleClick(object sender, RoutedEventArgs e)
        {
            if (moduleStream != null)
            {
                Reset();
            }
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Module File (*.module)|*.module";

            if (ofd.ShowDialog() == true)
            {
                moduleFileName = ofd.FileName.Split('\\').Last();
                moduleStream = new FileStream(ofd.FileName, FileMode.Open, FileAccess.ReadWrite);

                WriteStatus("Scanning for tag list...");
                Thread.Sleep(100);

                bool result = false;
                Task task1 = new Task(() =>
                {
                    result = TagListScan();
                });
                task1.Start();
                await task1;
                task1.Dispose();

                if (result)
                {
                    WriteStatus("Loading controls...");
                    Thread.Sleep(100);

                    tagListStart += 36;
                    moduleStream.Position = tagListStart;
                    int tagCount = 0;
                    Task task2 = new Task(() =>
                    {
                        tagCount = GetTagList();
                    });
                    task2.Start();
                    await task2;
                    task2.Dispose();
                    CreateControls();

                    WriteStatus( tagCount + " tags loaded from " + moduleFileName + "!");
                }  
                else
                    WriteStatus("Failed to open " + moduleFileName + "!");
            }
        }

        private void CloseModuleClick(object sender, RoutedEventArgs e)
        {
            Reset();
            WriteStatus("Module Closed!");
        }

        private void Reset()
        {
            moduleStream.Close();
            moduleStream.Dispose();
            moduleFileName = "";
            tagListStart = 0;
            tagList.Clear();
            TagViewer.Children.Clear();
            GC.Collect();
        }

        private bool TagListScan()
        {
            tagListStart = 0;
            bool found = false;
            while (!found)
            {
                if (moduleStream.ReadByte() == 0x8C)
                {
                    tagListStart = (int)moduleStream.Position;
                    if (moduleStream.ReadByte() == 0x06)
                    {
                        if (moduleStream.ReadByte() == 0x0)
                        {
                            moduleStream.Position = tagListStart + 24;
                            if (moduleStream.ReadByte() == 0xFF)
                                found = true;
                        }
                        else
                            moduleStream.Position = tagListStart;
                    }
                    else
                        moduleStream.Position = tagListStart;
                }

                if (moduleStream.Position == moduleStream.Length - 1)
                    break;
            }
            return found;
        }
        #endregion

        #region GUI
        private int GetTagList()
        {
            int check = 0;
            int count = 0;

            for (int i = 0; i < 10000; i++)
            {
                byte[] buffer = new byte[4];
                moduleStream.Read(buffer, 0, buffer.Length);
                string tagID = buffer[0].ToString("X2") + buffer[1].ToString("X2") + buffer[2].ToString("X2") + buffer[3].ToString("X2");

                
                if (tagID != "00000000")
                {
                    tagList.Add((int)moduleStream.Position - 4, tagID);
                    check = 0;
                    count++;
                }
                else
                {
                    check++;
                }

                if (check >= 4)
                    break;
            }

            return count;
        }

        private void CreateControls()
        {
            foreach (KeyValuePair<int, string> tag in tagList)
            {
                if (tagIDs.ContainsKey(tag.Value))
                {
                    TagReference tR = new TagReference();
                    TagInfo t = tagIDs[tag.Value];
                    tR.tagID.Text = tag.Value;
                    tR.tags.Items.Add(t.tagName);
                    tR.tags.SelectedIndex = 0;
                    tR.tagType.SelectionChanged += TagTypeChanged;
                    tR.tags.SelectionChanged += TagChanged;
                    tR.tags.Tag = tR;
                    tR.Tag = tag.Key;
                    
                    foreach (TagType tT in tagFolders.Values)
                    {
                        tR.tagType.Items.Add(tT.type);
                        tR.tagType.Tag = tR;
                    }

                    TagViewer.Children.Add(tR);
                }
            }
        }

        private void TagTypeChanged(object sender, RoutedEventArgs e)
        {
            TagType tT = new TagType();
            ComboBox s = (ComboBox)sender;
            TagReference tR = (TagReference)s.Tag;
            tR.tags.Items.Clear();

            if (s.SelectedItem != null)
               tT = tagFolders[s.SelectedItem.ToString()];

            foreach (TagInfo tag in tT.tags)
            {
                tR.tags.Items.Add(tag.tagID + " : " + tag.tagName);
            }

            GC.Collect();
        }

        private void TagChanged(object sender, RoutedEventArgs e)
        {
            ComboBox s = (ComboBox)sender;
            TagReference tR = (TagReference)s.Tag;
            
            if (s.SelectedItem != null)
            {
                string hexID = ((string)s.SelectedItem).Split(" : ")[0].Trim();

                moduleStream.Position = (int)tR.Tag;
                moduleStream.Write(StringToByteArray(hexID));

                moduleStream.Position = (int)tR.Tag;
                byte[] test = new byte[4];
                moduleStream.Read(test, 0, 4);
                string newID = Convert.ToHexString(test);
                tR.tagID.Text = newID;
            }
        }

        public byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        private void WriteStatus(string message)
        {
            statusText.Text = message;
        }
        #endregion
    }
}