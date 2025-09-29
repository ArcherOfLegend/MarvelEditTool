using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using MarvelData;

namespace MarvelEditTool
{
    public partial class TableEditor : Form
    {
        public TableFile tablefile;
        public List<string> tableNames;
        public bool bDisableUpdate;

        public static bool bError;
        public string ImportPath;
        public string FilePath;
        private AnmChrEntry currentAnmChrEntry;
        private bool suppressSubEntryEvents;
        private bool suppressCommandEvents;

        public TableEditor()
        {
            InitializeComponent();
            Text += ", build " + GetCompileDate();
            AELogger.Log(Text);
            bDisableUpdate = true;
            FilePath = String.Empty;
            ImportPath = String.Empty;
            try
            {
                string initResult = AnmChrSubEntry.InitCmdNames();
                if (!string.IsNullOrWhiteSpace(initResult))
                {
                    AELogger.Log(initResult);
                }
            }
            catch (Exception ex)
            {
                AELogger.Log("Failed to initialize command labels: " + ex.Message);
            }
            UpdateStructureView(null);
        }

        public static string GetCompileDate()
        {
            System.Version MyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return new DateTime(2000, 1, 1).AddDays(MyVersion.Build).AddSeconds(MyVersion.Revision * 2).ToString("MMM.dd.yyyy");
        }

        public void SaferExit()
        {
            AELogger.WriteLog();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.CloseReason == CloseReason.WindowsShutDown) return;
            if (bError) return;

            // Confirm user wants to close
            switch (MessageBox.Show(this, "Are you sure you want to close?", "Closing", MessageBoxButtons.YesNo))
            {
                case DialogResult.No:
                    e.Cancel = true;
                    break;
                default:
                    AELogger.WriteLog();
                    break;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                if (saveButton.Enabled)
                {
                    saveButton_Click(null, null);
                }
                return true;
            }
            else if (keyData == (Keys.Control | Keys.O))
            {
                if (openButton.Enabled)
                {
                    openButton_Click(null, null);
                }
                return true;
            }
            else if (keyData == (Keys.Control | Keys.T))
            {
                if (extendButton.Enabled)
                {
                    extendButton_Click(null, null);
                }
                return true;
            }
            else if (keyData == (Keys.Control | Keys.E))
            {
                if (exportButton.Enabled)
                {
                    exportButton_Click(null, null);
                }
                return true;
            }
            else if (keyData == (Keys.Control | Keys.R))
            {
                if (importButton.Enabled)
                {
                    importButton_Click(null, null);
                }
                return true;
            }
            else if (keyData == Keys.Up || keyData == Keys.Down)
            {
                RedirectToAnimBox("{" + keyData.ToString() + "}");
            }
            else if (keyData == Keys.PageDown)
            {
                RedirectToAnimBox("{pgdn}");
            }
            else if (keyData == Keys.PageUp)
            {
                RedirectToAnimBox("{pgdn}");
            }
            else if(keyData == Keys.Home || keyData == Keys.End)
            {
                if(!textBox1.Focused)
                {
                    RedirectToAnimBox("{" + keyData.ToString() + "}");
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RedirectToAnimBox(string key)
        {
            if (!animBox.Focused)
            {
                animBox.Focus();
                SendKeys.Send(key);
            }
        }

        private void openButton_Click(object sender, EventArgs e)
        {
            if (bError)
            {
                return;
            }

            using (OpenFileDialog openFile = new OpenFileDialog())
            {
                //openFile.DefaultExt = "bcm";
                // The Filter property requires a search string after the pipe ( | )
                //openFile.Filter = "BCM Files (*.bcm)|*.bcm|All files (*.*)|*.*";
                openFile.ShowDialog();
                if (openFile.FileNames.Length > 0)
                {
                    TableFile newTable = TableFile.LoadFile(openFile.FileNames[0], true);
                    int count = newTable.table.Count;
                    if (newTable == null && count != 0)
                    {
                        AELogger.Log("load failed for some reason?");
                        return;
                    }
                    tablefile = newTable;

                    SuspendLayout();
                    saveButton.Enabled = true;
                    analyzeButton.Enabled = true;
                    importButton.Enabled = false;
                    exportButton.Enabled = false;
                    openButton.Enabled = false;
                    animBox.Enabled = true;
                    extendButton.Enabled = true;
                    sizeLabel.Text = count + " entries loaded";
                    FilePath = openFile.FileNames[0];
                    RefreshData();
                    Text += " :: " + openFile.FileNames[0];
                    filenameLabel.Text = openFile.FileNames[0];
                    ResumeLayout();
                }
                else
                {
                    AELogger.Log("nothing selected!");
                }
            }
        }

        private void saveButton_Click(object sender, EventArgs ev)
        {
            if (bError)
            {
                return;
            }

            using (SaveFileDialog saveFileDialog1 = new SaveFileDialog())
            {
                //saveFileDialog1.Filter = "BCM files (*.bcm)|*.bcm|All files (*.*)|*.*";
                //saveFileDialog1.FilterIndex = 2;
                if (FilePath != String.Empty)
                {
                    try
                    {
                        saveFileDialog1.InitialDirectory = Path.GetDirectoryName(FilePath);
                        saveFileDialog1.FileName = Path.GetFileName(FilePath);
                    }
                    catch(Exception e)
                    {
                        AELogger.Log("some kind of exception setting save path from " + FilePath);
                        AELogger.Log("Exception: " + e.Message);

                        AELogger.Log("Exception: " + e.StackTrace);

                        int i = 1;
                        while (e.InnerException != null)
                        {
                            e = e.InnerException;
                            AELogger.Log("InnerException " + i + ": " + e.Message);

                            AELogger.Log("InnerException " + i + ": " + e.StackTrace);
                            i++;
                        }
                    }
                }
                saveFileDialog1.RestoreDirectory = true;

                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if (saveFileDialog1.FileNames.Length > 0)
                    {
                        FilePath = saveFileDialog1.FileNames[0];
                        tablefile.WriteFile(saveFileDialog1.FileNames[0]);
                    }
                }
            }
        }

        private void exportButton_Click(object sender, EventArgs ev)
        {
            if (animBox.SelectedIndex < 0
                ||
                animBox.SelectedIndex >= tablefile.table.Count)
            {
                return;
            }

            using (SaveFileDialog saveFileDialog1 = new SaveFileDialog())
            {
                saveFileDialog1.Title = "Export " + tablefile.table[animBox.SelectedIndex].GetFancyName();
                if (tablefile.fileExtension == "CAC")
                {
                    saveFileDialog1.Filter = "|UMVC3 Character Script File (*.mvc3anm;*.mvc3data)|*.mvc3anm;*.mvc3data|UMVC3 Loose Data (*.mvc3data)|*.mvc3data|All files (*.*)|*.*";
                }
                else if (tablefile.fileExtension == "CSP")
                {
                    saveFileDialog1.Filter = "MVC3 Special Input Command File (*.mvc3csp;*.mvc3data)|*.mvc3csp;*.mvc3data|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files (*.*)|*.*|";
                }
                else if (tablefile.fileExtension == "CBA")
                {
                    saveFileDialog1.Filter = "MVC3 Basic Input Action File (*.mvc3cba;*.mvc3data)|*.mvc3cba;*.mvc3data|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*|";
                }
                else if (tablefile.fileExtension == "CCM")
                {
                    saveFileDialog1.Filter = "MVC3 Extra Cancel Input File (*.mvc3ccm;*.mvc3data)|*.mvc3ccm;*.mvc3data|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*|";
                }
                else if (tablefile.fileExtension == "ATI")
                {
                    saveFileDialog1.Filter = "MVC3 Attack Data File (*.mvc3data;*.mvc3ati)|*.mvc3ati;*.mvc3data|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*|";
                }
                else if (tablefile.fileExtension == "CLI")
                {
                    saveFileDialog1.Filter = "MVC3 Collision File (*.mvc3cli;*.mvc3data)|*.mvc3cli;*.mvc3data|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*|";
                }
                else if (tablefile.fileExtension == "CHS")
                {
                    saveFileDialog1.Filter = "MVC3 Character Status File (*.mvc3chs;*.mvc3data)|*.mvc3chs;*.mvc3data|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*|";
                }
                else if (tablefile.fileExtension == "CPI")
                {
                    saveFileDialog1.Filter = "MVC3 Character Status File (*.mvc3chs;*.mvc3data)|*.mvc3chs;*.mvc3data|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*|";
                }
                else if (tablefile.fileExtension == "SHT")
                {
                    saveFileDialog1.Filter = "UMVC3 Projectile File (*.mvc3sht;*.mvc3data)|*.mvc3sht;*.mvc3data|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*|";
                }
                else
                {
                    saveFileDialog1.Filter = "All files (*.*)|*.*|UMVC3 Loose Data(*.mvc3data)|*.mvc3data";
                }
                if (FilePath != String.Empty)
                {
                    try
                    {
                        saveFileDialog1.InitialDirectory = Path.GetDirectoryName(ImportPath);
                    }
                    catch (Exception e)
                    {
                        AELogger.Log("some kind of exception setting save path from " + ImportPath);
                        AELogger.Log("Exception: " + e.Message);

                        AELogger.Log("Exception: " + e.StackTrace);

                        int i = 1;
                        while (e.InnerException != null)
                        {
                            e = e.InnerException;
                            AELogger.Log("InnerException " + i + ": " + e.Message);

                            AELogger.Log("InnerException " + i + ": " + e.StackTrace);
                            i++;
                        }
                    }
                }
                saveFileDialog1.FilterIndex = 2;
                saveFileDialog1.RestoreDirectory = true;

                saveFileDialog1.FileName = tablefile.table[animBox.SelectedIndex].GetFilename() + ".mvc3data";

                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if (saveFileDialog1.FileNames.Length > 0)
                    {
                        ImportPath = saveFileDialog1.FileNames[0];
                        tablefile.table[animBox.SelectedIndex].Export(saveFileDialog1.FileNames[0]);
                    }
                }
            }
        }

        private void animBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(bDisableUpdate)
            {
                return;
            }
            SuspendLayout();
            animBox.BeginUpdate();
            bDisableUpdate = true;
            importButton.Enabled = true;

            if (
                animBox.SelectedIndex >= 0
                &&
                animBox.SelectedIndex < tablefile.table.Count
                &&
                tablefile.table[animBox.SelectedIndex].bHasData
                )
            {
                textBox1.Text = tablefile.table[animBox.SelectedIndex].name;
                textBox1.Enabled = true;
                exportButton.Enabled = true;
                SetTextConcurrent(tablefile.table[animBox.SelectedIndex].GetData());
                dataTextBox.Text = BitConverter.ToString(tablefile.table[animBox.SelectedIndex].GetData()).Replace("-", "");
                dataTextBox.WordWrap = true;
                sizeLabel.Text = "size: " + tablefile.table[animBox.SelectedIndex].size;
                UpdateStructureView(tablefile.table[animBox.SelectedIndex]);
            }
            else
            {
                textBox1.Text = "";
                textBox1.Enabled = false;
                exportButton.Enabled = false;
                dataTextBox.Text = "";
                sizeLabel.Text = "size: N/A";
                UpdateStructureView(null);
            }


            bDisableUpdate = false;
            animBox.EndUpdate();
            ResumeLayout();
        }

        private void UpdateStructureView(TableEntry entry)
        {
            if (entry == null || !entry.bHasData)
            {
                currentAnmChrEntry = null;
                structureLayoutPanel.Visible = false;
                structureUnavailableLabel.Visible = true;
                structureUnavailableLabel.BringToFront();
                subEntryListBox.DataSource = null;
                commandListBox.DataSource = null;
                subEntryListBox.Enabled = false;
                commandListBox.Enabled = false;
                commandDetailTextBox.Clear();
                commandDetailLabel.Text = "Command details";
                UpdateSubEntryButtons();
                UpdateCommandButtons();
                if (entryTabControl.SelectedTab == structureTabPage)
                {
                    entryTabControl.SelectedTab = rawDataTabPage;
                }
                return;
            }

            currentAnmChrEntry = entry as AnmChrEntry;
            if (currentAnmChrEntry == null)
            {
                structureLayoutPanel.Visible = false;
                structureUnavailableLabel.Visible = true;
                structureUnavailableLabel.BringToFront();
                subEntryListBox.DataSource = null;
                commandListBox.DataSource = null;
                subEntryListBox.Enabled = false;
                commandListBox.Enabled = false;
                commandDetailTextBox.Clear();
                commandDetailLabel.Text = "Command details";
                UpdateSubEntryButtons();
                UpdateCommandButtons();
                if (entryTabControl.SelectedTab == structureTabPage)
                {
                    entryTabControl.SelectedTab = rawDataTabPage;
                }
                return;
            }

            structureLayoutPanel.Visible = true;
            structureUnavailableLabel.Visible = false;
            subEntryListBox.Enabled = true;
            commandListBox.Enabled = true;
            entryTabControl.SelectedTab = structureTabPage;
            PopulateSubEntries();
        }

        private void PopulateSubEntries(int preferredIndex = -1)
        {
            if (currentAnmChrEntry == null)
            {
                suppressSubEntryEvents = true;
                subEntryListBox.DataSource = null;
                suppressSubEntryEvents = false;
                UpdateSubEntryButtons();
                PopulateCommandList(-1);
                return;
            }

            var items = currentAnmChrEntry.subEntries.Select(sub => sub.GetName()).ToList();
            suppressSubEntryEvents = true;
            int selection = preferredIndex >= 0 ? preferredIndex : subEntryListBox.SelectedIndex;
            subEntryListBox.DataSource = null;
            subEntryListBox.DataSource = items;
            if (items.Count > 0)
            {
                if (selection < 0 || selection >= items.Count)
                {
                    selection = 0;
                }
                subEntryListBox.SelectedIndex = selection;
            }
            else
            {
                subEntryListBox.SelectedIndex = -1;
            }
            suppressSubEntryEvents = false;
            UpdateSubEntryButtons();
            PopulateCommandList(subEntryListBox.SelectedIndex);
        }

        private void PopulateCommandList(int subIndex, int preferredCommandIndex = -1)
        {
            suppressCommandEvents = true;
            if (currentAnmChrEntry == null || subIndex < 0 || subIndex >= currentAnmChrEntry.subEntries.Count)
            {
                commandListBox.DataSource = null;
                commandDetailTextBox.Clear();
                commandDetailLabel.Text = "Command details";
                commandListBox.Enabled = currentAnmChrEntry != null;
                suppressCommandEvents = false;
                UpdateCommandButtons();
                return;
            }

            var subEntry = currentAnmChrEntry.subEntries[subIndex];
            var commands = subEntry.subsubEntries.Select((_, idx) => subEntry.GetSubSubName(idx)).ToList();
            int selection = preferredCommandIndex >= 0 ? preferredCommandIndex : commandListBox.SelectedIndex;
            commandListBox.DataSource = null;
            commandListBox.DataSource = commands;
            if (commands.Count > 0)
            {
                if (selection < 0 || selection >= commands.Count)
                {
                    selection = 0;
                }
                commandListBox.SelectedIndex = selection;
            }
            else
            {
                commandListBox.SelectedIndex = -1;
            }
            suppressCommandEvents = false;
            UpdateCommandButtons();
            UpdateCommandDetails(commandListBox.SelectedIndex);
        }

        private void UpdateSubEntryButtons()
        {
            if (currentAnmChrEntry == null || currentAnmChrEntry.subEntries.Count == 0 || subEntryListBox.SelectedIndex < 0)
            {
                subEntryUpButton.Enabled = false;
                subEntryDownButton.Enabled = false;
                return;
            }

            int index = subEntryListBox.SelectedIndex;
            subEntryUpButton.Enabled = index > 0;
            subEntryDownButton.Enabled = index < currentAnmChrEntry.subEntries.Count - 1;
        }

        private void UpdateCommandButtons()
        {
            if (currentAnmChrEntry == null || subEntryListBox.SelectedIndex < 0 || subEntryListBox.SelectedIndex >= currentAnmChrEntry.subEntries.Count)
            {
                commandMoveUpButton.Enabled = false;
                commandMoveDownButton.Enabled = false;
                return;
            }

            var subEntry = currentAnmChrEntry.subEntries[subEntryListBox.SelectedIndex];
            if (commandListBox.SelectedIndex < 0)
            {
                commandMoveUpButton.Enabled = false;
                commandMoveDownButton.Enabled = false;
                return;
            }

            int commandIndex = commandListBox.SelectedIndex;
            commandMoveUpButton.Enabled = commandIndex > 0;
            commandMoveDownButton.Enabled = commandIndex < subEntry.subsubEntries.Count - 1;
        }

        private void UpdateCommandDetails(int commandIndex)
        {
            if (currentAnmChrEntry == null || subEntryListBox.SelectedIndex < 0 || subEntryListBox.SelectedIndex >= currentAnmChrEntry.subEntries.Count)
            {
                commandDetailTextBox.Clear();
                commandDetailLabel.Text = "Command details";
                return;
            }

            var subEntry = currentAnmChrEntry.subEntries[subEntryListBox.SelectedIndex];
            if (commandIndex < 0 || commandIndex >= subEntry.subsubEntries.Count)
            {
                commandDetailTextBox.Clear();
                commandDetailLabel.Text = "Command details";
                return;
            }

            commandDetailLabel.Text = $"Command details (Time {subEntry.localindex})";
            commandDetailTextBox.Text = FormatCommandDetails(subEntry, commandIndex);
        }

        private string FormatCommandDetails(AnmChrSubEntry subEntry, int commandIndex)
        {
            byte[] data = subEntry.subsubEntries[commandIndex];
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Command: {subEntry.GetSubSubName(commandIndex)}");
            builder.AppendLine($"Command index: {commandIndex + 1} of {subEntry.subsubEntries.Count}");
            builder.AppendLine($"Block index: {subEntry.subsubIndices[commandIndex]}");
            builder.AppendLine($"Data length: {data.Length} bytes");
            builder.AppendLine();
            builder.AppendLine("Parameters:");

            if (data.Length == 0)
            {
                builder.AppendLine("  (no parameters)");
            }
            else
            {
                int chunkCount = data.Length / 4;
                for (int i = 0; i < chunkCount; i++)
                {
                    int offset = i * 4;
                    int intValue = BitConverter.ToInt32(data, offset);
                    float floatValue = BitConverter.ToSingle(data, offset);
                    builder.AppendLine($"  [{i}] Int32: {intValue}    Float: {floatValue}");
                }

                int remainder = data.Length % 4;
                if (remainder > 0)
                {
                    builder.Append("  Remaining bytes: ");
                    for (int i = data.Length - remainder; i < data.Length; i++)
                    {
                        builder.Append(data[i]);
                        if (i < data.Length - 1)
                        {
                            builder.Append(", ");
                        }
                    }
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private void MoveSubEntry(int direction)
        {
            if (currentAnmChrEntry == null)
            {
                return;
            }

            int index = subEntryListBox.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            int newIndex = index + direction;
            if (newIndex < 0 || newIndex >= currentAnmChrEntry.subEntries.Count)
            {
                return;
            }

            AnmChrSubEntry item = currentAnmChrEntry.subEntries[index];
            currentAnmChrEntry.subEntries.RemoveAt(index);
            currentAnmChrEntry.subEntries.Insert(newIndex, item);
            currentAnmChrEntry.UpdateSize();
            SetTextConcurrent(currentAnmChrEntry.GetData());
            PopulateSubEntries(newIndex);
        }

        private void MoveCommand(int direction)
        {
            if (currentAnmChrEntry == null)
            {
                return;
            }

            int subIndex = subEntryListBox.SelectedIndex;
            if (subIndex < 0 || subIndex >= currentAnmChrEntry.subEntries.Count)
            {
                return;
            }

            var subEntry = currentAnmChrEntry.subEntries[subIndex];
            int commandIndex = commandListBox.SelectedIndex;
            if (commandIndex < 0)
            {
                return;
            }

            int newIndex = commandIndex + direction;
            if (newIndex < 0 || newIndex >= subEntry.subsubEntries.Count)
            {
                return;
            }

            byte[] command = subEntry.subsubEntries[commandIndex];
            int commandId = subEntry.subsubIndices[commandIndex];
            subEntry.subsubEntries.RemoveAt(commandIndex);
            subEntry.subsubIndices.RemoveAt(commandIndex);
            subEntry.subsubEntries.Insert(newIndex, command);
            subEntry.subsubIndices.Insert(newIndex, commandId);
            subEntry.isEdited = true;
            currentAnmChrEntry.UpdateSize();
            SetTextConcurrent(currentAnmChrEntry.GetData());
            PopulateCommandList(subIndex, newIndex);
        }

        private void subEntryListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressSubEntryEvents)
            {
                return;
            }

            UpdateSubEntryButtons();
            PopulateCommandList(subEntryListBox.SelectedIndex);
        }

        private void commandListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressCommandEvents)
            {
                return;
            }

            UpdateCommandButtons();
            UpdateCommandDetails(commandListBox.SelectedIndex);
        }

        private void subEntryUpButton_Click(object sender, EventArgs e)
        {
            MoveSubEntry(-1);
        }

        private void subEntryDownButton_Click(object sender, EventArgs e)
        {
            MoveSubEntry(1);
        }

        private void commandMoveUpButton_Click(object sender, EventArgs e)
        {
            MoveCommand(-1);
        }

        private void commandMoveDownButton_Click(object sender, EventArgs e)
        {
            MoveCommand(1);
        }

        private void importButton_Click(object sender, EventArgs ev)
        {
            if (animBox.SelectedIndex < 0
                ||
                animBox.SelectedIndex >= tablefile.table.Count)
            {
                return;
            }

            using (OpenFileDialog openFile = new OpenFileDialog())
            {
                openFile.DefaultExt = "mvc3data";
                if (ImportPath != String.Empty)
                {
                    try
                    {
                        openFile.InitialDirectory = Path.GetDirectoryName(FilePath);
                        openFile.FileName = Path.GetFileName(FilePath);
                    }
                    catch (Exception e)
                    {
                        AELogger.Log("some kind of exception setting save path from " + FilePath);
                        AELogger.Log("Exception: " + e.Message);

                        AELogger.Log("Exception: " + e.StackTrace);

                        int i = 1;
                        while (e.InnerException != null)
                        {
                            e = e.InnerException;
                            AELogger.Log("InnerException " + i + ": " + e.Message);

                            AELogger.Log("InnerException " + i + ": " + e.StackTrace);
                            i++;
                        }
                    }
                }
                openFile.Title = "Import" + tablefile.table[animBox.SelectedIndex].GetFancyName();
                // The Filter property requires a search string after the pipe ( | )
                if (tablefile.fileExtension == "CSP")
                {
                    openFile.Filter = "UMVC3 Loose Input Command Data (*.mvc3csp)|*.mvc3csp|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files (*.*)|*.*";
                }
                else if (tablefile.fileExtension == "CBA")
                {
                    openFile.Filter = "UMVC3 Loose Basic Action Data (*.mvc3cba)|*.mvc3cba|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*";
                }
                else if (tablefile.fileExtension == "CCM")
                {
                    openFile.Filter = "UMVC3 Loose Unique Cancel Data (*.mvc3ccm)|*.mvc3ccm|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*";
                }
                else if (tablefile.fileExtension == "ATI")
                {
                    openFile.Filter = "UMVC3 Loose Attack Data (*.mvc3ati)|*.mvc3ati|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*";
                }
                else if (tablefile.fileExtension == "CLI")
                {
                    openFile.Filter = "UMVC3 Loose Collision Data (*.mvc3cli)|*.mvc3cli|UMVC3 Loose Data (*.mvc3data)|*.mvc3data|All files (*.*)|*.*";
                }
                else if (tablefile.fileExtension == "CHS")
                {
                    openFile.Filter = "UMVC3 Loose Status Data (*.mvc3chs)|*.mvc3chs|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*";
                }
                else if (tablefile.fileExtension == "SHT")
                {
                    openFile.Filter = "UMVC3 Loose Projectile Data (*.mvc3sht)|*.mvc3sht|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*";
                }
                else if (tablefile.fileExtension == "CPI")
                {
                    openFile.Filter = "UMVC3 Loose Profile Data (*.mvc3cpi)|*.mvc3cpi|UMVC3 Loose Data(*.mvc3data)|*.mvc3data|All files(*.*)|*.*";
                }
                else
                {
                    openFile.Filter = "All files (*.*)|*.*|UMVC3 Loose Data(*.mvc3data)|*.mvc3data";
                }
                openFile.ShowDialog();
                if (openFile.FileNames.Length > 0)
                {
                    tablefile.table[animBox.SelectedIndex].Import(openFile.FileNames[0]);
                    RefreshData();
                    dataTextBox.Text = BitConverter.ToString(tablefile.table[animBox.SelectedIndex].GetData()).Replace("-", "");
                    sizeLabel.Text = "size: " + tablefile.table[animBox.SelectedIndex].size;
                    ImportPath = openFile.FileNames[0];
                }
                else
                {
                    AELogger.Log("nothing selected!");
                }
            }
        }

        private void RefreshData()
        {
            bDisableUpdate = true;
            int s = animBox.SelectedIndex;
            int top = animBox.TopIndex;
            tableNames = tablefile.GetNames();
            animBox.DataSource = tableNames;
            animBox.SelectedIndex = s;
            animBox.TopIndex = top;
            bDisableUpdate = false;
            if (tablefile != null && animBox.SelectedIndex >= 0 && animBox.SelectedIndex < tablefile.table.Count)
            {
                UpdateStructureView(tablefile.table[animBox.SelectedIndex]);
            }
            else
            {
                UpdateStructureView(null);
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if(bDisableUpdate)
            {
                return;
            }
            
            tablefile.table[animBox.SelectedIndex].name = textBox1.Text;
            RefreshData();
        }

        private void extendButton_Click(object sender, EventArgs e)
        {
            switch (MessageBox.Show(this, "Do you want to extend list?" + Environment.NewLine + "This action cannot be undone!", "Extend List", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
            {
                case DialogResult.No:
                    break;
                default:
                    tablefile.Extend();
                    RefreshData();
                    if (animBox.TopIndex < animBox.Items.Count - 2)
                    {
                        animBox.TopIndex++;
                    }
                    break;
            }
        }


        // YES I KNOW THIS IS EXCESSIVE BUT LOOK I WANTED TO DO IT OKAY
        public Task TextTask;
        public byte[] newText;
        public bool bTextNeedsToBeDone;
        public void SetTextConcurrent(byte[] text)
        {
            bTextNeedsToBeDone = true;
            newText = text;
            if (TextTask == null)
            {
                TextTask = new Task(SetText);
                TextTask.Start();
            }
            else if (TextTask.IsCompleted)
            {
                TextTask.Dispose();
                TextTask = new Task(SetText);
                TextTask.Start();
            }
        }

        public void SetText()
        {
            try
            {
                int textSize = 8;

                if (dataTextBox.Width > 1140)
                {
                    textSize = 64;
                }
                else if (dataTextBox.Width > 570)
                {
                    textSize = 32;
                }
                else if (dataTextBox.Width > 285)
                {
                    textSize = 16;
                }
                while (bTextNeedsToBeDone)
                {
                    bTextNeedsToBeDone = false;
                    dataTextBox.Clear();

                    int newTextLength;
                    string[] newLines;
                    lock (newText)
                    {
                        newTextLength = newText.Length;
                        int lineCount = newTextLength / textSize;
                        if (newTextLength % textSize > 0)
                        {
                            lineCount++;
                        }
                        newLines = new string[lineCount];
                    }
                    for (int i = 0; i <= newTextLength / textSize; i++)
                    {
                        lock (newText)
                        {
                            if (bTextNeedsToBeDone)
                            {
                                break;
                            }
                            if (i == newTextLength / textSize)
                            {
                                if (newTextLength % textSize > 0)
                                {
                                    newLines[i] = BitConverter.ToString(newText, i * textSize, newTextLength % textSize).Replace("-", "");
                                }
                            }
                            else
                            {
                                newLines[i] = BitConverter.ToString(newText, i * textSize, textSize).Replace("-", "");
                            }
                        }
                    }

                    if (!bTextNeedsToBeDone)
                    {
                        dataTextBox.Lines = newLines;
                    }
                }
                dataTextBox.Select(0, 0);
                dataTextBox.ScrollToCaret();
            }
            catch(Exception e)
            {
                bTextNeedsToBeDone = false;
                AELogger.Log("Exception: " + e.Message);

                AELogger.Log("Exception: " + e.StackTrace);

                int i = 1;
                while (e.InnerException != null)
                {
                    e = e.InnerException;
                    AELogger.Log("InnerException " + i + ": " + e.Message);

                    AELogger.Log("InnerException " + i + ": " + e.StackTrace);
                    i++;
                }
            }
        }

        private void analyzeButton_Click(object sender, EventArgs e)
        {
            tablefile.Analyze();
            analyzeButton.Enabled = false;
        }
    }
}
