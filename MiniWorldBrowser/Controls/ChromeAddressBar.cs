using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiniWorldBrowser.Controls
{
    public class ChromeAddressBar : UserControl
    {
        private TextBox _textBox;
        private bool _isHovered = false;
        private bool _isFocused = false;
        private bool _isDropdownOpen = false;
        private bool _isDarkMode = false;
        
        // Chrome Colors
        private Color _idleBackColor = Color.FromArgb(241, 243, 244); // Google Grey 100 #F1F3F4
        private Color _activeBackColor = Color.White;
        private Color _focusRingColor = Color.FromArgb(26, 115, 232); // Google Blue 600 #1A73E8
        private Color _hoverBackColor = Color.FromArgb(232, 234, 237); // Google Grey 200 #E8EAED
        private Color _textColor = Color.FromArgb(32, 33, 36);

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    UpdateColors();
                    Invalidate();
                }
            }
        }

        private void UpdateColors()
        {
            if (_isDarkMode)
            {
                _idleBackColor = Color.FromArgb(40, 44, 52); // Darker
                _activeBackColor = Color.FromArgb(33, 37, 43); // Dark background
                _hoverBackColor = Color.FromArgb(50, 56, 66);
                _textColor = Color.White;
            }
            else
            {
                _idleBackColor = Color.FromArgb(241, 243, 244);
                _activeBackColor = Color.White;
                _hoverBackColor = Color.FromArgb(232, 234, 237);
                _textColor = Color.FromArgb(32, 33, 36);
            }
            _textBox.BackColor = _isFocused || _isDropdownOpen ? _activeBackColor : _idleBackColor;
            _textBox.ForeColor = _textColor;
        }
        
        public event EventHandler? EnterKeyPressed;
        public new event EventHandler? TextChanged;

        public bool IsDropdownOpen
        {
            get => _isDropdownOpen;
            set
            {
                if (_isDropdownOpen != value)
                {
                    _isDropdownOpen = value;
                    this.Invalidate();
                }
            }
        }

        public ChromeAddressBar()
        {
            this.DoubleBuffered = true;
            this.Padding = new Padding(16, 0, 12, 0); // Left padding for icon space, right for buttons
            this.Size = new Size(500, 34);
            this.BackColor = Color.Transparent;

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = _idleBackColor,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = _textColor,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            
            // Adjust vertical centering manually since TextBox doesn't support vertical alignment directly
            // We use a panel or padding to center it. Here we use the control's padding.
            // But standard TextBox ignores Top padding. We need to handle resizing.

            _textBox.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            _textBox.MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
            _textBox.GotFocus += (s, e) => { 
                _isFocused = true; 
                UpdateState(); 
                base.OnGotFocus(e); // Bubble up
            };
            _textBox.LostFocus += (s, e) => { 
                _isFocused = false; 
                UpdateState(); 
                base.OnLostFocus(e); // Bubble up
            };
            _textBox.TextChanged += (s, e) => TextChanged?.Invoke(this, e);
            _textBox.KeyDown += (s, e) => 
            {
                if (e.KeyCode == Keys.Enter)
                {
                    EnterKeyPressed?.Invoke(this, EventArgs.Empty);
                    e.SuppressKeyPress = true; // Prevent ding sound
                }
                base.OnKeyDown(e);
            };

            // Container for TextBox to handle vertical alignment
            var textContainer = new Panel 
            { 
                Dock = DockStyle.Fill, 
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 0) // Push text down
            };
            textContainer.Controls.Add(_textBox);

            this.Controls.Add(textContainer);
            
            // Forward clicks to textbox
            this.Click += (s, e) => _textBox.Focus();
            textContainer.Click += (s, e) => _textBox.Focus();
        }

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public override string? Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value ?? string.Empty;
        }

        public int SelectionStart
        {
            get => _textBox.SelectionStart;
            set => _textBox.SelectionStart = value;
        }
        
        public int SelectionLength
        {
            get => _textBox.SelectionLength;
            set => _textBox.SelectionLength = value;
        }

        public void SelectAll() => _textBox.SelectAll();

        private void UpdateState()
        {
            var targetColor = _isFocused ? _activeBackColor : (_isHovered ? _hoverBackColor : _idleBackColor);
            _textBox.BackColor = targetColor;
            this.Invalidate();
        }
        
        // Expose TextBox events and properties needed by MainForm
        public new bool Focused => _textBox.Focused;
        public new void Focus() => _textBox.Focus();

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            int radius = this.Height / 2; // Full capsule

            using var path = GetAddressBarPath(rect, radius);
            
            // 1. Fill Background
            var bgColor = (_isFocused || _isDropdownOpen) ? _activeBackColor : (_isHovered ? _hoverBackColor : _idleBackColor);
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            // 2. Draw Focus Ring or Border
            if (_isFocused || _isDropdownOpen)
            {
                using var pen = new Pen(_focusRingColor, 2f);
                // Inset slightly to keep border within bounds
                var borderRect = rect;
                borderRect.Inflate(-1, -1);
                
                if (_isDropdownOpen)
                {
                    // 下拉框打开时，不绘制底部边框，实现一体化效果
                    int d = (radius - 1) * 2;
                    using var borderPath = new GraphicsPath();
                    borderPath.AddLine(borderRect.X, borderRect.Bottom, borderRect.X, borderRect.Y + radius - 1);
                    borderPath.AddArc(borderRect.X, borderRect.Y, d, d, 180, 90);
                    borderPath.AddArc(borderRect.Right - d, borderRect.Y, d, d, 270, 90);
                    borderPath.AddLine(borderRect.Right, borderRect.Y + radius - 1, borderRect.Right, borderRect.Bottom);
                    e.Graphics.DrawPath(pen, borderPath);

                    // 绘制一个非常浅的分割线（可选，Chrome 有时会有）
                    using var separatorPen = new Pen(Color.FromArgb(241, 243, 244), 1f);
                    e.Graphics.DrawLine(separatorPen, borderRect.X + 1, borderRect.Bottom, borderRect.Right - 1, borderRect.Bottom);
                }
                else
                {
                    using var borderPath = GetAddressBarPath(borderRect, radius - 1);
                    e.Graphics.DrawPath(pen, borderPath);
                }
            }
        }

        private GraphicsPath GetAddressBarPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            
            // Ensure diameter doesn't exceed dimensions
            if (d > rect.Height) d = rect.Height;
            if (d > rect.Width) d = rect.Width;

            if (_isDropdownOpen)
            {
                // Dropdown open: Rounded top, square bottom
                path.AddArc(rect.X, rect.Y, d, d, 180, 90); // Top-left
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90); // Top-right
                path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom); // Bottom edge (flat)
                path.CloseFigure();
            }
            else
            {
                // Capsule shape
                path.AddArc(rect.X, rect.Y, d, d, 90, 180); // Left arc
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180); // Right arc
                path.CloseFigure();
            }
            return path;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}
