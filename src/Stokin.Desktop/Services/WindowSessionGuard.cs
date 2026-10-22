using System.Windows;
namespace Stokin.Desktop.Services;

public interface IRevocableWindow { void RevokeAccess(); }
public static class WindowSessionGuard
{
    public sealed record Obscured(UIElement Content, Visibility Previous);
    public static IReadOnlyList<Obscured> Obscure(IEnumerable<Window> windows)
    {
        var result = new List<Obscured>(); foreach (var window in windows.ToArray()) if (window.Content is UIElement content) { result.Add(new(content, content.Visibility)); content.Visibility = Visibility.Hidden; }
        return result;
    }
    public static void Restore(IEnumerable<Obscured> views) { foreach (var view in views) view.Content.Visibility = view.Previous; }
    public static void Revoke(IEnumerable<Window> windows)
    {
        foreach (var window in windows.ToArray()) { if (window is IRevocableWindow revocable) revocable.RevokeAccess(); else { window.DataContext = null; window.Close(); } }
    }
}
