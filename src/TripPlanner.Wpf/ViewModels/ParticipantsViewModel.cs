using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

public sealed partial class ParticipantsViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public ParticipantsViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    public ObservableCollection<ParticipantRow> Items { get; } = new();

    // Current-user perspective
    [ObservableProperty] private bool _isOrganizerMe;
    [ObservableProperty] private string _organizerDisplay = "Organizer: —";

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        Items.Clear();
        IsOrganizerMe = false;
        OrganizerDisplay = "Organizer: —";

        var list = await _client.ListParticipantsAsync(tripId);
        if (list is null) return;

        foreach (var p in list)
        {
            // Read properties via reflection to tolerate your recent DTO change.
            var pid          = GetString(p, "ParticipantId");
            var displayName  = GetString(p, "DisplayName");
            var isPlaceholder= GetBool(p,   "IsPlaceholder");
            var isMe         = GetBool(p,   "IsMe");          // asserted by your tests
            var isOrganizer  = GetBool(p,   "IsOrganizer");   // if missing -> false
            var username     = GetNullableString(p, "Username");
            var userId       = GetNullableString(p, "UserId");

            var row = new ParticipantRow(
                pid: pid,
                displayName: displayName,
                isPlaceholder: isPlaceholder,
                isOrganizer: isOrganizer,
                username: username,
                userId: userId,
                isSelf: isMe
            );
            Items.Add(row);

            if (row.IsOrganizer)
                OrganizerDisplay = $"Organizer: {(!string.IsNullOrWhiteSpace(row.Username) ? row.Username : row.DisplayName)}";
        }

        IsOrganizerMe = Items.Any(x => x.IsSelf && x.IsOrganizer);

        // Set per-row permissions based on organizer status
        foreach (var r in Items) r.UpdatePermissions(IsOrganizerMe);

        // Ordering: organizer first, then real users, then placeholders, then by name
        var ordered = Items.OrderByDescending(x => x.IsOrganizer)
                           .ThenBy(x => x.IsPlaceholder)
                           .ThenBy(x => (x.Username ?? x.DisplayName))
                           .ToList();

        if (!ordered.SequenceEqual(Items))
        {
            Items.Clear();
            foreach (var r in ordered) Items.Add(r);
        }
    }

    // -------- Organizer-only actions (per your new rules): Rename (placeholders) & Remove (anyone but self)

    [RelayCommand]
    private async Task RenameAsync(ParticipantRow row)
    {
        if (!IsOrganizerMe || row is null || !row.IsPlaceholder) return;
        await _client.UpdateParticipantDisplayNameAsync(TripId, row.ParticipantId, row.DisplayName);
    }

    [RelayCommand]
    private async Task RemoveAsync(ParticipantRow row)
    {
        if (!IsOrganizerMe || row is null || row.IsSelf) return; // organizer cannot remove himself
        await _client.DeleteParticipantAsync(TripId, row.ParticipantId);
        Items.Remove(row);
    }

    // ---- Helpers to read possibly changed DTOs safely (reflection)
    private static string GetString(object obj, string name)
        => obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
              ?.GetValue(obj)?.ToString() ?? string.Empty;

    private static string? GetNullableString(object obj, string name)
        => obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
              ?.GetValue(obj)?.ToString();

    private static bool GetBool(object obj, string name)
        => (obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
              ?.GetValue(obj) as bool?) ?? false;
}

public sealed partial class ParticipantRow : ObservableObject
{
    public ParticipantRow(
        string pid,
        string displayName,
        bool isPlaceholder,
        bool isOrganizer,
        string? username,
        string? userId,
        bool isSelf)
    {
        ParticipantId = pid;
        _displayName = displayName;
        IsPlaceholder = isPlaceholder;
        IsOrganizer = isOrganizer;
        Username = string.IsNullOrWhiteSpace(username) ? null : username;
        UserId = string.IsNullOrWhiteSpace(userId) ? null : userId;
        IsSelf = isSelf;
    }

    public string ParticipantId { get; }
    [ObservableProperty] private string _displayName;

    public bool IsPlaceholder { get; }
    public bool IsOrganizer { get; }
    public bool IsSelf { get; }
    public string? Username { get; }
    public string? UserId { get; }

    // Calculated after VM learns if current user is organizer
    [ObservableProperty] private bool _canRename;
    [ObservableProperty] private bool _canRemove;

    public bool IsRealUser => !IsPlaceholder;

    public string RoleBadge =>
        IsOrganizer ? "Organizer" :
        IsPlaceholder ? "Placeholder" : "User";

    public string DisplayPrimary =>
        IsPlaceholder ? DisplayName :
        !string.IsNullOrWhiteSpace(Username) ? Username! : DisplayName;

    public string? DisplaySecondary =>
        IsPlaceholder ? null :
        (!string.IsNullOrWhiteSpace(DisplayName) && !string.Equals(DisplayName, Username, StringComparison.OrdinalIgnoreCase))
            ? DisplayName : null;

    public void UpdatePermissions(bool isOrganizerMe)
    {
        CanRename = isOrganizerMe && IsPlaceholder; // organizer may rename placeholders
        CanRemove = isOrganizerMe && !IsSelf;       // organizer may remove anyone except himself
    }
}
