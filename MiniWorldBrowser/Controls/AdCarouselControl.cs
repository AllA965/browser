using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 美化后的广告轮播展示框 - 支持动画与高级样式
/// </summary>
public class AdCarouselControl : UserControl
{
    #region Constants

    private const int AnimationInterval = 10;
    private const int CarouselInterval = 5000;
    private const int FetchInterval = 600000;
    private const int AnimationSpeed = 12; // 降低速度使动画更丝滑
    private const int CollapsedSize = 36; // 稍微缩小折叠态尺寸
    private const int PaddingSize = 20; // 移回右下角
    private const int StatusBarHeight = 35; // 对齐状态栏上方
    private const int CornerRadius = 10; // 缩小圆角

    private static readonly Size ExpandedSize = new(147, 220); // 原 220, 330 的 2/3
    private static readonly Color OverlayColor = Color.FromArgb(80, 0, 0, 0);

    #endregion

    #region Fields

    private readonly IAdService _adService;
    private readonly System.Windows.Forms.Timer _carouselTimer;
    private readonly System.Windows.Forms.Timer _fetchTimer;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly System.Windows.Forms.Timer _loadingTimer;

    // UI Components (Initialized in InitializeComponents)
    private PictureBox _pictureBox = null!;
    private RoundedButton _toggleButton = null!; // 仅用于折叠态的悬浮按钮（现已隐藏）

    // 交互区域定义
    private Rectangle _closeRect;
    private Rectangle _prevRect;
    private Rectangle _nextRect;
    private bool _isCloseHover;
    private bool _isPrevHover;
    private bool _isNextHover;

    // State
    private List<AdItem> _ads = new();
    private readonly object _adsLock = new();
    private readonly Dictionary<string, Image> _imageMemoryCache = new();
    private int _currentAdIndex = -1;
    private bool _isCollapsed;
    private bool _isMouseOver;
    private bool _hasInitiallyShown;
    private int _targetWidth;
    private int _targetHeight;
    private bool _isLoadingAdImage;
    private bool _isAdImageLoadFailed;
    private float _loadingAngle;
    private bool _hasStarted;

    public bool AutoExpandOnFirstLoad { get; set; } = true;

    #endregion

    #region Constructor

    public AdCarouselControl()
    {
        _adService = new AdService();
        
        // 优化控件样式
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        
        // 初始化 UI
        InitializeComponents();

        // 初始化定时器
        _carouselTimer = new System.Windows.Forms.Timer { Interval = CarouselInterval };
        _carouselTimer.Tick += CarouselTimer_Tick;

        _fetchTimer = new System.Windows.Forms.Timer { Interval = FetchInterval };
        _fetchTimer.Tick += FetchTimer_Tick;

        _animationTimer = new System.Windows.Forms.Timer { Interval = AnimationInterval };
        _animationTimer.Tick += AnimationTimer_Tick;

        _loadingTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _loadingTimer.Tick += LoadingTimer_Tick;

        this.HandleCreated += (_, __) => StartIfNeeded();
        this.VisibleChanged += (_, __) => StartIfNeeded();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // 初始状态：处于折叠/隐藏态，不显示任何唤出箭头
        this.Visible = false;
        this._isCollapsed = true;
        this.Size = DpiHelper.Scale(new Size(CollapsedSize, CollapsedSize));
        this._targetWidth = Width;
        this._targetHeight = Height;
        
        this.BackColor = Color.Transparent;
        this.DoubleBuffered = true;
        // 确保布局刷新
        this.SizeChanged += (s, e) => { UpdatePosition(); UpdateControlLayout(); };

        // 图片框设置
        _pictureBox = new PictureBox
        {
            Location = Point.Empty,
            Size = this.Size,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent
        };
        _pictureBox.MouseClick += PictureBox_MouseClick;
        _pictureBox.Paint += PictureBox_Paint;
        _pictureBox.MouseDown += PictureBox_MouseDown;
        _pictureBox.MouseMove += PictureBox_MouseMove;
        _pictureBox.MouseLeave += (s, e) => { 
            _isCloseHover = _isPrevHover = _isNextHover = false;
            SetHoverState(false); 
        };

        // 切换按钮设置 - 仅用于折叠态唤出
        _toggleButton = new RoundedButton
        {
            Text = string.Empty,
            Size = DpiHelper.Scale(new Size(CollapsedSize, CollapsedSize)),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(64, 64, 64),
            HoverBackColor = Color.Transparent,
            CornerRadius = CollapsedSize / 2,
            Font = new Font("Segoe UI", DpiHelper.Scale(16F), FontStyle.Bold),
            Cursor = Cursors.Default,
            Visible = false
        };

        // 悬停逻辑
        _pictureBox.MouseEnter += (s, e) => SetHoverState(true);
        
        this.Controls.Add(_pictureBox);
        this.Controls.Add(_toggleButton);

        UpdateControlLayout();
    }

    private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isCollapsed) return;

        bool oldClose = _isCloseHover;
        bool oldPrev = _isPrevHover;
        bool oldNext = _isNextHover;

        _isCloseHover = _closeRect.Contains(e.Location);
        _isPrevHover = _prevRect.Contains(e.Location);
        _isNextHover = _nextRect.Contains(e.Location);

        if (oldClose != _isCloseHover || oldPrev != _isPrevHover || oldNext != _isNextHover)
        {
            _pictureBox.Cursor = (_isCloseHover || _isPrevHover || _isNextHover) ? Cursors.Hand : Cursors.Default;
            _pictureBox.Invalidate();
        }
    }

    private void SetHoverState(bool isOver)
    {
        if (_isCollapsed) return;
        _isMouseOver = isOver;

        if (isOver)
            _carouselTimer.Stop();
        else
            _carouselTimer.Start();
        
        _pictureBox.Invalidate();
    }

    private void StartIfNeeded()
    {
        if (_hasStarted) return;
        if (!IsHandleCreated) return;
        if (IsDisposed) return;

        _hasStarted = true;
        UpdatePosition();
        _ = SafeFetchAdsAsync();
        _carouselTimer.Start();
        _fetchTimer.Start();
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// 安全地获取广告数据，处理异常
    /// </summary>
    private async Task SafeFetchAdsAsync()
    {
        try
        {
            await FetchAdsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AdCarousel] Fetch failed: {ex.Message}");
        }
    }

    private async Task FetchAdsAsync()
    {
        var positions = new[] { "adv_position_04", "adv_position_05" };

        var fetchTasks = positions.Select(async pos =>
        {
            try
            {
                return await _adService.GetAdsAsync("10014", pos);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdCarousel] Fetch {pos} failed: {ex.Message}");
                return new List<AdItem>();
            }
        }).ToArray();

        var results = await Task.WhenAll(fetchTasks);
        var anyAdded = false;
        lock (_adsLock)
        {
            foreach (var list in results)
            {
                if (list == null || list.Count == 0) continue;
                anyAdded |= MergeAdsByUrl(_ads, list) > 0;
            }
        }

        List<AdItem> adsSnapshot;
        lock (_adsLock)
        {
            adsSnapshot = _ads.ToList();
            if (_currentAdIndex == -1 && _ads.Count > 0) _currentAdIndex = 0;
        }

        if (anyAdded && adsSnapshot.Count > 0)
        {
            _ = PreloadAdImagesAsync(adsSnapshot);
        }

        if (!_hasInitiallyShown && adsSnapshot.Count > 0)
        {
            _hasInitiallyShown = true; // 立即置位，防止重入
            _ = EnsureInitialAdReadyAsync();
        }
    }

    private async Task EnsureInitialAdReadyAsync()
    {
        try
        {
            await Task.Delay(500);
            var displayed = await DisplayCurrentAdAsync();
            if (displayed)
            {
                if (AutoExpandOnFirstLoad)
                {
                    await InvokeAsync(() => ExpandWithAnimation());
                }
                return;
            }

            _hasInitiallyShown = false;
        }
        catch
        {
            _hasInitiallyShown = false;
        }
    }

    /// <summary>
    /// 后台预加载所有图片到内存缓存
    /// </summary>
    private async Task PreloadAdImagesAsync(List<AdItem> ads)
    {
        var urls = ads.Select(a => a.AdvUrl).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        var semaphore = new SemaphoreSlim(4, 4);

        var tasks = urls.Select(async url =>
        {
            lock (_imageMemoryCache)
            {
                if (_imageMemoryCache.ContainsKey(url)) return;
            }

            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var img = await ImageHelper.GetImageAsync(url).ConfigureAwait(false);
                if (img == null) return;

                lock (_imageMemoryCache)
                {
                    if (!_imageMemoryCache.ContainsKey(url))
                        _imageMemoryCache[url] = img;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AdCarousel] Preload failed for {url}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// 显示当前索引的广告图片
    /// </summary>
    private async Task<bool> DisplayCurrentAdAsync()
    {
        AdItem? ad;
        lock (_adsLock)
        {
            if (_currentAdIndex < 0 || _currentAdIndex >= _ads.Count) return false;
            ad = _ads[_currentAdIndex];
        }

        if (ad == null || string.IsNullOrWhiteSpace(ad.AdvUrl)) return false;

        try
        {
            _isAdImageLoadFailed = false;
            Image? newImage = null;

            lock (_imageMemoryCache)
            {
                if (_imageMemoryCache.TryGetValue(ad.AdvUrl, out var cachedImg))
                {
                    newImage = cachedImg;
                }
            }

            if (newImage == null)
            {
                await SetLoadingStateAsync(true);

                var loadTask = ImageHelper.GetImageAsync(ad.AdvUrl);
                var timeoutTask = Task.Delay(5000);
                var completed = await Task.WhenAny(loadTask, timeoutTask);

                if (completed == loadTask)
                {
                    newImage = await loadTask;
                    if (newImage != null)
                    {
                        lock (_imageMemoryCache)
                        {
                            _imageMemoryCache[ad.AdvUrl] = newImage;
                        }
                    }
                }
                else
                {
                    _isAdImageLoadFailed = true;
                    await SetLoadingStateAsync(false);
                    await InvokeAsync(() => _pictureBox.Invalidate());
                    return false;
                }
            }
            
            if (newImage != null)
            {
                await InvokeAsync(() =>
                {
                    try
                    {
                        _pictureBox.Image = newImage;
                        _pictureBox.Invalidate();
                    }
                    catch { }
                });

                await SetLoadingStateAsync(false);
                return true;
            }

            _isAdImageLoadFailed = true;
            await SetLoadingStateAsync(false);
            await InvokeAsync(() => _pictureBox.Invalidate());
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AdCarousel] Display failed: {ex.Message}");
            _isAdImageLoadFailed = true;
            await SetLoadingStateAsync(false);
            return false;
        }
    }

    private void UpdateRegion()
    {
        if (this.Width <= 0 || this.Height <= 0) return;
        
        using (var path = GetRoundedPath(new Rectangle(0, 0, this.Width, this.Height), CornerRadius))
        {
            this.Region = new Region(path);
        }
    }

    private void UpdateControlLayout()
    {
        if (_isCollapsed)
        {
            // 折叠/隐藏状态：不再显示任何唤出箭头
            _pictureBox.Visible = false;
            _toggleButton.Visible = false;
            this.Size = DpiHelper.Scale(new Size(CollapsedSize, CollapsedSize));
        }
        else
        {
            // 展开状态
            _pictureBox.Location = new Point(0, 0);
            _pictureBox.Size = this.Size;
            _pictureBox.Visible = true;
            
            _toggleButton.Visible = false;

            // 计算交互区域
            int btnSize = DpiHelper.Scale(32);
            int padding = DpiHelper.Scale(8);
            _closeRect = new Rectangle(this.Width - DpiHelper.Scale(28), DpiHelper.Scale(4), DpiHelper.Scale(24), DpiHelper.Scale(24));
            _prevRect = new Rectangle(padding, (this.Height - btnSize) / 2, btnSize, btnSize);
            _nextRect = new Rectangle(this.Width - btnSize - padding, (this.Height - btnSize) / 2, btnSize, btnSize);

            _pictureBox.BringToFront();
        }
        UpdateRegion();
    }

    private async void NavigateAd(int direction)
    {
        if (_ads.Count <= 1) return;

        // 停止自动轮播定时器，防止手动切换时发生冲突
        _carouselTimer.Stop();

        _currentAdIndex += direction;
        if (_currentAdIndex < 0) _currentAdIndex = _ads.Count - 1;
        if (_currentAdIndex >= _ads.Count) _currentAdIndex = 0;

        await DisplayCurrentAdAsync();

        // 重新启动自动轮播
        _carouselTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!this.Visible || this.Width <= 0 || this.Height <= 0) return;
        base.OnPaint(e);
    }

    private void UpdatePosition()
    {
        if (this.Parent == null) return;

        var x = this.Parent.ClientSize.Width - this.Width - DpiHelper.Scale(PaddingSize);
        var y = this.Parent.ClientSize.Height - this.Height - DpiHelper.Scale(StatusBarHeight);
        
        this.Location = new Point(x, y);
        this.BringToFront(); // 确保始终在最前端
    }

    #endregion

    #region Event Handlers

    private async void FetchTimer_Tick(object? sender, EventArgs e)
    {
        await SafeFetchAdsAsync();
    }

    private async void CarouselTimer_Tick(object? sender, EventArgs e)
    {
        if (_ads.Count <= 1 || _isCollapsed) return;

        _currentAdIndex = (_currentAdIndex + 1) % _ads.Count;
        await DisplayCurrentAdAsync();
    }

    private void PictureBox_MouseClick(object? sender, MouseEventArgs e)
    {
        if (_isCollapsed) return;

        // 1. 优先检查关闭按钮，确保即使在加载中也能关闭
        if (_closeRect.Contains(e.Location))
        {
            CollapseWithAnimation();
            return;
        }

        if (_isAdImageLoadFailed || _pictureBox.Image == null)
        {
            _ = DisplayCurrentAdAsync();
            return;
        }

        // 2. 检查导航按钮
        if (_ads.Count > 1)
        {
            if (_prevRect.Contains(e.Location))
            {
                NavigateAd(-1);
                return;
            }
            if (_nextRect.Contains(e.Location))
            {
                NavigateAd(1);
                return;
            }
        }

        // 3. 检查指示点
        int dotCount = _ads.Count;
        if (dotCount > 1)
        {
            int dotSize = 12;
            int dotSpacing = 8;
            int totalWidth = (dotCount * 6) + ((dotCount - 1) * dotSpacing);
            int startX = (_pictureBox.Width - totalWidth) / 2;
            int y = _pictureBox.Height - 15 - 3;

            for (int i = 0; i < dotCount; i++)
            {
                var hitRect = new Rectangle(startX + (i * (6 + dotSpacing)) - 3, y, dotSize, dotSize);
                if (hitRect.Contains(e.Location)) return;
            }
        }

        // 4. 广告跳转
        if (_currentAdIndex < 0 || _currentAdIndex >= _ads.Count) return;
        var ad = _ads[_currentAdIndex];
        if (string.IsNullOrEmpty(ad.TargetUrl)) return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = ad.TargetUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AdCarousel] Navigation failed: {ex.Message}");
        }
    }

    private void PictureBox_Paint(object? sender, PaintEventArgs e)
    {
        if (_isCollapsed) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // 1. 加载状态层（先绘制，作为背景）
        if (_isLoadingAdImage || _pictureBox.Image == null)
        {
            DrawLoadingOverlay(g);
        }

        // 2. 绘制交互控件（确保在最上层，方便点击）
        if (_isMouseOver)
        {
            // 绘制关闭按钮 (X)
            DrawModernButton(g, _closeRect, "✕", 10, _isCloseHover);

            // 绘制导航箭头
            if (_ads.Count > 1)
            {
                DrawModernButton(g, _prevRect, "‹", 16, _isPrevHover);
                DrawModernButton(g, _nextRect, "›", 16, _isNextHover);
            }
        }

        // 3. 绘制底部指示点
        if (_ads.Count > 1)
        {
            DrawDots(g);
        }
    }

    private void DrawModernButton(Graphics g, Rectangle rect, string text, float fontSize, bool isHover)
    {
        // 磨砂背景
        int alpha = isHover ? 200 : 120;
        using var brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
        g.FillEllipse(brush, rect);

        // 极简图标
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        using var textBrush = new SolidBrush(Color.FromArgb(180, 64, 64, 64));
        g.DrawString(text, font, textBrush, 
            rect.X + (rect.Width - size.Width) / 2 + 1, 
            rect.Y + (rect.Height - size.Height) / 2);
    }

    private void DrawDots(Graphics g)
    {
        int dotCount = _ads.Count;
        int dotSize = DpiHelper.Scale(6);
        int dotSpacing = DpiHelper.Scale(8);
        int totalWidth = (dotCount * dotSize) + ((dotCount - 1) * dotSpacing);
        int startX = (_pictureBox.Width - totalWidth) / 2;
        int y = _pictureBox.Height - DpiHelper.Scale(15);

        for (int i = 0; i < dotCount; i++)
        {
            var dotRect = new Rectangle(startX + (i * (dotSize + dotSpacing)), y, dotSize, dotSize);
            bool isCurrent = (i == _currentAdIndex);
            
            // 采用半透明磨砂质感
            Color dotColor = isCurrent ? Color.FromArgb(200, 255, 255, 255) : Color.FromArgb(100, 255, 255, 255);
            using var brush = new SolidBrush(dotColor);
            g.FillEllipse(brush, dotRect);

            // 如果是当前点，加一个微弱的描边
            if (isCurrent)
            {
                using var dotPen = new Pen(Color.FromArgb(50, 0, 0, 0), DpiHelper.Scale(1));
                g.DrawEllipse(dotPen, dotRect);
            }
        }
    }

    private async void PictureBox_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_ads.Count <= 1 || _isCollapsed) return;

        // 检查是否点击了指示点
        int dotCount = _ads.Count;
        int dotSize = DpiHelper.Scale(12); // 增加点击判定区域
        int dotSpacing = DpiHelper.Scale(8);
        int totalWidth = (dotCount * DpiHelper.Scale(6)) + ((dotCount - 1) * dotSpacing);
        int startX = (_pictureBox.Width - totalWidth) / 2;
        int y = _pictureBox.Height - DpiHelper.Scale(15) - DpiHelper.Scale(3); // 向上偏移一点以覆盖 6x6 的点

        for (int i = 0; i < dotCount; i++)
        {
            var hitRect = new Rectangle(startX + (i * (DpiHelper.Scale(6) + dotSpacing)) - DpiHelper.Scale(3), y, dotSize, dotSize);
            if (hitRect.Contains(e.Location))
            {
                if (_currentAdIndex != i)
                {
                    _currentAdIndex = i;
                    await DisplayCurrentAdAsync();
                    _pictureBox.Invalidate();
                }
                return; // 点击了点就不要触发图片点击跳转了
            }
        }
    }

    private void LoadingTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isLoadingAdImage)
        {
            _loadingTimer.Stop();
            return;
        }

        _loadingAngle += 10f;
        if (_loadingAngle >= 360f) _loadingAngle -= 360f;
        try { _pictureBox.Invalidate(); } catch { }
    }

    private void CollapseWithAnimation()
    {
        // 关闭后彻底隐藏并停止所有与广告相关的活动
        _isCollapsed = true;
        _targetWidth = DpiHelper.Scale(CollapsedSize);
        _targetHeight = DpiHelper.Scale(CollapsedSize);

        _pictureBox.Visible = false;
        _toggleButton.Visible = false;
        this.Visible = false;

        _carouselTimer.Stop();
        _fetchTimer.Stop();
        _animationTimer.Stop();
        _loadingTimer.Stop();
    }

    /// <summary>
    /// 展开控件（带动画）
    /// </summary>
    public void ExpandWithAnimation()
    {
        if (!_isCollapsed) return;
        _isCollapsed = false;
        _targetWidth = DpiHelper.Scale(ExpandedSize.Width);
        _targetHeight = DpiHelper.Scale(ExpandedSize.Height);
        
        // 确保在动画开始前控件是可见的
        this.Visible = true;
        _pictureBox.Visible = true;
        UpdateControlLayout();
        _animationTimer.Start();
    }

    private void ToggleButton_Click(object? sender, EventArgs e)
    {
        if (_isCollapsed)
        {
            ExpandWithAnimation();
            if (_pictureBox.Image == null)
            {
                _ = DisplayCurrentAdAsync();
            }
        }
        else
        {
            CollapseWithAnimation();
        }
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        // 动画逻辑：逐步逼近目标尺寸
        bool widthFinished = AnimateValue(this.Width, _targetWidth, val => this.Width = val);
        bool heightFinished = AnimateValue(this.Height, _targetHeight, val => this.Height = val);

        if (!_isCollapsed)
        {
            _pictureBox.Size = this.Size;
            UpdateControlLayout(); // 动画过程中更新按钮位置
            _toggleButton.BringToFront();
        }

        UpdatePosition();

        if (widthFinished && heightFinished)
        {
            _animationTimer.Stop();
            UpdateControlLayout();
            UpdateRegion(); // 动画结束更新圆角区域
        }
    }

    private static bool AnimateValue(int current, int target, Action<int> updateAction)
    {
        if (Math.Abs(current - target) <= DpiHelper.Scale(AnimationSpeed))
        {
            updateAction(target);
            return true;
        }

        int diff = target - current;
        int step = Math.Max(DpiHelper.Scale(2), Math.Abs(diff) / 4);
        if (step > DpiHelper.Scale(AnimationSpeed)) step = DpiHelper.Scale(AnimationSpeed);

        updateAction(current + (diff > 0 ? step : -step));
        return false;
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (this.Parent != null)
        {
            this.Parent.Resize += (s, ev) => UpdatePosition();
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 生成圆角矩形路径
    /// </summary>
    private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _carouselTimer.Dispose();
            _fetchTimer.Dispose();
            _animationTimer.Dispose();
            _loadingTimer.Dispose();
            
            // 清理图片内存缓存
            lock (_imageMemoryCache)
            {
                foreach (var img in _imageMemoryCache.Values)
                {
                    img.Dispose();
                }
                _imageMemoryCache.Clear();
            }

            if (_adService is IDisposable disposable) 
                disposable.Dispose();
        }
        base.Dispose(disposing);
    }

    public static int MergeAdsByUrl(List<AdItem> target, IEnumerable<AdItem> incoming)
    {
        var existing = target.Select(a => a.AdvUrl).Where(u => !string.IsNullOrWhiteSpace(u)).ToHashSet();
        var added = 0;
        foreach (var ad in incoming)
        {
            if (string.IsNullOrWhiteSpace(ad.AdvUrl)) continue;
            if (!existing.Add(ad.AdvUrl)) continue;
            target.Add(ad);
            added++;
        }
        return added;
    }

    private Task SetLoadingStateAsync(bool isLoading)
    {
        return InvokeAsync(() =>
        {
            if (_isLoadingAdImage == isLoading) return;
            _isLoadingAdImage = isLoading;
            if (isLoading)
            {
                if (!_loadingTimer.Enabled) _loadingTimer.Start();
            }
            else
            {
                if (_loadingTimer.Enabled) _loadingTimer.Stop();
            }
            try { _pictureBox.Invalidate(); } catch { }
        });
    }

    private void DrawLoadingOverlay(Graphics g)
    {
        using var brush = new SolidBrush(OverlayColor);
        g.FillRectangle(brush, _pictureBox.ClientRectangle);

        float centerX = _pictureBox.Width / 2f;
        float centerY = _pictureBox.Height / 2f;
        float radius = DpiHelper.Scale(15f);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.White, DpiHelper.Scale(3f));
        g.DrawArc(pen, centerX - radius, centerY - radius, radius * 2, radius * 2, _loadingAngle, 270);
    }

    private Task InvokeAsync(Action action)
    {
        if (IsDisposed) return Task.CompletedTask;

        if (!IsHandleCreated)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler? onHandleCreated = null;
            onHandleCreated = (_, __) =>
            {
                try { HandleCreated -= onHandleCreated; } catch { }
                if (IsDisposed)
                {
                    tcs.TrySetCanceled();
                    return;
                }

                try
                {
                    action();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };

            try
            {
                HandleCreated += onHandleCreated;
            }
            catch
            {
                tcs.TrySetCanceled();
            }

            if (IsHandleCreated)
            {
                try { HandleCreated -= onHandleCreated; } catch { }
                return InvokeAsync(action);
            }

            return tcs.Task;
        }

        if (!InvokeRequired)
        {
            action();
            return Task.CompletedTask;
        }

        var invokeTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            BeginInvoke(new Action(() =>
            {
                try
                {
                    action();
                    invokeTcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    invokeTcs.TrySetException(ex);
                }
            }));
        }
        catch (Exception ex)
        {
            invokeTcs.TrySetException(ex);
        }
        return invokeTcs.Task;
    }

    #endregion
}
