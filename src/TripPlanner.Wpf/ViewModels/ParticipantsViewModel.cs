using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TripPlanner.Client.Abstractions;

namespace TripPlanner.Wpf.ViewModels;

/// <summary>
/// Participants tab: lists trip participants, allows organizer to invite/remove and rename entries.
/// Users can rename themselves; organizer can manage placeholders.
/// </summary>
public sealed partial class ParticipantsViewModel : ObservableObject
{
    private readonly ITripPlannerClient _client;
    public ParticipantsViewModel(ITripPlannerClient client) => _client = client;

    [ObservableProperty] private string _tripId = "";
    public ObservableCollection<ParticipantRow> Items { get; } = new();

    [ObservableProperty] private bool _isOrganizerMe;
    [ObservableProperty] private string _organizerDisplay = "Organizer: —";

    // Organizer-only: invite code generation (back in UI)
    [ObservableProperty] private string _inviteCode = "";
    [ObservableProperty] private string _newPlaceholderName = "";

    public async Task LoadAsync(string tripId)
    {
        TripId = tripId;
        Items.Clear();
        InviteCode = "";
        IsOrganizerMe = false;
        OrganizerDisplay = "Organizer: —";

        var list = await _client.ListParticipantsAsync(tripId);
        if (list is null) return;

        foreach (var p in list)
        {
            var pid           = GetString(p, "ParticipantId");
            var displayName   = GetString(p, "DisplayName");
            var isPlaceholder = GetBool(p,   "IsPlaceholder");
            var isMe          = GetBool(p,   "IsMe");
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

        // sort: organizer, me, real users, placeholders, then by name
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

    // ======= Name editing flow (text until user clicks "Edit name") =======

    [RelayCommand]
    private void BeginEditName(ParticipantRow row)
    {
        if (row is null || !row.CanRename) return;
        row.EditDisplayName = row.DisplayName; // seed with current
        row.IsEditingName = true;
    }

    [RelayCommand]
    private async Task SaveEditNameAsync(ParticipantRow row)
    {
        if (row is null || !row.CanRename) return;
        var newName = row.EditDisplayName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(newName) || newName == row.DisplayName)
        {
            row.IsEditingName = false;
            return;
        }

        try
        {
            if (row.IsSelf && !row.IsPlaceholder)
                await _client.UpdateMyParticipantDisplayNameAsync(TripId, newName);
            else
                await _client.UpdateParticipantDisplayNameAsync(TripId, row.ParticipantId, newName);

            row.DisplayName = newName;     // triggers NameForView re-eval
        }
        catch (Exception)
        {
            // swallow to avoid app crash; optionally could expose a Status property
        }
        finally
        {
            row.IsEditingName = false;
        }
    }

    [RelayCommand]
    private void CancelEditName(ParticipantRow row)
    {
        if (row is null) return;
        row.IsEditingName = false;
        row.EditDisplayName = row.DisplayName;
    }

    // Organizer can remove anyone except himself
    [RelayCommand]
    private async Task RemoveAsync(ParticipantRow row)
    {
        if (row is null || !row.CanRemove) return;
        await _client.DeleteParticipantAsync(TripId, row.ParticipantId);
        Items.Remove(row);
    }

    // Organizer: Create invite code
    [RelayCommand]
    private async Task CreateInviteCodeAsync()
    {
        if (!IsOrganizerMe) return;
        var inv = await _client.CreateInviteAsync(TripId);
        if (inv.HasValue) InviteCode = inv.Value.code;
    }
    
    [RelayCommand]
    private async Task AddPlaceholderAsync()
    {
        if (!IsOrganizerMe) return;                       // organizer-only
        if (string.IsNullOrWhiteSpace(NewPlaceholderName)) return;

        await _client.CreatePlaceholderAsync(TripId, NewPlaceholderName.Trim());
        NewPlaceholderName = "";
        await LoadAsync(TripId);                          // refresh list
    }

    // -------- helpers (reflection tolerant) --------
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
        _editDisplayName = displayName;
    }

    public string ParticipantId { get; }
    [ObservableProperty] private string _displayName;

    public bool IsPlaceholder { get; }
    public bool IsOrganizer { get; }
    public bool IsSelf { get; }
    public string? Username { get; }
    public string? UserId { get; }

    // Permissions (computed)
    [ObservableProperty] private bool _canRename;
    [ObservableProperty] private bool _canRemove;

    // Inline-edit state
    [ObservableProperty] private bool _isEditingName;
    [ObservableProperty] private string _editDisplayName;

    public bool IsRealUser => !IsPlaceholder;

    public string RoleText =>
        IsOrganizer ? "Organizer" :
        IsPlaceholder ? "Placeholder" : "User";

    // Fallback: if user has no display name, show username in the Name column
    public string NameForView => string.IsNullOrWhiteSpace(DisplayName) ? (Username ?? "") : DisplayName;

    // When DisplayName changes, also refresh NameForView binding
    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(NameForView));

    public void UpdatePermissions(bool isOrganizerMe)
    {
        // Users may rename themselves; organizer may rename placeholders
        CanRename = IsSelf || (isOrganizerMe && IsPlaceholder);
        // Organizer may remove anyone except himself
        CanRemove = isOrganizerMe && !IsSelf;
    }
}
