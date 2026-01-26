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
        private Color _idleBackColor = Color.White; // Changed to White for modern look
        private Color _activeBackColor = Color.White;
        private Color _focusRingColor = Color.Transparent; // 用户要求透明或灰色，这里设为透明以移除蓝色边框，或者使用淡灰色
        private Color _borderColor = Color.FromArgb(218, 220, 224); // 新增边框颜色 Google Grey 300
        private Color _hoverBackColor = Color.FromArgb(241, 243, 244); // Light grey for hover
        private Color _textColor = Color.FromArgb(32, 33, 36);
        
        // Expanded corner radius
        private int _expandedCornerRadius = 16;

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
                _idleBackColor = Color.White;
                _activeBackColor = Color.White;
                _hoverBackColor = Color.FromArgb(241, 243, 244);
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
#pragma warning disable CS8765
        public override string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value;
        }
#pragma warning restore CS8765

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
            
            // 1. Fill Background
            var bgColor = (_isFocused || _isDropdownOpen) ? _activeBackColor : (_isHovered ? _hoverBackColor : _idleBackColor);
            
            // Determine path based on state
            using var path = new GraphicsPath();
            
            if (_isDropdownOpen)
            {
                // Expanded state: Top rounded, bottom flat
                int d = _expandedCornerRadius * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90); // Top-left
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90); // Top-right
                path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom); // Bottom edge
                path.CloseFigure();
            }
            else
            {
                // Default state: Capsule
                int d = rect.Height; // Diameter = Height
                path.AddArc(rect.X, rect.Y, d, d, 90, 180); // Left arc
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180); // Right arc
                path.CloseFigure();
            }

            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            // 2. Draw Focus Ring or Border
            if (_isFocused || _isDropdownOpen)
            {
                // 使用灰色边框替代原来的蓝色聚焦环
                // 如果是 DropdownOpen 状态，使用透明色或者灰色，这里统一用灰色保持轮廓
                using var pen = new Pen(_borderColor, 1f); 
                
                if (_isDropdownOpen)
                {
                    // Draw top and sides only
                    using var borderPath = new GraphicsPath();
                    int d = _expandedCornerRadius * 2;
                    
                    // Start from bottom-left
                    borderPath.AddLine(rect.X, rect.Bottom, rect.X, rect.Y + _expandedCornerRadius);
                    borderPath.AddArc(rect.X, rect.Y, d, d, 180, 90); // Top-left
                    borderPath.AddArc(rect.Right - d, rect.Y, d, d, 270, 90); // Top-right
                    borderPath.AddLine(rect.Right, rect.Y + _expandedCornerRadius, rect.Right, rect.Bottom);
                    
                    e.Graphics.DrawPath(pen, borderPath);
                }
                else
                {
                    // Full border for capsule
                    e.Graphics.DrawPath(pen, path);
                }
            }
            else
            {
                // Idle border
                if (!IsDarkMode)
                {
                    using var borderPen = new Pen(Color.FromArgb(20, 0, 0, 0), 1f);
                    e.Graphics.DrawPath(borderPen, path);
                }
            }
        }

        // Helper not used anymore but kept if needed for reference, or can be removed.
        // Simplified OnPaint handles logic directly.
        private GraphicsPath GetAddressBarPath(Rectangle rect, int radius)
        {
            return new GraphicsPath(); // Placeholder
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}
