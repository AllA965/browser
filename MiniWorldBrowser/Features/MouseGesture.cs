using MiniWorldBrowser.Controls;

namespace MiniWorldBrowser.Features;

/// <summary>
/// 鼠标手势识别引擎
/// </summary>
public class MouseGesture
{
    public event Action? GestureBack;      // ←
    public event Action? GestureForward;   // →
    public event Action? GestureRefresh;   // ↑↓
    public event Action? GestureClose;     // ↓→ (L形)
    
    private readonly List<Point> _points = new();
    private bool _isTracking;
    private readonly Form _form;
    private GestureOverlay? _overlay;
    
    private const int MinDistance = 30;
    
    /// <summary>
    /// 是否启用鼠标手势
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    public MouseGesture(Form form)
    {
        _form = form;
    }
    
    public void StartTracking(Point startPoint)
    {
        if (!Enabled) return;
        
        _isTracking = true;
        _points.Clear();
        _points.Add(startPoint);
        
        _overlay = new GestureOverlay
        {
            Location = _form.PointToScreen(Point.Empty),
            Size = _form.ClientSize
        };
        _overlay.Show();
    }
    
    public void AddPoint(Point point)
    {
        if (!_isTracking) return;
        _points.Add(point);
        _overlay?.AddPoint(point);
    }
    
    public void EndTracking()
    {
        if (!_isTracking) return;
        _isTracking = false;
        _overlay?.Close();
        _overlay = null;
        
        var gesture = RecognizeGesture();
        ExecuteGesture(gesture);
        _points.Clear();
    }
    
    public void Cancel()
    {
        _isTracking = false;
        _overlay?.Close();
        _overlay = null;
        _points.Clear();
    }
    
    private string RecognizeGesture()
    {
        if (_points.Count < 2) return "";
        
        var directions = new List<char>();
        Point prev = _points[0];
        
        foreach (var p in _points.Skip(1))
        {
            int dx = p.X - prev.X;
            int dy = p.Y - prev.Y;
            
            if (Math.Abs(dx) > MinDistance || Math.Abs(dy) > MinDistance)
            {
                char dir;
                if (Math.Abs(dx) > Math.Abs(dy))
                    dir = dx > 0 ? 'R' : 'L';
                else
                    dir = dy > 0 ? 'D' : 'U';
                
                if (directions.Count == 0 || directions[^1] != dir)
                    directions.Add(dir);
                
                prev = p;
            }
        }
        
        return string.Join("", directions);
    }
    
    private void ExecuteGesture(string gesture)
    {
        switch (gesture)
        {
            case "L": GestureBack?.Invoke(); break;
            case "R": GestureForward?.Invoke(); break;
            case "UD": GestureRefresh?.Invoke(); break;
            case "DR": GestureClose?.Invoke(); break;
        }
    }
}
