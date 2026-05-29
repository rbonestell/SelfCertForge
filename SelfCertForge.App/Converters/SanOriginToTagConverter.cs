using System.Globalization;
using SelfCertForge.Core.Models;

namespace SelfCertForge.App.Converters;

public sealed class SanOriginToTagConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CsrSignedSanOrigin o && o == CsrSignedSanOrigin.FromCsr ? "From CSR" : "Added";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
