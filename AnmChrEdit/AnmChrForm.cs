using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Threading;
using MarvelData;
using static System.Windows.Forms.LinkLabel;
using Microsoft.VisualBasic;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;

namespace AnmChrEdit
{
    public partial class ACE : Form
    {
        public static bool bError = false;
        public string filePath;
        public string ImportPath;
        public TableFile tablefile;
        public List<string> tableNames;
        public bool bDisableUpdate;
        public bool bDisableSubUpdate;
        public bool bDisableSubSubUpdate;
        public bool isMultiSelection;
        public AnmChrSubEntry commandBlockCopyInstance;
        public CmdSpAtkEntry spatkCopyInstance = new CmdSpAtkEntry();
        public byte[] commandCopyInstance;
        
        public List<List<int>> selectedSubSubIndices;
        public List<int> selectedSubIndices;

        public BindingList<string> subDataSource;
        public BindingList<string> subsubDataSource;
        private int subEntryHoveredIndex = -1;
        private ImageForm imageForm;
        private bool isChecked;
        private bool isBreak;
        public bool isDeleting = false;
        public int reselectID = -1;
        private BindingList<CommandByteRow> commandDetailRows;
        private bool isUpdatingCommandDetail;
        private Panel commandDetailPanel;
        private Label commandDetailHeaderLabel;
        private DataGridView commandDetailGrid;
        private ComboBox commandSelector;
        private BindingList<CommandDefinition> commandDefinitionSource;
        private Dictionary<long, CommandDefinition> commandDefinitionMap;
        private Dictionary<long, byte[]> commandTemplates;
        private bool isUpdatingCommandSelector;

        private class CommandByteRow
        {
            public int Index { get; set; }
            public byte Value { get; set; }
        }

        private class CommandDefinition
        {
            public CommandDefinition(long key, string label)
            {
                Key = key;
                DisplayLabel = label;
                Group = (byte)(key >> 32);
                IdLow = (byte)(key & 0xFF);
                IdHigh = (byte)((key >> 8) & 0xFF);
                Code = ExtractCode(label);
            }

            public long Key { get; }
            public byte Group { get; }
            public byte IdLow { get; }
            public byte IdHigh { get; }
            public string DisplayLabel { get; }
            public string Code { get; }

            private static string ExtractCode(string label)
            {
                if (string.IsNullOrWhiteSpace(label))
                {
                    return string.Empty;
                }

                var parts = label.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 0 ? parts[0] : label;
            }

            public override string ToString() => DisplayLabel;
        }

        public ACE()
        {
            InitializeComponent();
            Text += ", build " + GetCompileDate();
            //AELogger.Log(Text);
            filePath = String.Empty;
            ImportPath = String.Empty;
            selectedSubIndices = new List<int>();
            selectedSubSubIndices = new List<List<int>>();
            AnmChrSubEntry.InitCmdNames();
            commandBlocksBox.DrawItem += commandBlocksBox_DrawItem;
            commandBlocksBox.DrawMode = DrawMode.OwnerDrawFixed;

            commandDetailRows = new BindingList<CommandByteRow>();
            commandTemplates = new Dictionary<long, byte[]>();
            commandDefinitionMap = new Dictionary<long, CommandDefinition>();
            commandDefinitionSource = new BindingList<CommandDefinition>();
            SetupModernInterface();
        }

        private void SetupModernInterface()
        {
            BackColor = Color.FromArgb(28, 28, 28);
            ForeColor = Color.White;

            tableLayoutPanel1.Controls.Remove(dataTextBox);
            dataTextBox.Visible = false;
            dataTextBox.Enabled = false;

            commandDetailPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(36, 36, 36)
            };

            PopulateCommandDefinitionSource();

            commandDetailHeaderLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                Text = "Select a command to view its details."
            };

            commandSelector = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 8, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Enabled = false,
                IntegralHeight = false,
                MaxDropDownItems = 20
            };

            commandSelector.DataSource = commandDefinitionSource;
            commandSelector.DisplayMember = nameof(CommandDefinition.DisplayLabel);
            commandSelector.SelectedIndexChanged += commandSelector_SelectedIndexChanged;

            isUpdatingCommandSelector = true;
            commandSelector.SelectedIndex = -1;
            commandSelector.Text = "Select a command...";
            isUpdatingCommandSelector = false;

            commandDetailGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                BackgroundColor = Color.FromArgb(32, 32, 32),
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(64, 64, 64),
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                ReadOnly = false
            };

            commandDetailGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            commandDetailGrid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(70, 70, 70),
                SelectionForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };

            var indexColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(CommandByteRow.Index),
                HeaderText = "Byte",
                ReadOnly = true,
                Width = 60
            };

            var valueColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(CommandByteRow.Value),
                HeaderText = "Value",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            commandDetailGrid.Columns.Add(indexColumn);
            commandDetailGrid.Columns.Add(valueColumn);

            commandDetailGrid.CellValidating += commandDetailGrid_CellValidating;
            commandDetailGrid.CellValueChanged += commandDetailGrid_CellValueChanged;
            commandDetailGrid.DataError += commandDetailGrid_DataError;

            commandDetailGrid.DataSource = commandDetailRows;

            var commandDetailHeaderLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                BackColor = Color.FromArgb(36, 36, 36)
            };
            commandDetailHeaderLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            commandDetailHeaderLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            commandDetailHeaderLayout.Controls.Add(commandDetailHeaderLabel, 0, 0);
            commandDetailHeaderLayout.Controls.Add(commandSelector, 0, 1);

            commandDetailPanel.Controls.Add(commandDetailGrid);
            commandDetailPanel.Controls.Add(commandDetailHeaderLayout);

            tableLayoutPanel1.Controls.Add(commandDetailPanel, 0, 6);
            tableLayoutPanel1.SetColumnSpan(commandDetailPanel, 3);
            if (tableLayoutPanel1.RowStyles.Count > 6)
            {
                tableLayoutPanel1.RowStyles[6] = new RowStyle(SizeType.Percent, 100F);
            }

            formatUnsetButton.Visible = false;
            format8HexButton.Visible = false;
            format16HexButton.Visible = false;
            formatDisplayToolStripMenuItem.Visible = false;

            Color listBackColor = Color.FromArgb(32, 32, 32);
            animBox.BackColor = listBackColor;
            commandBlocksBox.BackColor = listBackColor;
            commandsBox.BackColor = listBackColor;

            animBox.ForeColor = Color.White;
            commandBlocksBox.ForeColor = Color.White;
            commandsBox.ForeColor = Color.White;

            commandBlocksBox.BorderStyle = BorderStyle.FixedSingle;
            commandsBox.BorderStyle = BorderStyle.FixedSingle;
            animBox.BorderStyle = BorderStyle.FixedSingle;

            tableLayoutPanel1.BackColor = Color.FromArgb(30, 30, 30);
            tableLayoutPanel2.BackColor = Color.FromArgb(30, 30, 30);
            tableLayoutPanel3.BackColor = Color.FromArgb(30, 30, 30);
            tableLayoutPanel4.BackColor = Color.FromArgb(30, 30, 30);

            splitContainer1.BackColor = Color.FromArgb(25, 25, 25);
            splitContainer2.BackColor = Color.FromArgb(25, 25, 25);
            splitContainer4.BackColor = Color.FromArgb(25, 25, 25);

            sizeLabel.ForeColor = Color.WhiteSmoke;

            ClearCommandDetailView();
            AddReorderOptions();
        }

        private void PopulateCommandDefinitionSource()
        {
            if (commandDefinitionSource == null)
            {
                commandDefinitionSource = new BindingList<CommandDefinition>();
            }

            if (commandDefinitionMap == null)
            {
                commandDefinitionMap = new Dictionary<long, CommandDefinition>();
            }

            commandDefinitionMap.Clear();
            commandDefinitionSource.Clear();

            if (AnmChrSubEntry.cmdNames == null || AnmChrSubEntry.cmdNames.Count == 0)
            {
                return;
            }

            var orderedDefinitions = AnmChrSubEntry.cmdNames
                .Select(pair => new CommandDefinition(pair.Key, pair.Value))
                .OrderBy(definition => definition.Code, StringComparer.OrdinalIgnoreCase)
                .ThenBy(definition => definition.DisplayLabel, StringComparer.OrdinalIgnoreCase);

            foreach (var definition in orderedDefinitions)
            {
                commandDefinitionMap[definition.Key] = definition;
                AddDefinitionSorted(definition);
            }
        }

        private void AddDefinitionSorted(CommandDefinition definition)
        {
            int insertIndex = 0;
            while (insertIndex < commandDefinitionSource.Count)
            {
                var existing = commandDefinitionSource[insertIndex];
                int codeComparison = string.Compare(existing.Code, definition.Code, StringComparison.OrdinalIgnoreCase);
                if (codeComparison > 0)
                {
                    break;
                }

                if (codeComparison == 0)
                {
                    int labelComparison = string.Compare(existing.DisplayLabel, definition.DisplayLabel, StringComparison.OrdinalIgnoreCase);
                    if (labelComparison > 0)
                    {
                        break;
                    }
                }

                insertIndex++;
            }

            commandDefinitionSource.Insert(insertIndex, definition);
        }

        private CommandDefinition EnsureCommandDefinition(long key, string label)
        {
            if (key < 0)
            {
                return null;
            }

            if (commandDefinitionMap != null && commandDefinitionMap.TryGetValue(key, out var definition))
            {
                return definition;
            }

            var displayLabel = string.IsNullOrWhiteSpace(label) ? $"0x{key:X}" : label;
            definition = new CommandDefinition(key, displayLabel);
            commandDefinitionMap[key] = definition;
            AddDefinitionSorted(definition);
            return definition;
        }

        private static byte[] CloneBytes(byte[] source)
        {
            if (source == null)
            {
                return Array.Empty<byte>();
            }

            var clone = new byte[source.Length];
            Array.Copy(source, clone, clone.Length);
            return clone;
        }

        private long GetCommandKey(byte[] data)
        {
            if (data == null || data.Length < 6)
            {
                return -1;
            }

            long key = ((long)data[0] << 32);
            key += data[4];
            key += (long)data[5] << 8;
            return key;
        }

        private void CacheCommandTemplate(byte[] data)
        {
            if (commandTemplates == null)
            {
                commandTemplates = new Dictionary<long, byte[]>();
            }

            long key = GetCommandKey(data);
            if (key < 0)
            {
                return;
            }

            commandTemplates[key] = CloneBytes(data);
        }

        private bool TryGetTemplate(long key, out byte[] template)
        {
            template = null;

            if (key < 0)
            {
                return false;
            }

            if (commandTemplates != null && commandTemplates.TryGetValue(key, out var cached))
            {
                template = CloneBytes(cached);
                return true;
            }

            var discovered = FindTemplateInTable(key);
            if (discovered != null)
            {
                template = CloneBytes(discovered);
                commandTemplates[key] = CloneBytes(discovered);
                return true;
            }

            return false;
        }

        private byte[] FindTemplateInTable(long key)
        {
            if (tablefile?.table == null)
            {
                return null;
            }

            foreach (var entryBase in tablefile.table)
            {
                if (entryBase is AnmChrEntry entry && entry.bHasData)
                {
                    foreach (var block in entry.subEntries)
                    {
                        foreach (var command in block.subsubEntries)
                        {
                            if (command != null && command.Length >= 6 && GetCommandKey(command) == key)
                            {
                                return CloneBytes(command);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private void ApplyCommandHeader(byte[] data, CommandDefinition definition)
        {
            if (data == null || definition == null)
            {
                return;
            }

            if (data.Length > 0)
            {
                data[0] = definition.Group;
            }

            if (data.Length > 4)
            {
                data[4] = definition.IdLow;
            }

            if (data.Length > 5)
            {
                data[5] = definition.IdHigh;
            }
        }

        private byte[] BuildCommandData(CommandDefinition definition, byte[] currentData)
        {
            if (definition == null)
            {
                return currentData ?? Array.Empty<byte>();
            }

            if (TryGetTemplate(definition.Key, out var template))
            {
                ApplyCommandHeader(template, definition);
                return template;
            }

            int length = currentData?.Length ?? 0;
            if (length < 8)
            {
                length = 16;
            }

            var data = new byte[length];
            if (currentData != null && currentData.Length > 0)
            {
                Array.Copy(currentData, data, Math.Min(currentData.Length, data.Length));
            }

            ApplyCommandHeader(data, definition);
            return data;
        }

        private void RebuildCommandTemplates()
        {
            if (commandTemplates == null)
            {
                commandTemplates = new Dictionary<long, byte[]>();
            }

            commandTemplates.Clear();

            if (tablefile?.table == null)
            {
                return;
            }

            foreach (var entryBase in tablefile.table)
            {
                if (entryBase is AnmChrEntry entry && entry.bHasData)
                {
                    foreach (var block in entry.subEntries)
                    {
                        foreach (var command in block.subsubEntries)
                        {
                            CacheCommandTemplate(command);
                        }
                    }
                }
            }
        }

        public static string GetCompileDate()
        {
            System.Version MyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return new DateTime(2000, 1, 1).AddDays(MyVersion.Build).AddSeconds(MyVersion.Revision * 2).ToString("MMM.dd.yyyy");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.CloseReason == CloseReason.WindowsShutDown) return;
            if (bError) return;

            // Confirm user wants to close
            switch (MessageBox.Show(this, "Are you sure you want to close?" + Environment.NewLine + "All unsaved data will be lost!", "Closing", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
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
                if (saveAsToolStripMenuItem.Enabled || saveToolStripMenuItem.Enabled)
                {
                    saveButton_Click(null, null);
                }
                return true;
            }
            else if (keyData == (Keys.Control | Keys.O))
            {
                if (openToolStripMenuItem.Enabled)
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
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RefreshData()
        {
            bDisableUpdate = true;
            int s = animBox.SelectedIndex;
            int top = animBox.TopIndex;
            tableNames = tablefile.GetNames();
            animBox.DataSource = tableNames;
            if (s >= animBox.Items.Count)
            {
                s = animBox.Items.Count -1;
            }
            animBox.SelectedIndex = s;
            animBox.TopIndex = top;
            bDisableUpdate = false;
        }

        public void SaveSelectedIndices()
        {
            //AELogger.Log("HAHA");
            int s = animBox.SelectedIndex;
            for (int i = selectedSubIndices.Count; i <= s; i++)
            {
                //AELogger.Log("a" + i);
                selectedSubIndices.Add(-1);
            }

            int subS = commandBlocksBox.SelectedIndex;
            selectedSubIndices[s] = subS;
            
            for (int i = selectedSubSubIndices.Count; i <= s; i++)
            {
                //AELogger.Log("b" + i);
                selectedSubSubIndices.Add(new List<int>());
            }

            for (int i = selectedSubSubIndices[s].Count; i <= subS; i++)
            {
                //AELogger.Log("c" + i);
                selectedSubSubIndices[s].Add(-1);
            }

            selectedSubSubIndices[s][subS] = commandsBox.SelectedIndex;
        }

        public void RefreshSelectedIndices()
        {
            int s = animBox.SelectedIndex;

            if (!tablefile.table[s].bHasData || commandBlocksBox.Items.Count == 0)
            {
                EmptyCommandsTextBox();
                return;
            }

            if (!bDisableSubUpdate && selectedSubIndices.Count > s)
            {
                int selectedSub = selectedSubIndices[s];
                if (selectedSub >= 0 && selectedSub < commandBlocksBox.Items.Count)
                {
                    commandBlocksBox.SelectedIndex = selectedSub;
                    
                    // un-selects top-most multi select entry if relevant
                    if (commandBlocksBox.SelectedItems.Count > 1)
                    {
                        commandBlocksBox.SelectedItems.Remove(commandBlocksBox.SelectedItems[0]);
                    }
                }
                else if (selectedSub >= 0 && selectedSub <= commandBlocksBox.Items.Count)
                {
                    commandBlocksBox.SelectedIndex = selectedSub -1;

                    // un-selects top-most multi select entry if relevant
                    if (commandBlocksBox.SelectedItems.Count > 1)
                    {
                        commandBlocksBox.SelectedItems.Remove(commandBlocksBox.SelectedItems[0]);
                    }
                }
            }

            if (selectedSubSubIndices.Count > s)
            {
                int subS = commandBlocksBox.SelectedIndex;
                List<int> selectedList = selectedSubSubIndices[s];
                if (selectedList.Count > subS && subS >= 0)
                {
                    int selectedSub = selectedList[subS];

                    if (selectedSub >= 0 && selectedSub < commandsBox.Items.Count)
                    {
                        commandsBox.SelectedIndex = selectedSub;
                    }
                }
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
                if (tablefile != null)
                {
                    // Confirm user wants to open a new instance
                    switch (MessageBox.Show(this, "Are you sure you want to open a new instance?" 
                        + Environment.NewLine + "All unsaved data will be lost!", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
                    {
                        case DialogResult.No:
                            AELogger.Log("procedure canceled!");
                            return;
                        default:
                            Text = "AnmChrEditor, build " + GetCompileDate();
                            /*filenameLabel.Text = String.Empty;
                            ClearCommandDetailView();
                            filePath = String.Empty;
                            AELogger.WriteLog();*/
                            break;
                    }
                }
                //openFile.DefaultExt = "bcm";
                // The Filter property requires a search string after the pipe ( | )
                openFile.Filter = "AnmChr Files (*.cac;*.5A7E5D8A; *.anm)|*.cac;*.5A7E5D8A;*.anm|All files (*.*)|*.*";
                openFile.ShowDialog();
                
                if (openFile.FileNames.Length > 0)
                {
                    filenameLabel.Text = String.Empty;
                    ClearCommandDetailView();
                    filePath = String.Empty;
                    AELogger.WriteLog();
                    //TableFile newTable = TableFile.LoadFile(openFile.FileNames[0], true, typeof(StructEntry<ATKInfoChunk>), 848, true);
                    TableFile newTable = TableFile.LoadFile(openFile.FileNames[0], false, typeof(AnmChrEntry), -1, true);
                    int count = newTable.table.Count;
                    if (newTable == null && count != 0)
                    {
                        AELogger.Log("load failed for some reason?");
                        return;
                    }

                    tablefile = newTable;
                    RebuildCommandTemplates();
                    filePath = openFile.FileNames[0];
#if DEBUG
                    tablefile.AnalyzeAnmChr();
#endif
                    // start naming missing labels
                    List<AnmChrSubEntry> subEntries = new List<AnmChrSubEntry>();
                    tablefile.GetSubSubEntryInfo(ref subEntries);
                    tablefile.MatchAtiNames(filePath);
                    // end naming missing labels


                    SuspendLayout();
                    saveToolStripMenuItem.Enabled = true;
                    saveAsToolStripMenuItem.Enabled = true;
                    importButton.Enabled = false;
                    exportButton.Enabled = false;
                    animBox.Enabled = true;
                    extendButton.Enabled = true;

                    sizeLabel.Text = count + " entries loaded";
                    RefreshData();

                    for (int i = 0; i < animBox.Items.Count; i++)
                    {
                        selectedSubSubIndices.Add(new List<int>());
                    }

                    //Text += " :: " + openFile.FileNames[0];
                    
                    filenameLabel.Text = openFile.FileNames[0];
                    animBox.SelectedIndex = 0;
                    ResumeLayout();
                    animBox_SelectedIndexChanged(null, null);
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
                if (filePath != String.Empty)
                {
                    try
                    {
                        saveFileDialog1.InitialDirectory = Path.GetDirectoryName(filePath);
                        saveFileDialog1.FileName = Path.GetFileName(filePath);
                    }
                    catch (Exception e)
                    {
                        AELogger.Log("some kind of exception setting save path from " + filePath);
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
                        filePath = saveFileDialog1.FileNames[0];
                        tablefile.WriteFile(saveFileDialog1.FileNames[0]);
                    }
                }
            }

        }

        private void closeButton_Click(object sender, EventArgs ev)
        {
            Application.Exit();
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
                openFile.DefaultExt = "mvc3anm";
                if (ImportPath != String.Empty)
                {
                    try
                    {
                        openFile.InitialDirectory = Path.GetDirectoryName(filePath);
                        openFile.FileName = Path.GetFileName(filePath);
                    }
                    catch (Exception e)
                    {
                        AELogger.Log("some kind of exception setting save path from " + filePath);
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
                openFile.Title = "Import " + tablefile.table[animBox.SelectedIndex].GetFancyName();
                // The Filter property requires a search string after the pipe ( | )
                openFile.Filter = "UMVC3 Anmchr Entry (*.mvc3anm)|*.mvc3anm|UMVC3 Loose Data (*.mvc3data)|*.mvc3data|All files (*.*)|*.*";

                openFile.ShowDialog();
                if (openFile.FileNames.Length > 0)
                {
                    tablefile.table[animBox.SelectedIndex].Import(openFile.FileNames[0]);
                    RefreshData();
                    bDisableUpdate = true;
                    RefreshEditBox();
                    bDisableUpdate = false;
                    sizeLabel.Text = "size: " + tablefile.table[animBox.SelectedIndex].size;
                    ImportPath = openFile.FileNames[0];
                }
                else
                {
                    AELogger.Log("nothing selected!");
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

                Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                saveFileDialog1.Title = "Export " + tablefile.table[animBox.SelectedIndex].GetFancyName();
                if (tablefile.fileExtension == "CAC")
                {
                  saveFileDialog1.Filter = "All files (*.*)|*.*|UMVC3 Loose Data (*.mvc3data)|*.mvc3data|UMVC3 Character Script File (*.mvc3anm;*.mvc3data)|*.mvc3anm;*.mvc3data";
                }
                else
                {
                    saveFileDialog1.Filter = "All files (*.*)|*.*|UMVC3 Loose Data(*.mvc3data)|*.mvc3data";
                }

                if (filePath != String.Empty)
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
                saveFileDialog1.FilterIndex = 3;
                saveFileDialog1.RestoreDirectory = true;
                saveFileDialog1.FileName = rgx.Replace(tablefile.table[animBox.SelectedIndex].GetFilename(), "");
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

        private void formatUnsetButton_Click(object sender, EventArgs e)
        {
            // Legacy handler retained for designer compatibility.
        }

        private void format8HexButton_Click(object sender, EventArgs e)
        {
            // Legacy handler retained for designer compatibility.
        }

        private void format16HexButton_Click(object sender, EventArgs e)
        {
            // Legacy handler retained for designer compatibility.
        }

        private void dataTextBox_TextChanged(object sender, EventArgs ev)
        {
            // Legacy handler retained for designer compatibility.
        }

        private void testImgButton_Click(object sender, EventArgs e)
        {
            imageForm = new ImageForm();
            imageForm.Show();
        }

        private void animBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bDisableUpdate)
            {
                return;
            }
            //SuspendLayout();
            animBox.BeginUpdate();
            AELogger.Log("selected animbox " + animBox.SelectedIndex);
            bDisableUpdate = true;
            importButton.Enabled = true;
            RefreshEditBox();
            RefreshSelectedIndices();
            //sizeLabel.Text = "size: " + tablefile.table[animBox.SelectedIndex].size;
            subEntryHoveredIndex = -1;
            bDisableUpdate = false;
            animBox.EndUpdate();
            //ResumeLayout();
        }

        public void RefreshEditBox() // disableupdate is true here
        {
            if (!bDisableUpdate)
            {
                AELogger.Log("ENABLED WHILE REFRESHEDITBOX WARNING");
                return;
            }
            int s = animBox.SelectedIndex;
            int top = animBox.TopIndex;
            //subEntryBox.BeginUpdate();
            if (tablefile.table[s].bHasData && tablefile.table[s] is AnmChrEntry)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[s];
                ClearCommandDetailView();
                subDataSource = entry.getSubEntryList();
                commandBlocksBox.DataSource = subDataSource;
                // total time elements
                lengthTextBox.Enabled = true;
                lengthTextBox.Text = entry.animTime.ToString();
                exportButton.Enabled = true;
                // commandBlocks button>menuOptions>contextMenu
                commandBlockCopyButton.Enabled = true;
                copyCommandBlockToolStripMenuItem1.Enabled = true;
                copyCommandBlockToolStripMenuItem.Enabled = true;
                commandBlockPasteButton.Enabled = commandBlockCopyInstance != null;
                pasteCommandBlockToolStripMenuItem1.Enabled = commandBlockCopyInstance != null && !isMultiSelection; ;
                pasteCommandBlockToolStripMenuItem.Enabled = commandBlockCopyInstance != null && !isMultiSelection; ;
                // commands button>menuOptions>contextMenu
                commandsCopyButton.Enabled = true;
                copyCommandsToolStripMenuItem.Enabled = true;
                copyCommandToolStripMenuItem.Enabled = true;
                commandsPasteButton.Enabled = commandCopyInstance != null;
                pasteCommandsToolStripMenuItem.Enabled = commandCopyInstance != null && !isMultiSelection; ;
                pasteCommandToolStripMenuItem.Enabled = commandCopyInstance != null && !isMultiSelection; ;
                validateDeleteButtons(entry);
            }
            else
            {
                //subsubEntryBox.BeginUpdate();
                ClearCommandDetailView("No animation data available.");
                exportButton.Enabled = false;
                // total time elements
                lengthTextBox.Enabled = false;
                lengthTextBox.Clear();
                // commandBlocks button>menuOptions>contextMenu
                commandBlocksBox.DataSource = null;
                commandBlocksBox.Items.Clear();
                commandBlockDeleteButton.Enabled = false;
                commandBlockDisableButton.Enabled = false;
                commandBlockCopyButton.Enabled = false;
                copyCommandBlockToolStripMenuItem1.Enabled = false;
                copyCommandBlockToolStripMenuItem.Enabled = false;
                commandBlockPasteButton.Enabled = commandBlockCopyInstance != null;
                pasteCommandBlockToolStripMenuItem1.Enabled = commandBlockCopyInstance != null;
                pasteCommandBlockToolStripMenuItem.Enabled = commandBlockCopyInstance != null;
                // commands button>menuOptions>contextMenu
                commandsBox.Items.Clear();
                commandsBox.DataSource = null;
                commandsDeleteButton.Enabled = false;
                commandsCopyButton.Enabled = false;
                copyCommandsToolStripMenuItem.Enabled = false;
                copyCommandToolStripMenuItem.Enabled = false;
                commandsPasteButton.Enabled = commandCopyInstance != null;
                pasteCommandsToolStripMenuItem.Enabled = commandCopyInstance != null;
                pasteCommandToolStripMenuItem.Enabled = commandCopyInstance != null;
                //subsubEntryBox.EndUpdate();
            }
            //subEntryBox.EndUpdate();
        }

        private void commandBlocksBox_RightMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var commandBlockIndex = commandBlocksBox.IndexFromPoint(e.Location);
                if (commandBlockIndex >= 0)
                {
                    if ((Control.ModifierKeys & Keys.Shift) == Keys.None)
                    {
                        commandBlocksBox.ClearSelected();
                    }
                    commandBlocksBox.SelectedIndex = commandBlockIndex;
                    //subEntryBox.Controls.;
                    this.Cursor = new Cursor(Cursor.Current.Handle);
                    commandBlockContextMenuStrip.Show(Cursor.Position);
                }
            }
        }

        private void commandBlocksBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bDisableSubUpdate)
            {
                return;
            }

            bDisableSubUpdate = true;
            if (commandBlocksBox.SelectedItems.Count > 1 && isDeleting)
            {
                commandBlocksBox.SelectedItems.Remove(commandBlocksBox.SelectedItems[0]);
            } //multiselect fix
            int s = animBox.SelectedIndex;
            int top = animBox.TopIndex;
            if (tablefile.table[s] is AnmChrEntry && (tablefile.table[s].bHasData && tablefile.table[s].size > 0))
            {
                if (commandBlocksBox.SelectedIndices.Count == 1)
                {
                    isMultiSelection = false;
                    AnmChrEntry entry = (AnmChrEntry)tablefile.table[s];

                    if (commandBlocksBox.SelectedIndex >= entry.subEntries.Count)
                    {
                        commandsBox.SelectedIndex = entry.subEntries.Count - 1;
                    }

                    if (bDisableSubSubUpdate)
                    {
                        subsubDataSource = entry.subEntries[commandBlocksBox.SelectedIndex].GetCommandList();
                        commandsBox.DataSource = subsubDataSource;
                    }
                    else
                    {
                        bDisableSubSubUpdate = true;
                        subsubDataSource = entry.subEntries[commandBlocksBox.SelectedIndex].GetCommandList();
                        commandsBox.DataSource = subsubDataSource;
                        bDisableSubSubUpdate = false;
                    }
                    if (bDisableUpdate)
                    {
                        RefreshSelectedIndices();
                    }
                    else
                    {
                        SaveSelectedIndices();
                    }
                    RefreshCommandDetails();
                    timeTextBox.Text = entry.subEntries[commandBlocksBox.SelectedIndex].isDisabled ? "" : 
                    entry.subEntries[commandBlocksBox.SelectedIndex].tableindex.ToString();
                    timeTextBox.Enabled = true;
                    validateDeleteButtons(entry);
                } else if (commandBlocksBox.SelectedIndices.Count > 1)
                {
                    isMultiSelection = true;
                    EmptyCommandsTextBox();
                    validateDeleteButtons(null);
                }
                else
                {
                    isMultiSelection = false;
                    EmptyCommandsTextBox();
                    validateDeleteButtons(null);
                }
            }
            else
            {
                EmptyCommandsTextBox();
            }

            bDisableSubUpdate = false;
            //subsubEntryBox.EndUpdate();
            //ResumeLayout();
        }

        private void EmptyCommandsTextBox()
        {
            timeTextBox.Enabled = false;
            commandsBox.DataSource = null;
            commandsBox.Items.Clear();
        }

        // Sets the given # frames to the selected command block(s)
        private void SetCommandBlockFramesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var inputframes = Interaction.InputBox("Set #Frames", "Command Block number of frames", "");
            int frames;

            if (int.TryParse(inputframes, out frames))
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                isChecked = false;
                isBreak = false;
                foreach (var index in commandBlocksBox.SelectedIndices)
                {
                    if (CheckNewFrameValue(entry, frames) && !isBreak)
                    {
                        entry.subEntries[(int)index].localindex = frames < 0 ? -1 : frames > entry.animTime ? entry.animTime : frames;
                        entry.subEntries[(int)index].tableindex = entry.subEntries[(int)index].localindex;
                    }
                }
                subDataSource = entry.getSubEntryList();
                commandBlocksBox.DataSource = subDataSource; ;
            }
            else if (!String.IsNullOrEmpty(inputframes))
            {
                MessageBox.Show(this, "Invalid input.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Adds the given offset to the frames of the selected command block(s)
        private void OffsetToolStripMenu_Click(object sender, EventArgs e)
        {
            var inputOffset = Interaction.InputBox("Set offset", "Command Block Offset", "");
            int offset;

            if (int.TryParse(inputOffset, out offset))
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                isChecked = false;
                isBreak = false;
                foreach (var index in commandBlocksBox.SelectedIndices)
                {
                    if (CheckNewFrameValue(entry, entry.subEntries[(int)index].localindex + offset) && !isBreak)
                    {
                        /*entry.subEntries[(int)index].localindex =
                            entry.subEntries[(int)index].localindex + offset < 0 ? -1 :
                            entry.subEntries[(int)index].localindex + offset > entry.animTime ? entry.animTime :
                            entry.subEntries[(int)index].localindex + offset;*/
                        entry.subEntries[(int)index].localindex = entry.subEntries[(int)index].localindex + offset;
                        entry.subEntries[(int)index].tableindex = entry.subEntries[(int)index].localindex;
                    }
                }
                subDataSource = entry.getSubEntryList();
                commandBlocksBox.DataSource = subDataSource; ;
            }
            else if (!String.IsNullOrEmpty(inputOffset))
            {
                MessageBox.Show(this, "Invalid input.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Verifies new frame value giving a warning when it reaches negative values or the max frames
        private bool CheckNewFrameValue(AnmChrEntry entry, int newValue)
        {
            if ((newValue >= entry.animTime || newValue < 0) && !isChecked) 
            {
                switch (MessageBox.Show(this, "One or more of your new values will reach or exceed the " +
                    (newValue < 0 ? "minimum " : newValue > entry.animTime ? "maximum " : "minimum/maximum ") + "threshold. These values may not be read properly."
                    + Environment.NewLine + "Are you sure you want to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
                {
                    case DialogResult.No:
                        isChecked = true;
                        isBreak = true;
                        return false;
                    default:
                        isChecked = true;
                        isBreak = false;
                        return true;
                }
            }
            return true;
        }

        private void CommandsBox_RightMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var commandIndex = commandsBox.IndexFromPoint(e.Location);
                if (commandIndex >= 0)
                {
                    commandsBox.SelectedIndex = commandIndex;
                    //subEntryBox.Controls.;
                    this.Cursor = new Cursor(Cursor.Current.Handle);
                    commandContextMenuStrip1.Show(Cursor.Position);
                }
            }
        }

        private void commandsBox_SelectedIndexChanged(object sender, EventArgs ev)
        {
            if (bDisableSubSubUpdate)
            {
                return;
            }

            AELogger.Log("selected subsub box " + commandsBox.SelectedIndex);

            bDisableSubSubUpdate = true;
            int s = animBox.SelectedIndex;
            if (tablefile.table[s] is AnmChrEntry && tablefile.table[s].bHasData)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[s];

                if (commandBlocksBox.SelectedIndex >= entry.subEntries.Count && commandBlocksBox.SelectedIndex >= 0)
                {
                    AELogger.Log("possible big error");

                    RefreshCommandDetails();
                }
                else if (entry.subEntries.Count > 0 && commandBlocksBox.SelectedIndex > 0)
                {
                    if (commandsBox.SelectedIndex >= entry.subEntries[commandBlocksBox.SelectedIndex].subsubEntries.Count)
                    {
                        commandsBox.SelectedIndex = entry.subEntries.Count - 1;
                    }

                    RefreshCommandDetails();

                    SaveSelectedIndices();
                }
                else
                {
                    RefreshCommandDetails();
                }
            }
            bDisableSubSubUpdate = false;
        }
        // Creates labels for the sub entries box
        private void commandBlocksBoxMouseMove(object sender, MouseEventArgs e)
        {
            int hoveredIndex = commandBlocksBox.IndexFromPoint(e.Location);
            if (hoveredIndex == subEntryHoveredIndex)
            {
                return;
            }

            subEntryHoveredIndex = hoveredIndex;

            if (hoveredIndex < 0 || hoveredIndex >= commandBlocksBox.Items.Count)
            {
                toolTip1.SetToolTip(commandBlocksBox, string.Empty);
                return;
            }

            string tooltip = commandBlocksBox.Items[hoveredIndex].ToString();

            if (tablefile != null
                && animBox.SelectedIndex >= 0
                && animBox.SelectedIndex < tablefile.table.Count
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry entry
                && hoveredIndex < entry.subEntries.Count)
            {
                var commandList = entry.subEntries[hoveredIndex].GetCommandList();
                if (commandList != null && commandList.Count > 0)
                {
                    foreach (var command in commandList)
                    {
                        tooltip += Environment.NewLine + "  " + command;
                    }
                }
            }

            toolTip1.SetToolTip(commandBlocksBox, tooltip);
        }

        // Creates labels for the sub sub entries box
        private void OnCommandsBoxMouseMove(object sender, MouseEventArgs e)
        {
            string strTip = "";

            //Get the item
            int nIdx = commandsBox.IndexFromPoint(e.Location);
            if ((nIdx >= 0) && (nIdx < commandsBox.Items.Count))
                strTip = commandsBox.Items[nIdx].ToString();

            toolTip2.SetToolTip(commandsBox, strTip);
        }

        // Creates labels for the entries (anim) box
        private void OnAnimBoxMouseMove(object sender, MouseEventArgs e)
        {
            string strTip = "";

            //Get the item
            int nIdx = animBox.IndexFromPoint(e.Location);
            if ((nIdx >= 0) && (nIdx < animBox.Items.Count))
                strTip = animBox.Items[nIdx].ToString();

            toolTip3.SetToolTip(animBox, strTip);
        }

        private void timeTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!bDisableUpdate && !bDisableSubUpdate && !bDisableSubSubUpdate
                && tablefile.table[animBox.SelectedIndex].bHasData
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                try
                {
                    int newTime = int.Parse(timeTextBox.Text);
                    //if (newTime > entry.animTime)
                    //{
                    //    throw new OutOfTimeException();
                    //}
                    entry.subEntries[commandBlocksBox.SelectedIndex].isDisabled = false;
                    entry.subEntries[commandBlocksBox.SelectedIndex].isEdited = true;
                    entry.subEntries[commandBlocksBox.SelectedIndex].tableindex = newTime;
                    entry.subEntries[commandBlocksBox.SelectedIndex].localindex = newTime;
                    /*if (newTime > entry.animTime)
                    {
                        timeTextBox.ForeColor = Color.Gray;
                    }
                    else
                    {*/
                        timeTextBox.ForeColor = Color.White;
                    //}
                    lengthTextBox.ForeColor = Color.White;
                    bDisableSubSubUpdate = true;
                    bDisableSubUpdate = true;
                    int s = commandBlocksBox.SelectedIndex;
                    subDataSource = entry.getSubEntryList();
                    commandBlocksBox.DataSource = subDataSource;
                    bool bDontSelect = true;
                    for (int i = 0; i < entry.subEntries.Count; i++)
                    {
                        if (entry.subEntries[i].isEdited)
                        {
                            entry.subEntries[i].isEdited = false;
                            commandBlocksBox.ClearSelected();
                            commandBlocksBox.SelectedIndex = i;
                            bDontSelect = false;
                            break;
                        }
                    }

                    if (bDontSelect)
                    {
                        commandBlocksBox.SelectedIndex = s;
                    }

                    bDisableSubUpdate = false;
                    bDisableSubSubUpdate = false;
                }
                catch (OutOfTimeException err)
                {
                    lengthTextBox.ForeColor = Color.Red;
                    timeTextBox.ForeColor = Color.Red;
                }
                catch
                {
                    timeTextBox.ForeColor = Color.Red;
                }
            }
            else {
                lengthTextBox.ForeColor = Color.White;
                timeTextBox.ForeColor = Color.White;
            }
        }

        private void lengthTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!bDisableUpdate && !bDisableSubUpdate && !bDisableSubSubUpdate
                && tablefile.table[animBox.SelectedIndex].bHasData
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                try
                {
                    int newTime = int.Parse(lengthTextBox.Text);
                    entry.animTime = newTime;
                    lengthTextBox.Text = lengthTextBox.Text;
                    lengthTextBox.ForeColor = Color.White;
                }
                catch
                {
                    lengthTextBox.ForeColor = Color.Red;
                }
            }
            else
            {
                lengthTextBox.ForeColor = Color.White;
            }
        }

        private void commandBlockCopyButton_Click(object sender, EventArgs e)
        {
            if (tablefile.table[animBox.SelectedIndex].bHasData
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry)
            {
                AELogger.Log("copying sub " + animBox.SelectedIndex + "." + commandBlocksBox.SelectedIndex);
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];


                commandBlockCopyInstance = entry.subEntries[commandBlocksBox.SelectedIndex].Copy();
                
                commandBlockPasteButton.Enabled = true;
                pasteCommandBlockToolStripMenuItem1.Enabled = true;
                pasteCommandBlockToolStripMenuItem.Enabled = true;
            }
        }

        private void commandBlockPasteButton_Click(object sender, EventArgs e)
        {
            if (tablefile.table[animBox.SelectedIndex].bHasData
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry
                && commandBlockCopyInstance != null)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                AELogger.Log("pasting sub to " + animBox.SelectedIndex);

                bDisableSubUpdate = true;
                bDisableSubSubUpdate = true;
                
                entry.subEntries.Add(commandBlockCopyInstance.Copy());
                subDataSource = entry.getSubEntryList();
                commandBlocksBox.DataSource = subDataSource;

                timeTextBox.Enabled = true;
                bDisableSubSubUpdate = false;
                bDisableSubUpdate = false;

                for (int i = 0; i < entry.subEntries.Count; i++)
                {
                    if (entry.subEntries[i].isEdited)
                    {
                        entry.subEntries[i].isEdited = false;
                        commandBlocksBox.SelectedIndex = i;
                        break;
                    }
                }
                if (commandBlocksBox.SelectedItems.Count > 1) // catches multi select after pasting a time
                {
                    commandBlocksBox.SelectedItems.Remove(commandBlocksBox.SelectedItems[0]);
                }
                commandBlocksBox_SelectedIndexChanged(null, null);
            }
        }

        private void commandsCopyButton_Click(object sender, EventArgs e)
        {
            if (tablefile.table[animBox.SelectedIndex].bHasData
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                AELogger.Log("copying subsub " + animBox.SelectedIndex + "." + commandBlocksBox.SelectedIndex + "." + commandsBox.SelectedIndex);
                byte[] source = entry.subEntries[commandBlocksBox.SelectedIndex].subsubEntries[commandsBox.SelectedIndex];
                commandCopyInstance = new byte[source.Length];
                source.CopyTo(commandCopyInstance, 0);

                commandsPasteButton.Enabled = true;
                pasteCommandsToolStripMenuItem.Enabled = true;
                pasteCommandToolStripMenuItem.Enabled = true;
            }
        }

        private void commandsPasteButton_Click(object sender, EventArgs e)
        {
            if (tablefile.table[animBox.SelectedIndex].bHasData
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry
                && commandCopyInstance != null)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                AELogger.Log("pasting subsub to " + animBox.SelectedIndex + "." + commandsBox.SelectedIndex);
                byte[] dest = new byte[commandCopyInstance.Length];
                commandCopyInstance.CopyTo(dest, 0);
                entry.subEntries[commandBlocksBox.SelectedIndex].subsubEntries.Add(dest);
                entry.subEntries[commandBlocksBox.SelectedIndex].subsubPointers.Add(uint.MaxValue);
                entry.subEntries[commandBlocksBox.SelectedIndex].subsubIndices.Add(0);

                bDisableSubUpdate = true;
                bDisableSubSubUpdate = true;
                commandsBox.DataSource = null;
                subsubDataSource = entry.subEntries[commandBlocksBox.SelectedIndex].GetCommandList();
                commandsBox.DataSource = subsubDataSource;

                bDisableSubSubUpdate = false;
                bDisableSubUpdate = false;
                commandsBox.SelectedIndex = commandsBox.Items.Count-1;
            }
        }

        private void commandBlockDeleteButton_Click(object sender, EventArgs e)
        {
            if (tablefile.table[animBox.SelectedIndex].bHasData
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                AnmChrSubEntry commandBlockEntry = entry.subEntries[commandBlocksBox.SelectedIndex];

                if (MessageBox.Show(this, "Deleting command block with " + commandBlockEntry.subsubPointers.Count 
                    + " commands and " + (commandBlockEntry.isDisabled ? "that is disabled" : 
                    ("with timestamp " + commandBlockEntry.localindex.ToString())) + ". Are you sure?" 
                    + Environment.NewLine + "This action is irreversible." + Environment.NewLine, "PERMANENT DELETION", MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Exclamation) != DialogResult.Yes)
                {
                    return;
                }
                AELogger.Log("deleting sub " + animBox.SelectedIndex + "." + commandBlocksBox.SelectedIndex);

                bDisableSubUpdate = true;
                bDisableSubSubUpdate = true;
                isDeleting = true;
                reselectID = commandBlocksBox.SelectedIndex;
                entry.subEntries.RemoveAt(commandBlocksBox.SelectedIndex);
                commandsBox.DataSource = null; //empties command list
                subDataSource = entry.getSubEntryList(); //grabs subchunk list
                commandBlocksBox.DataSource = subDataSource; //applies new subchunk list
                bDisableSubSubUpdate = false;
                bDisableSubUpdate = false;
                if (subDataSource.Count > 0)
                {
                    commandBlocksBox.SelectedIndex = 0;
                    RefreshSelectedIndices();
                    subsubDataSource = commandBlockEntry.GetCommandList();
                    if (reselectID == 0 && commandBlocksBox.Items.Count > 0)
                    {
                        commandBlocksBox_SelectedIndexChanged(null, null);
                    }
                    //commandsBox.DataSource = subsubDataSource; //this caused the commands to be reloaded improperly? need to reload the subchunk
                    //if (commandsBox.SelectedIndex < 0)
                    //{
                    //    commandsBox.SelectedIndex = 0;
                    //}
                }
                else
                {
                    AELogger.Log("odd issue ???????");
                    ClearCommandDetailView("This animation has no command blocks.");
                }

                
                isDeleting = false;
                validateDeleteButtons(entry);
            }
        }

        private void disableCommandBlockToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!bDisableUpdate && !bDisableSubUpdate && !bDisableSubSubUpdate 
    && tablefile.table[animBox.SelectedIndex].bHasData
    && tablefile.table[animBox.SelectedIndex] is AnmChrEntry)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];

                int newTime = 21012;
                entry.subEntries[commandBlocksBox.SelectedIndex].isEdited = true;
                entry.subEntries[commandBlocksBox.SelectedIndex].isDisabled = true;
                entry.subEntries[commandBlocksBox.SelectedIndex].tableindex = entry.subEntries[commandBlocksBox.SelectedIndex].tableindex + newTime;
                entry.subEntries[commandBlocksBox.SelectedIndex].localindex = entry.subEntries[commandBlocksBox.SelectedIndex].localindex + newTime;
                timeTextBox.Clear();

                bDisableSubSubUpdate = true;
                bDisableSubUpdate = true;
                int s = commandBlocksBox.SelectedIndex;
                subDataSource = entry.getSubEntryList();
                commandBlocksBox.DataSource = subDataSource;
                bool bDontSelect = true;
                for (int i = 0; i < entry.subEntries.Count; i++)
                {
                    if (entry.subEntries[i].isEdited)
                    {
                        entry.subEntries[i].isEdited = false;
                        commandBlocksBox.SelectedIndex = i;
                        bDontSelect = false;
                        break;
                    }
                }
                if (commandBlocksBox.SelectedItems.Count > 1) // catches multi select after pasting a time
                {
                    commandBlocksBox.SelectedItems.Remove(commandBlocksBox.SelectedItems[0]);
                }
                if (bDontSelect)
                {
                    commandBlocksBox.SelectedIndex = s;
                }

                bDisableSubUpdate = false;
                bDisableSubSubUpdate = false;

            }
            else
            {
                return;
            }
        }

        private void commandsDeleteButton_Click(object sender, EventArgs e)
        {
            if (tablefile.table[animBox.SelectedIndex].bHasData
                && tablefile.table[animBox.SelectedIndex] is AnmChrEntry)
            {
                AnmChrEntry entry = (AnmChrEntry)tablefile.table[animBox.SelectedIndex];
                AnmChrSubEntry commandBlockEntry = entry.subEntries[commandBlocksBox.SelectedIndex];

                if (entry.subEntries[commandBlocksBox.SelectedIndex].subsubPointers.Count <= 0)
                {
                    MessageBox.Show(this, "There is nothing to delete!"
                    + Environment.NewLine, "INVALID ACTION", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                if (MessageBox.Show(this, "Deleting command [" + (commandsBox.SelectedItem.ToString().Length <= 8 ?
                    commandsBox.SelectedItem.ToString() : commandsBox.SelectedItem.ToString().Substring(0, commandsBox.SelectedItem.ToString()
                    .IndexOf(" "))) + (commandBlockEntry.isDisabled ? "] that belongs to a disabled block" :
                    ("] with timestamp " + commandBlockEntry.localindex.ToString())) + ". Are you sure?"
                    + Environment.NewLine + "This action is irreversible." + Environment.NewLine, "PERMANENT DELETION", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation) != DialogResult.Yes)
                {
                    return;
                }

                AELogger.Log("deleting subsub " + animBox.SelectedIndex + "." + commandBlocksBox.SelectedIndex + "." + commandsBox.SelectedIndex);

                bDisableSubUpdate = true;
                bDisableSubSubUpdate = true;

                commandBlockEntry.subsubPointers.RemoveAt(commandsBox.SelectedIndex);
                commandBlockEntry.subsubEntries.RemoveAt(commandsBox.SelectedIndex);
                commandBlockEntry.subsubIndices.RemoveAt(commandsBox.SelectedIndex);
                commandsBox.DataSource = null;
                subDataSource = entry.getSubEntryList();
                commandBlocksBox.DataSource = subDataSource;

                bDisableSubSubUpdate = false;
                bDisableSubUpdate = false;

                commandBlocksBox.SelectedIndex = 0;
                RefreshSelectedIndices();
                subsubDataSource = commandBlockEntry.GetCommandList();
                commandsBox.DataSource = subsubDataSource;
                if (commandsBox.Items.Count > 0)
                {
                    commandsBox.SelectedIndex = 0;
                }
                validateDeleteButtons(entry);
            }
        }

        private void RefreshCommandDetails()
        {
            if (commandDetailGrid == null)
            {
                return;
            }

            if (tablefile == null || animBox.SelectedIndex < 0 || !(tablefile.table[animBox.SelectedIndex] is AnmChrEntry entry) || !entry.bHasData)
            {
                ClearCommandDetailView("Open a file to view command data.");
                return;
            }

            if (isMultiSelection)
            {
                ClearCommandDetailView("Command details are unavailable while multiple command blocks are selected.");
                return;
            }

            if (commandBlocksBox.SelectedIndex < 0 || commandBlocksBox.SelectedIndex >= entry.subEntries.Count)
            {
                ClearCommandDetailView("Select a command block to inspect its commands.");
                return;
            }

            var block = entry.subEntries[commandBlocksBox.SelectedIndex];
            if (commandsBox.SelectedIndex < 0 || commandsBox.SelectedIndex >= block.subsubEntries.Count)
            {
                ClearCommandDetailView("Select a command to see its byte values.");
                return;
            }

            var data = block.subsubEntries[commandsBox.SelectedIndex];
            CacheCommandTemplate(data);

            isUpdatingCommandDetail = true;
            commandDetailRows.Clear();
            for (int i = 0; i < data.Length; i++)
            {
                commandDetailRows.Add(new CommandByteRow { Index = i, Value = data[i] });
            }
            isUpdatingCommandDetail = false;

            commandDetailGrid.Enabled = commandDetailRows.Count > 0;

            var commandLabel = block.GetSubSubName(commandsBox.SelectedIndex);
            commandDetailHeaderLabel.Text = $"{commandLabel}  •  {data.Length} bytes";
            sizeLabel.Text = $"Command size: {data.Length} bytes";

            if (commandSelector != null)
            {
                isUpdatingCommandSelector = true;
                var definition = EnsureCommandDefinition(GetCommandKey(data), commandLabel);
                commandSelector.Enabled = definition != null;
                if (definition != null)
                {
                    commandSelector.SelectedItem = definition;
                }
                else
                {
                    commandSelector.SelectedIndex = -1;
                    commandSelector.Text = commandLabel;
                }
                isUpdatingCommandSelector = false;
            }
        }

        private void ClearCommandDetailView(string message = "Select a command to view its byte values.")
        {
            if (commandDetailRows == null)
            {
                return;
            }

            isUpdatingCommandDetail = true;
            commandDetailRows.Clear();
            isUpdatingCommandDetail = false;
            commandDetailGrid.Enabled = false;
            commandDetailHeaderLabel.Text = message;
            if (!string.IsNullOrEmpty(message))
            {
                sizeLabel.Text = message;
            }

            if (commandSelector != null)
            {
                isUpdatingCommandSelector = true;
                commandSelector.Enabled = false;
                commandSelector.SelectedIndex = -1;
                commandSelector.Text = "Select a command...";
                isUpdatingCommandSelector = false;
            }
        }

        private void commandDetailGrid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != 1 || e.RowIndex < 0)
            {
                return;
            }

            var text = e.FormattedValue?.ToString();
            if (!byte.TryParse(text, out _))
            {
                e.Cancel = true;
                commandDetailGrid.Rows[e.RowIndex].ErrorText = "Enter a value between 0 and 255.";
            }
            else
            {
                commandDetailGrid.Rows[e.RowIndex].ErrorText = string.Empty;
            }
        }

        private void commandDetailGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isUpdatingCommandDetail || e.RowIndex < 0 || e.ColumnIndex != 1)
            {
                return;
            }

            if (tablefile?.table[animBox.SelectedIndex] is AnmChrEntry entry && entry.bHasData)
            {
                if (commandBlocksBox.SelectedIndex >= 0 && commandBlocksBox.SelectedIndex < entry.subEntries.Count)
                {
                    var block = entry.subEntries[commandBlocksBox.SelectedIndex];
                    if (commandsBox.SelectedIndex >= 0 && commandsBox.SelectedIndex < block.subsubEntries.Count)
                    {
                        var data = block.subsubEntries[commandsBox.SelectedIndex];
                        var row = commandDetailRows[e.RowIndex];
                        data[row.Index] = row.Value;
                        block.subsubEntries[commandsBox.SelectedIndex] = data;
                        block.isEdited = true;
                        CacheCommandTemplate(data);
                        subsubDataSource[commandsBox.SelectedIndex] = block.GetSubSubName(commandsBox.SelectedIndex);
                        sizeLabel.Text = $"Command size: {data.Length} bytes";
                    }
                }
            }
        }

        private void commandDetailGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.Cancel = true;
        }

        private void commandSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdatingCommandSelector || commandSelector == null || commandSelector.SelectedIndex < 0)
            {
                return;
            }

            if (!(commandSelector.SelectedItem is CommandDefinition definition))
            {
                return;
            }

            if (tablefile?.table == null || animBox.SelectedIndex < 0 || animBox.SelectedIndex >= tablefile.table.Count)
            {
                return;
            }

            if (!(tablefile.table[animBox.SelectedIndex] is AnmChrEntry entry) || !entry.bHasData)
            {
                return;
            }

            if (commandBlocksBox.SelectedIndex < 0 || commandBlocksBox.SelectedIndex >= entry.subEntries.Count)
            {
                return;
            }

            var block = entry.subEntries[commandBlocksBox.SelectedIndex];
            if (commandsBox.SelectedIndex < 0 || commandsBox.SelectedIndex >= block.subsubEntries.Count)
            {
                return;
            }

            var currentData = block.subsubEntries[commandsBox.SelectedIndex];
            var newData = BuildCommandData(definition, currentData);

            bool lengthChanged = currentData == null || currentData.Length != newData.Length;
            bool contentChanged = lengthChanged || currentData == null || !currentData.SequenceEqual(newData);

            if (!contentChanged)
            {
                ApplyCommandHeader(newData, definition);
                return;
            }

            block.subsubEntries[commandsBox.SelectedIndex] = newData;
            block.isEdited = true;
            CacheCommandTemplate(newData);

            bDisableSubSubUpdate = true;
            subsubDataSource[commandsBox.SelectedIndex] = block.GetSubSubName(commandsBox.SelectedIndex);
            bDisableSubSubUpdate = false;

            isUpdatingCommandSelector = true;
            try
            {
                RefreshCommandDetails();
            }
            finally
            {
                isUpdatingCommandSelector = false;
            }
        }

        private void CommandsBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                MoveSelectedCommand(-1);
            }
            else if (e.Control && e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                MoveSelectedCommand(1);
            }
        }

        private void CommandBlocksBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                MoveSelectedCommandBlock(-1);
            }
            else if (e.Control && e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                MoveSelectedCommandBlock(1);
            }
        }

        private void MoveSelectedCommand(int offset)
        {
            if (tablefile?.table[animBox.SelectedIndex] is AnmChrEntry entry && entry.bHasData)
            {
                if (commandBlocksBox.SelectedIndex < 0 || commandBlocksBox.SelectedIndex >= entry.subEntries.Count)
                {
                    return;
                }

                var block = entry.subEntries[commandBlocksBox.SelectedIndex];
                if (commandsBox.SelectedIndex < 0 || commandsBox.SelectedIndex >= block.subsubEntries.Count)
                {
                    return;
                }

                int index = commandsBox.SelectedIndex;
                int newIndex = index + offset;
                if (newIndex < 0 || newIndex >= block.subsubEntries.Count)
                {
                    return;
                }

                (block.subsubEntries[index], block.subsubEntries[newIndex]) = (block.subsubEntries[newIndex], block.subsubEntries[index]);
                (block.subsubPointers[index], block.subsubPointers[newIndex]) = (block.subsubPointers[newIndex], block.subsubPointers[index]);
                (block.subsubIndices[index], block.subsubIndices[newIndex]) = (block.subsubIndices[newIndex], block.subsubIndices[index]);
                (subsubDataSource[index], subsubDataSource[newIndex]) = (subsubDataSource[newIndex], subsubDataSource[index]);

                block.isEdited = true;
                commandsBox.SelectedIndex = newIndex;
                RefreshCommandDetails();
            }
        }

        private void MoveSelectedCommandBlock(int offset)
        {
            if (isMultiSelection)
            {
                return;
            }

            if (tablefile?.table[animBox.SelectedIndex] is AnmChrEntry entry && entry.bHasData)
            {
                if (commandBlocksBox.SelectedIndex < 0 || commandBlocksBox.SelectedIndex >= entry.subEntries.Count)
                {
                    return;
                }

                int index = commandBlocksBox.SelectedIndex;
                int newIndex = index + offset;
                if (newIndex < 0 || newIndex >= entry.subEntries.Count)
                {
                    return;
                }

                (entry.subEntries[index], entry.subEntries[newIndex]) = (entry.subEntries[newIndex], entry.subEntries[index]);
                subDataSource = entry.getSubEntryList();
                commandBlocksBox.DataSource = subDataSource;
                commandBlocksBox.SelectedIndex = newIndex;
                entry.subEntries[newIndex].isEdited = true;
            }
        }

        private void AddReorderOptions()
        {
            if (commandContextMenuStrip1.Items.Count > 0)
            {
                commandContextMenuStrip1.Items.Insert(0, new ToolStripMenuItem("Move Command Down", null, (s, e) => MoveSelectedCommand(1)));
                commandContextMenuStrip1.Items.Insert(0, new ToolStripMenuItem("Move Command Up", null, (s, e) => MoveSelectedCommand(-1)));
                commandContextMenuStrip1.Items.Insert(2, new ToolStripSeparator());
            }

            if (commandBlockContextMenuStrip.Items.Count > 0)
            {
                commandBlockContextMenuStrip.Items.Insert(0, new ToolStripMenuItem("Move Block Down", null, (s, e) => MoveSelectedCommandBlock(1)));
                commandBlockContextMenuStrip.Items.Insert(0, new ToolStripMenuItem("Move Block Up", null, (s, e) => MoveSelectedCommandBlock(-1)));
                commandBlockContextMenuStrip.Items.Insert(2, new ToolStripSeparator());
            }

            commandsBox.KeyDown += CommandsBox_KeyDown;
            commandBlocksBox.KeyDown += CommandBlocksBox_KeyDown;
        }

        // Validates if the command related buttons ought to be enabled or not
        private void validateDeleteButtons(AnmChrEntry entry)
        {
            bool hasBlocks = entry?.subEntries.Count > 0;
            bool isDisabled = false;
            if (hasBlocks && commandBlocksBox.SelectedIndex > -1)
            {
                isDisabled = entry.subEntries[commandBlocksBox.SelectedIndex].isDisabled;
            }

            commandBlockDeleteButton.Enabled = hasBlocks && !isMultiSelection;
            deleteCommandBlockToolStripMenuItem1.Enabled = hasBlocks && !isMultiSelection;
            deleteCommandBlockToolStripMenuItem.Enabled = hasBlocks && !isMultiSelection;
            commandBlockCopyButton.Enabled = hasBlocks && !isMultiSelection;
            copyCommandBlockToolStripMenuItem1.Enabled = hasBlocks && !isMultiSelection;
            copyCommandBlockToolStripMenuItem.Enabled = hasBlocks && !isMultiSelection;
            commandBlockDisableButton.Enabled = hasBlocks && !isMultiSelection && !isDisabled;
            disableCommandBlockToolStripMenuItem1.Enabled = hasBlocks && !isMultiSelection && !isDisabled;
            disableCommandBlockToolStripMenuItem.Enabled = hasBlocks && !isMultiSelection && !isDisabled;

            timeTextBox.Enabled = hasBlocks;
            if (!hasBlocks)
            {
                timeTextBox.Clear();
                commandsCopyButton.Enabled = false;
                copyCommandsToolStripMenuItem.Enabled = false;
                commandsDeleteButton.Enabled = false;
                deleteCommandsToolStripMenuItem.Enabled = false;
                ClearCommandDetailView(entry == null ? "Open a file to view command data." : "This animation has no command blocks.");
                return;
            }

            if (isMultiSelection)
            {
                commandsCopyButton.Enabled = false;
                copyCommandsToolStripMenuItem.Enabled = false;
                commandsDeleteButton.Enabled = false;
                deleteCommandsToolStripMenuItem.Enabled = false;
                ClearCommandDetailView("Command details are unavailable while multiple command blocks are selected.");
                return;
            }

            var block = entry.subEntries[commandBlocksBox.SelectedIndex];
            bool hasCommands = block.subsubPointers.Count > 0;
            if (hasCommands)
            {
                RefreshCommandDetails();
            }
            else
            {
                ClearCommandDetailView("This command block has no commands yet.");
            }

            commandsDeleteButton.Enabled = hasCommands;
            deleteCommandsToolStripMenuItem.Enabled = hasCommands;
            commandsCopyButton.Enabled = hasCommands;
            copyCommandsToolStripMenuItem.Enabled = hasCommands;
        }

    } // class

} // ns
