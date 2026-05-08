namespace SelfCertForge.App.Controls;

/// <summary>
/// Adds hover/press highlight to a menu item row via semi-transparent white overlays.
/// Designed for rows sitting on a dark surface (e.g. ColorAccentPrimaryDeep dropdown card).
/// </summary>
public class MenuItemHoverBehavior : Behavior<View>
{
    private static readonly Color HoverColor   = Color.FromArgb("#19FFFFFF");
    private static readonly Color PressedColor = Color.FromArgb("#33FFFFFF");

    private View? _view;
    private PointerGestureRecognizer? _pointer;
    private bool _isPressed;
    private bool _isHovered;

    protected override void OnAttachedTo(View view)
    {
        base.OnAttachedTo(view);
        _view = view;

        _pointer = new PointerGestureRecognizer();
        _pointer.PointerEntered  += OnPointerEntered;
        _pointer.PointerExited   += OnPointerExited;
        _pointer.PointerPressed  += OnPointerPressed;
        _pointer.PointerReleased += OnPointerReleased;
        view.GestureRecognizers.Add(_pointer);
    }

    protected override void OnDetachingFrom(View view)
    {
        if (_pointer != null)
        {
            _pointer.PointerEntered  -= OnPointerEntered;
            _pointer.PointerExited   -= OnPointerExited;
            _pointer.PointerPressed  -= OnPointerPressed;
            _pointer.PointerReleased -= OnPointerReleased;
            view.GestureRecognizers.Remove(_pointer);
            _pointer = null;
        }
        _view = null;
        base.OnDetachingFrom(view);
    }

    private void OnPointerEntered(object? s, PointerEventArgs e)  { _isHovered = true;  UpdateColor(); }
    private void OnPointerExited(object? s, PointerEventArgs e)   { _isHovered = false; _isPressed = false; UpdateColor(); }
    private void OnPointerPressed(object? s, PointerEventArgs e)  { _isPressed = true;  UpdateColor(); }
    private void OnPointerReleased(object? s, PointerEventArgs e) { _isPressed = false; UpdateColor(); }

    private void UpdateColor()
    {
        if (_view == null) return;
        _view.BackgroundColor = _isPressed ? PressedColor : _isHovered ? HoverColor : Colors.Transparent;
    }
}
