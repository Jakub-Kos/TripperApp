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
            // Be tolerant to DTO naming — read by reflection
            var pid           = GetString(p, "ParticipantId");
            var displayName   = GetString(p, "DisplayName");
            var isPlaceholder = GetBool(p,   "IsPlaceholder");
            var isMe          = GetBool(p,   "IsMe");          // validated by your tests
            var isOrganizer   = GetBool(p,   "IsOrganizer");
            var username      = GetNullableString(p, "Username");
            var userId        = GetNullableString(p, "UserId");

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

        foreach (var r in Items) r.UpdatePermissions(IsOrganizerMe);

        // Organizer first, then real users, then placeholders, then by name. Keep "Me" near top among non-organizers.
        var ordered = Items
            .OrderByDescending(x => x.IsOrganizer)
            .ThenByDescending(x => x.IsSelf)
            .ThenBy(x => x.IsPlaceholder)
            .ThenBy(x => (x.Username ?? x.DisplayName))
            .ToList();

        if (!ordered.SequenceEqual(Items))
        {
            Items.Clear();
            foreach (var r in ordered) Items.Add(r);
        }
    }

    // ---- Actions ----
    // Organizer can rename placeholders; ANY user can rename himself (your requested change).
    [RelayCommand]
    private async Task RenameAsync(ParticipantRow row)
    {
        if (row is null) return;
        if (!(row.CanRename)) return;
        if (row.IsSelf && !row.IsPlaceholder)
            await _client.UpdateMyParticipantDisplayNameAsync(TripId, row.DisplayName);
        else
            await _client.UpdateParticipantDisplayNameAsync(TripId, row.ParticipantId, row.DisplayName);
    }

    // Organizer can remove anyone EXCEPT himself
    [RelayCommand]
    private async Task RemoveAsync(ParticipantRow row)
    {
        if (row is null) return;
        if (!(row.CanRemove)) return;
        await _client.DeleteParticipantAsync(TripId, row.ParticipantId);
        Items.Remove(row);
    }

    // -------- helpers (reflection friendly) --------
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

    // Derived / permissions
    [ObservableProperty] private bool _canRename;
    [ObservableProperty] private bool _canRemove;

    public bool IsRealUser => !IsPlaceholder;

    public string RoleText =>
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
        // New rule: users can rename themselves; organizer can rename placeholders
        CanRename = (IsSelf) || (isOrganizerMe && IsPlaceholder);
        // Organizer may remove anyone except himself
        CanRemove = isOrganizerMe && !IsSelf;
    }
}
