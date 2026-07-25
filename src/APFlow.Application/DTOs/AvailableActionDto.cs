namespace APFlow.Application.DTOs;

/// <summary>
/// Read shape for one workflow transition the acting user may actually execute
/// right now for a given invoice (WP-054). Deliberately just the (code, label)
/// pair the caller needs to render an action - not "the full graph with
/// permission flags" (WP-054 task 1's own wording), since a transition this
/// user cannot currently perform is omitted entirely rather than included with
/// a flag saying so.
/// </summary>
public sealed record AvailableActionDto(string TargetStatusCode, string TargetStatusLabel);
