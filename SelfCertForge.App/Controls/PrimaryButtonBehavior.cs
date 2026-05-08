namespace SelfCertForge.App.Controls;

public class PrimaryButtonBehavior : Behavior<Border>
{
    private Border? _border;
    private PointerGestureRecognizer? _pointer;
    private Color? _normalColor;
    private bool _isPressed;
    private bool _isHovered;

    protected override void OnAttachedTo(Border border)
    {
        base.OnAttachedTo(border);
        _border = border;
        _normalColor = border.BackgroundColor;

        _pointer = new PointerGestureRecognizer();
        _pointer.PointerEntered += OnPointerEntered;
        _pointer.PointerExited += OnPointerExited;
        _pointer.PointerPressed += OnPointerPressed;
        _pointer.PointerReleased += OnPointerReleased;
        border.GestureRecognizers.Add(_pointer);
    }

    protected override void OnDetachingFrom(Border border)
    {
        if (_pointer != null)
        {
            _pointer.PointerEntered -= OnPointerEntered;
            _pointer.PointerExited -= OnPointerExited;
            _pointer.PointerPressed -= OnPointerPressed;
            _pointer.PointerReleased -= OnPointerReleased;
            border.GestureRecognizers.Remove(_pointer);
            _pointer = null;
        }
        _border = null;
        base.OnDetachingFrom(border);
    }

    private void OnPointerEntered(object? s, PointerEventArgs e)
    {
        _isHovered = true;
        UpdateColor();
    }

    private void OnPointerExited(object? s, PointerEventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        UpdateColor();
    }

    private void OnPointerPressed(object? s, PointerEventArgs e)
    {
        _isPressed = true;
        UpdateColor();
    }

    private void OnPointerReleased(object? s, PointerEventArgs e)
    {
        _isPressed = false;
        UpdateColor();
    }

    private void UpdateColor()
    {
        if (_border == null) return;

        if (_isPressed)
            _border.BackgroundColor = GetColor("ColorAccentPrimaryPressed");
        else if (_isHovered)
            _border.BackgroundColor = GetColor("ColorAccentPrimaryHover");
        else
            _border.BackgroundColor = _normalColor;
    }

    private static Color GetColor(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c)
            return c;
        return Colors.Transparent;
    }
}
