using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniWorldBrowser.Helpers;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Web.WebView2.WinForms;

namespace MiniWorldBrowser.Forms
{
    public class ImageSelectionDialog : Form
    {
        private List<string> _imageUrls;
        private ListView _listView = null!;
        private Button _btnDownload = null!;
        private Button _btnCancel = null!;
        private Label _lblTitle = null!;
        
        private Panel _dirPanel = null!;
        private Label _lblSaveTo = null!;
        private TextBox _txtSavePath = null!;
        private Button _btnBrowse = null!;

        private PictureBox _picPreview = null!;
        private WebView2 _webViewPreview = null!;
        private Panel _previewContainer = null!;
        private Label _lblPreviewInfo = null!;
        private readonly HttpClient _httpClient = new HttpClient();

        public string SelectedSavePath { get; private set; }
        public List<string> SelectedUrls { get; private set; } = new List<string>();

        public ImageSelectionDialog(List<string> imageUrls, string defaultSavePath)
        {
            _imageUrls = imageUrls;
            this.SelectedSavePath = defaultSavePath;
            
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            InitializeUI();
            LoadData();
            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                await _webViewPreview.EnsureCoreWebView2Async();
                _webViewPreview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webViewPreview.CoreWebView2.Settings.AreDevToolsEnabled = false;
            }
            catch { }
        }

        private void InitializeUI()
        {
            this.Text = Localization.T("image_dialog.title");
            this.Size = DpiHelper.Scale(new Size(1000, 700)); // 略微增加尺寸
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252); // 背景色 F8FAFC
            this.Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));

            _lblTitle = new Label
            {
                Text = Localization.T("image_dialog.found_count", new Dictionary<string, string> { { "count", _imageUrls.Count.ToString() } }),
                Location = DpiHelper.Scale(new Point(20, 20)),
                Size = DpiHelper.Scale(new Size(550, 40)),
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(12F), FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59), // Text 色 1E293B
                AutoEllipsis = true
            };

            _listView = new ListView
            {
                Location = DpiHelper.Scale(new Point(0, 0)),
                Size = DpiHelper.Scale(new Size(580, 400)),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                GridLines = false, // 移除网格线，更现代
                BorderStyle = BorderStyle.None,
                BackColor = Color.White
            };

            _listView.Columns.Add(Localization.T("image_dialog.columns.url"), DpiHelper.Scale(450));
            _listView.Columns.Add(Localization.T("image_dialog.columns.type"), DpiHelper.Scale(100));
            _listView.SelectedIndexChanged += OnListViewSelectedIndexChanged;

            // 列表容器
            var listContainer = new Panel
            {
                Location = DpiHelper.Scale(new Point(20, 75)),
                Size = DpiHelper.Scale(new Size(580, 400)),
                BackColor = Color.White,
                Padding = new Padding(1)
            };
            listContainer.Controls.Add(_listView);

            // 预览容器
            _previewContainer = new Panel
            {
                Location = DpiHelper.Scale(new Point(620, 75)),
                Size = DpiHelper.Scale(new Size(350, 400)),
                BackColor = Color.FromArgb(241, 245, 249), // 浅灰蓝背景
                BorderStyle = BorderStyle.None
            };

            _picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Visible = true
            };

            _webViewPreview = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            _lblPreviewInfo = new Label
            {
                Text = Localization.T("image_dialog.preview.select"),
                Dock = DockStyle.Bottom,
                Height = DpiHelper.Scale(35),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(226, 232, 240), // Border 色 E2E8F0
                ForeColor = Color.FromArgb(71, 85, 105), // Muted text 475569
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(8.5F))
            };

            _previewContainer.Controls.Add(_picPreview);
            _previewContainer.Controls.Add(_webViewPreview);
            _previewContainer.Controls.Add(_lblPreviewInfo);

            // 目录选择组件
            _dirPanel = new Panel
            {
                Location = DpiHelper.Scale(new Point(20, 495)),
                Size = DpiHelper.Scale(new Size(950, 85)),
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            _lblSaveTo = new Label
            {
                Text = Localization.T("image_dialog.save_to"),
                Location = DpiHelper.Scale(new Point(15, 12)),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F), FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            _txtSavePath = new TextBox
            {
                Text = SelectedSavePath,
                Location = DpiHelper.Scale(new Point(15, 40)),
                Size = DpiHelper.Scale(new Size(810, 30)),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            _btnBrowse = new Button
            {
                Text = Localization.T("image_dialog.browse"),
                Location = DpiHelper.Scale(new Point(835, 38)),
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
                    Description = Localization.T("image_dialog.browse_desc"),
                    SelectedPath = !string.IsNullOrWhiteSpace(_txtSavePath.Text) ? _txtSavePath.Text : SelectedSavePath
                };
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                {
                    SelectedSavePath = dlg.SelectedPath;
                    _txtSavePath.Text = SelectedSavePath;
                }
            };

            _dirPanel.Controls.AddRange(new Control[] { _lblSaveTo, _txtSavePath, _btnBrowse });

            // 底部按钮区域
            _btnDownload = new Button
            {
                Text = Localization.T("image_dialog.download_now"),
                Location = DpiHelper.Scale(new Point(740, 600)),
                Size = DpiHelper.Scale(new Size(110, 40)),
                BackColor = Color.FromArgb(37, 99, 235), // Primary 2563EB
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9.5F), FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnDownload.FlatAppearance.BorderSize = 0;
            _btnDownload.Click += (s, e) => {
                SelectedUrls = _listView.CheckedItems.Cast<ListViewItem>().Select(i => i.Text).ToList();
                if (SelectedUrls.Count > 0)
                {
                    SelectedSavePath = _txtSavePath.Text;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show(Localization.T("image_dialog.select_one_image"), Localization.T("confirm.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _btnCancel = new Button
            {
                Text = Localization.T("confirm.cancel"),
                Location = DpiHelper.Scale(new Point(860, 600)),
                Size = DpiHelper.Scale(new Size(110, 40)),
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

            var btnSelectAll = new Button
            {
                Text = Localization.T("image_dialog.select_all_toggle"),
                Location = DpiHelper.Scale(new Point(20, 600)),
                Size = DpiHelper.Scale(new Size(120, 40)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(71, 85, 105),
                Cursor = Cursors.Hand
            };
            btnSelectAll.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            bool allSelected = true;
            btnSelectAll.Click += (s, e) => {
                allSelected = !allSelected;
                foreach (ListViewItem item in _listView.Items)
                {
                    item.Checked = allSelected;
                }
            };

            this.Controls.AddRange(new Control[] { _lblTitle, listContainer, _previewContainer, _dirPanel, _btnDownload, _btnCancel, btnSelectAll });
        }

        private void LoadData()
        {
            foreach (var url in _imageUrls)
            {
                string ext = "Unknown";
                try {
                    var uri = new Uri(url);
                    ext = Path.GetExtension(uri.LocalPath).ToUpper().TrimStart('.');
                    if (string.IsNullOrEmpty(ext)) ext = "IMG";
                } catch { }

                var item = new ListViewItem(new[] { url, ext });
                item.Checked = true;
                _listView.Items.Add(item);
            }
        }

        private async void OnListViewSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_listView.SelectedItems.Count == 0) return;

            string url = _listView.SelectedItems[0].Text;
            await UpdatePreviewAsync(url);
        }

        private string _currentPreviewUrl = "";
        private MemoryStream? _currentImageStream;

        private async Task UpdatePreviewAsync(string url)
        {
            if (_currentPreviewUrl == url) return;
            _currentPreviewUrl = url;

            // 1. 清理旧资源
            if (_picPreview.Image != null)
            {
                var oldImg = _picPreview.Image;
                _picPreview.Image = null;
                oldImg.Dispose();
            }
            _currentImageStream?.Dispose();
            _currentImageStream = null;

            _lblPreviewInfo.Text = Localization.T("image_dialog.loading_preview");

            // 2. 决定预览方式
            bool isModernFormat = url.EndsWith(".avif", StringComparison.OrdinalIgnoreCase) || 
                                 url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                                 url.Contains("/avif") || url.Contains("/webp");

            if (isModernFormat)
            {
                // 使用 WebView2 预览现代格式
                _picPreview.Visible = false;
                _webViewPreview.Visible = true;
                if (_webViewPreview.CoreWebView2 != null)
                {
                    // 使用 HTML 居中显示图片并处理缩放
                    string html = $@"<html><body style='margin:0;padding:0;background:#f0f0f0;display:flex;justify-content:center;align-items:center;height:100vh;'>
                                    <img src='{url}' style='max-width:100%;max-height:100%;object-fit:contain;' />
                                    </body></html>";
                    _webViewPreview.CoreWebView2.NavigateToString(html);
                    _lblPreviewInfo.Text = isModernFormat ? Localization.T("image_dialog.modern_preview") : Localization.T("image_dialog.modern");
                }
            }
            else
            {
                // 使用 PictureBox 预览传统格式 (JPG/PNG/GIF/BMP)
                _webViewPreview.Visible = false;
                _picPreview.Visible = true;

                try
                {
                    var data = await _httpClient.GetByteArrayAsync(url);
                    if (_currentPreviewUrl != url) return;

                    _currentImageStream = new MemoryStream(data);
                    var img = Image.FromStream(_currentImageStream);
                    _picPreview.Image = img;
                    _lblPreviewInfo.Text = Localization.T("image_dialog.preview_size", new Dictionary<string, string> { { "width", img.Width.ToString() }, { "height", img.Height.ToString() }, { "kb", (data.Length / 1024).ToString() } });
                }
                catch
                {
                    if (_currentPreviewUrl == url)
                    {
                        // 如果 PictureBox 失败，尝试切换到 WebView2 作为兜底
                        _picPreview.Visible = false;
                        _webViewPreview.Visible = true;
                        if (_webViewPreview.CoreWebView2 != null)
                        {
                            _webViewPreview.CoreWebView2.Navigate(url);
                            _lblPreviewInfo.Text = Localization.T("image_dialog.preview_loading_webview");
                        }
                        else
                        {
                            _lblPreviewInfo.Text = Localization.T("image_dialog.preview_unavailable");
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _picPreview.Image?.Dispose();
                _currentImageStream?.Dispose();
                _webViewPreview?.Dispose();
                _httpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
