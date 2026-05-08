using System.Collections.ObjectModel;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly ICertificateStore _store;
    private readonly IActivityLog _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly ITrustStoreChecker? _trustChecker;

    public DashboardViewModel(ICertificateStore store, IActivityLog log, ITrustStoreChecker? trustChecker = null)
        : this(store, log, () => DateTimeOffset.UtcNow, trustChecker) { }

    internal DashboardViewModel(ICertificateStore store, IActivityLog log, Func<DateTimeOffset> now, ITrustStoreChecker? trustChecker = null)
    {
        _store = store;
        _log = log;
        _now = now;
        _trustChecker = trustChecker;
        _store.Changed += (_, _) => Refresh();
        _log.Changed += (_, _) => Refresh();
        if (_trustChecker is not null)
            _trustChecker.Changed += (_, _) => Refresh();
        Refresh();
    }

    public int TotalCertificates { get; private set; }
    public int RootAuthorities { get; private set; }
    public int InstalledRoots { get; private set; }
    public int ExpiringSoon { get; private set; }

    public ObservableCollection<ActivityRowViewModel> Activity { get; } = new();

    public bool HasActivity => Activity.Count > 0;
    public bool IsActivityEmpty => Activity.Count == 0;

    public Task LoadAsync(CancellationToken ct = default) =>
        Task.WhenAll(_store.LoadAsync(ct), _log.LoadAsync(ct));

    private void Refresh()
    {
        var now = _now();
        var all = _store.All;
        var children = all.Where(c => c.Kind == StoredCertificateKind.Child).ToList();
        var roots = all.Where(c => c.Kind == StoredCertificateKind.Root).ToList();

        TotalCertificates = children.Count;
        RootAuthorities = roots.Count;
        InstalledRoots = _trustChecker is null
            ? roots.Count(r => r.InstalledInTrustStore)
            : roots.Count(r => _trustChecker.IsTrusted(r.Sha1));
        ExpiringSoon = children.Count(c =>
        {
            var kind = CertificateStatus.DeriveChildKind(c, all, now);
            return kind is "expiring" or "expired";
        });

        Activity.Clear();
        foreach (var entry in _log.Recent.Take(20))
        {
            Activity.Add(new ActivityRowViewModel(entry, now));
        }

        OnPropertyChanged(nameof(TotalCertificates));
        OnPropertyChanged(nameof(RootAuthorities));
        OnPropertyChanged(nameof(InstalledRoots));
        OnPropertyChanged(nameof(ExpiringSoon));
        OnPropertyChanged(nameof(HasActivity));
        OnPropertyChanged(nameof(IsActivityEmpty));
    }
}

public sealed class ActivityRowViewModel
{
    internal ActivityRowViewModel(ActivityEntry entry, DateTimeOffset now)
    {
        Message = entry.Message;
        Kind = entry.Kind;
        TimeLabel = FormatRelative(now - entry.At);
    }

    public string Message { get; }
    public string Kind { get; }
    public string TimeLabel { get; }

    private static string FormatRelative(TimeSpan delta)
    {
        if (delta < TimeSpan.FromMinutes(1)) return "just now";
        if (delta < TimeSpan.FromHours(1))   return $"{(int)delta.TotalMinutes}m ago";
        if (delta < TimeSpan.FromDays(1))    return $"{(int)delta.TotalHours}h ago";
        if (delta < TimeSpan.FromDays(30))   return $"{(int)delta.TotalDays}d ago";
        return $"{(int)(delta.TotalDays / 30)}mo ago";
    }
}
