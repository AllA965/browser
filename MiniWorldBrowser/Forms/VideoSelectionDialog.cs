using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniWorldBrowser.Helpers;
using System.Text.Json;

namespace MiniWorldBrowser.Forms
{
    public class VideoSelectionDialog : Form
    {
        private readonly JsonElement _videoInfo;
        private ListView _listView = null!;
        private Button _btnDownload = null!;
        private Button _btnCancel = null!;
        private Label _lblTitle = null!;
        private PictureBox _picThumbnail = null!; // 虽然警告说未使用，但保留定义以防后续扩展

        private Panel _dirPanel = null!;
        private Label _lblSaveTo = null!;
        private TextBox _txtSavePath = null!;
        private Button _btnBrowse = null!;

        public string SelectedFormatId { get; private set; } = string.Empty;
        public string SelectedSavePath { get; private set; } = string.Empty;

        public VideoSelectionDialog(string jsonInfo, string defaultSavePath)
        {
            this.SelectedSavePath = defaultSavePath;
            try
            {
                using var doc = JsonDocument.Parse(jsonInfo);
                _videoInfo = doc.RootElement.Clone();
            }
            catch
            {
                // 解析失败时抛出异常
                throw new Exception("无法解析视频信息 JSON。");
            }
            
            InitializeUI();
            LoadData();
        }

        private void InitializeUI()
        {
            this.Text = Localization.T("video_dialog.title");
            this.Size = DpiHelper.Scale(new Size(620, 620)); // 略微调整尺寸
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252); // 背景色 F8FAFC
            this.Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));

            _lblTitle = new Label
            {
                Location = DpiHelper.Scale(new Point(20, 20)),
                Size = DpiHelper.Scale(new Size(580, 45)),
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(11F), FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59), // Text 色 1E293B
                AutoEllipsis = true
            };

            _listView = new ListView
            {
                Location = DpiHelper.Scale(new Point(0, 0)),
                Size = DpiHelper.Scale(new Size(580, 320)),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = false, // 移除网格线
                BorderStyle = BorderStyle.None,
                BackColor = Color.White
            };

            _listView.Columns.Add(Localization.T("video_dialog.columns.resolution"), DpiHelper.Scale(120));
            _listView.Columns.Add(Localization.T("video_dialog.columns.ext"), DpiHelper.Scale(80));
            _listView.Columns.Add(Localization.T("video_dialog.columns.size"), DpiHelper.Scale(100));
            _listView.Columns.Add(Localization.T("video_dialog.columns.note"), DpiHelper.Scale(250));

            // 列表容器
            var listContainer = new Panel
            {
                Location = DpiHelper.Scale(new Point(20, 75)),
                Size = DpiHelper.Scale(new Size(580, 320)),
                BackColor = Color.White,
                Padding = new Padding(1)
            };
            listContainer.Controls.Add(_listView);

            // 目录选择组件
            _dirPanel = new Panel
            {
                Location = DpiHelper.Scale(new Point(20, 415)),
                Size = DpiHelper.Scale(new Size(580, 85)),
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            _lblSaveTo = new Label
            {
                Text = Localization.T("video_dialog.save_to"),
                Location = DpiHelper.Scale(new Point(15, 12)),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F), FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            _txtSavePath = new TextBox
            {
                Text = SelectedSavePath,
                Location = DpiHelper.Scale(new Point(15, 40)),
                Size = DpiHelper.Scale(new Size(440, 30)),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            _btnBrowse = new Button
            {
                Text = Localization.T("video_dialog.browse"),
                Location = DpiHelper.Scale(new Point(465, 38)),
                Size = DpiHelper.Scale(new Size(100, 32)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(37, 99, 235), // Primary 2563EB
                Cursor = Cursors.Hand
            };
            _btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(37, 99, 235);
            _btnBrowse.Enabled = true;
            _btnBrowse.Visible = true;
            _btnBrowse.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog
                {
                    Description = Localization.T("video_dialog.browse_desc"),
                    SelectedPath = !string.IsNullOrWhiteSpace(_txtSavePath.Text) ? _txtSavePath.Text : SelectedSavePath
                };
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                {
                    SelectedSavePath = dlg.SelectedPath;
                    _txtSavePath.Text = SelectedSavePath;
                }
            };

            _dirPanel.Controls.AddRange(new Control[] { _lblSaveTo, _txtSavePath, _btnBrowse });

            _btnDownload = new Button
            {
                Text = Localization.T("video_dialog.start_download"),
                Location = DpiHelper.Scale(new Point(340, 520)),
                Size = DpiHelper.Scale(new Size(120, 40)),
                BackColor = Color.FromArgb(37, 99, 235), // Primary 2563EB
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9.5F), FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnDownload.FlatAppearance.BorderSize = 0;
            _btnDownload.Click += (s, e) => {
                if (_listView.SelectedItems.Count > 0)
                {
                    SelectedFormatId = _listView.SelectedItems[0].Tag?.ToString() ?? "";
                    SelectedSavePath = _txtSavePath.Text;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show(Localization.T("video_dialog.select_format_first"), Localization.T("confirm.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _btnCancel = new Button
            {
                Text = Localization.T("confirm.cancel"),
                Location = DpiHelper.Scale(new Point(470, 520)),
                Size = DpiHelper.Scale(new Size(120, 40)),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(100, 116, 139),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            _btnCancel.Click += (s, e) => {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.AddRange(new Control[] { _lblTitle, listContainer, _dirPanel, _btnDownload, _btnCancel });
        }

        private void LoadData()
        {
            try
            {
                _lblTitle.Text = _videoInfo.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : Localization.T("video_dialog.unknown_title");
                
                if (_videoInfo.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in formats.EnumerateArray())
                    {
                        string res = f.TryGetProperty("resolution", out var resProp) ? resProp.GetString() ?? Localization.T("video_dialog.unknown") : Localization.T("video_dialog.unknown");
                        string ext = f.TryGetProperty("ext", out var extProp) ? extProp.GetString() ?? "" : "";
                        string note = f.TryGetProperty("note", out var noteProp) ? noteProp.GetString() ?? "" : "";
                        string formatId = f.TryGetProperty("format_id", out var idProp) ? idProp.GetString() ?? "" : "";
                        
                        if (string.IsNullOrEmpty(formatId)) continue;
                        
                        long sizeBytes = f.TryGetProperty("filesize", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.Number ? sizeProp.GetInt64() : 0;
                        string sizeStr = sizeBytes > 0 ? $"{(double)sizeBytes / 1024 / 1024:F2} MB" : Localization.T("video_dialog.unknown");

                        var item = new ListViewItem(new[] { res, ext, sizeStr, note });
                        item.Tag = formatId;
                        _listView.Items.Add(item);
                    }
                }

                if (_listView.Items.Count > 0)
                {
                    _listView.Items[0].Selected = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Localization.T("video_dialog.parse_failed", new Dictionary<string, string> { { "msg", ex.Message } }), Localization.T("confirm.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
