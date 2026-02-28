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
        private readonly dynamic _videoInfo;
        private ListView _listView;
        private Button _btnDownload;
        private Button _btnCancel;
        private Label _lblTitle;
        private PictureBox _picThumbnail;

        public string SelectedFormatId { get; private set; }

        public VideoSelectionDialog(string jsonInfo)
        {
            _videoInfo = JsonSerializer.Deserialize<dynamic>(jsonInfo);
            InitializeUI();
            LoadData();
        }

        private void InitializeUI()
        {
            this.Text = "选择下载格式";
            this.Size = DpiHelper.Scale(new Size(600, 500));
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;
            this.Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));

            _lblTitle = new Label
            {
                Location = DpiHelper.Scale(new Point(15, 15)),
                Size = DpiHelper.Scale(new Size(550, 40)),
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10F), FontStyle.Bold),
                AutoEllipsis = true
            };

            _listView = new ListView
            {
                Location = DpiHelper.Scale(new Point(15, 70)),
                Size = DpiHelper.Scale(new Size(550, 320)),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true
            };

            _listView.Columns.Add("分辨率", DpiHelper.Scale(120));
            _listView.Columns.Add("扩展名", DpiHelper.Scale(80));
            _listView.Columns.Add("大小", DpiHelper.Scale(100));
            _listView.Columns.Add("备注", DpiHelper.Scale(220));

            _btnDownload = new Button
            {
                Text = "下载所选格式",
                Location = DpiHelper.Scale(new Point(360, 410)),
                Size = DpiHelper.Scale(new Size(100, 35)),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnDownload.FlatAppearance.BorderSize = 0;
            _btnDownload.Click += (s, e) => {
                if (_listView.SelectedItems.Count > 0)
                {
                    SelectedFormatId = _listView.SelectedItems[0].Tag.ToString();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("请先选择一个格式", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _btnCancel = new Button
            {
                Text = "取消",
                Location = DpiHelper.Scale(new Point(465, 410)),
                Size = DpiHelper.Scale(new Size(100, 35)),
                FlatStyle = FlatStyle.Flat
            };
            _btnCancel.Click += (s, e) => {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.AddRange(new Control[] { _lblTitle, _listView, _btnDownload, _btnCancel });
        }

        private void LoadData()
        {
            try
            {
                var root = (JsonElement)_videoInfo;
                _lblTitle.Text = root.GetProperty("title").GetString();
                
                var formats = root.GetProperty("formats");
                foreach (var f in formats.EnumerateArray())
                {
                    string res = f.TryGetProperty("resolution", out var resProp) ? resProp.GetString() : "未知";
                    string ext = f.TryGetProperty("ext", out var extProp) ? extProp.GetString() : "";
                    string note = f.TryGetProperty("note", out var noteProp) ? noteProp.GetString() : "";
                    string formatId = f.GetProperty("format_id").GetString();
                    
                    long sizeBytes = f.TryGetProperty("filesize", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.Number ? sizeProp.GetInt64() : 0;
                    string sizeStr = sizeBytes > 0 ? $"{(double)sizeBytes / 1024 / 1024:F2} MB" : "未知";

                    var item = new ListViewItem(new[] { res, ext, sizeStr, note });
                    item.Tag = formatId;
                    _listView.Items.Add(item);
                }

                if (_listView.Items.Count > 0)
                {
                    _listView.Items[0].Selected = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解析视频信息失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
