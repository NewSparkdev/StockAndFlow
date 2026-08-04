using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Wpf.Platform
{
    /// <summary>
    /// Desktop upgrade prompt. Purchases happen only in the phone apps (see
    /// MONETIZATION_PLAN "One licence, three platforms"), so this explains the plans and where to
    /// buy rather than offering a buy button that couldn't work.
    ///
    /// Interim by design: it reuses the shared <see cref="PaywallViewModel"/> for its content and
    /// renders as a dialog. A proper window with sync-code entry lands with the sync-code work.
    /// </summary>
    public sealed class WpfPaywallPresenter : IPaywallPresenter
    {
        private readonly IServiceProvider _services;

        public WpfPaywallPresenter(IServiceProvider services) => _services = services;

        public Task ShowPaywallAsync(ProFeature? triggeredBy = null)
        {
            var entitlements = (EntitlementService)_services.GetService(typeof(EntitlementService))!;
            var dialogs = (IDialogService)_services.GetService(typeof(IDialogService))!;
            var vm = new PaywallViewModel(entitlements, dialogs, new UnavailablePurchaseService(), triggeredBy);

            var text = new StringBuilder();
            text.AppendLine(vm.SubHeadline);
            text.AppendLine();

            foreach (var plan in vm.Plans)
            {
                var badge = string.IsNullOrWhiteSpace(plan.Badge) ? "" : $"   ({plan.Badge})";
                text.AppendLine($"{plan.Name}: {plan.Price}{plan.PricePeriod}{badge}");
            }

            text.AppendLine();
            text.AppendLine("What you get:");
            foreach (var row in vm.Comparison.Where(r => r.Free != r.Pro))
                text.AppendLine($"  • {row.Feature}: {row.Free} free → {row.Pro} on Pro");

            text.AppendLine();
            text.AppendLine("Pro is bought in the Stock & Flow app on your phone. One purchase covers " +
                            "your phone and this computer — you'll get a code to unlock it here.");
            text.AppendLine();
            text.AppendLine(vm.PrivacyNote);

            MessageBox.Show(text.ToString(), vm.Headline, MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }
    }
}
