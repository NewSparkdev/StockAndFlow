using System.Windows.Input;

namespace StockAndFlow.Mobile.Controls;

/// <summary>A top-bar action (button) a section wants shown while it is active.</summary>
public record NavAction(string Text, ICommand Command);

/// <summary>
/// Implemented by each section view hosted in <c>HostPage</c> so the host can show the right
/// title and toolbar buttons when that section becomes active. Sections are plain ContentViews
/// (not pages) so a single persistent bottom bar can host them without re-creating on switch.
/// </summary>
public interface ISectionView
{
	string SectionId { get; }
	string SectionTitle { get; }
	IReadOnlyList<NavAction> Actions { get; }
}
