using System.Text;

namespace SelfCertForge.App.Controls;

/// <summary>
/// Restricts an <see cref="Entry"/> to digits only, cross-platform.
///
/// <para>Why a behavior instead of <c>Keyboard="Numeric"</c>:</para>
/// <para>On Windows, the numeric soft-keyboard hint is a hint only. Hardware
/// keyboards and clipboard paste still let arbitrary characters through. On
/// macCatalyst the numeric keyboard hint shows the standard text keyboard.
/// To get consistent "digits only" behavior on both TFMs we filter the text
/// at the Entry level on every change.</para>
///
/// <para>The filter strips any non-digit characters and re-assigns the
/// sanitized string back to <see cref="Entry.Text"/>. The cursor jumps to
/// the end on filter — acceptable for a short numeric field.</para>
/// </summary>
public sealed class NumericEntryBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.TextChanged += OnTextChanged;
    }

    protected override void OnDetachingFrom(Entry bindable)
    {
        bindable.TextChanged -= OnTextChanged;
        base.OnDetachingFrom(bindable);
    }

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry) return;
        var input = e.NewTextValue ?? string.Empty;
        if (input.Length == 0) return;

        if (IsAllDigits(input)) return;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
            if (c >= '0' && c <= '9') sb.Append(c);

        var sanitized = sb.ToString();
        if (sanitized != entry.Text)
            entry.Text = sanitized;
    }

    private static bool IsAllDigits(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (s[i] < '0' || s[i] > '9') return false;
        return true;
    }
}
