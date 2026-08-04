# Stock & Flow — Monetization Plan

> Decisions locked **2026-08-02** (supersedes the pricing sections of `BUSINESS_PLAN.md`,
> which was written for the desktop-only era). This is the blueprint for building the
> paywall before the public store launch.
>
> **Core principle: monetize from day one.** Launching free and adding payment later
> punishes the earliest supporters and invites review-bombing. Nobody should ever lose
> a feature they already had.

---

## 1. Guiding principles

1. **Never block data entry or data exit.** Recording a sale is the app's heartbeat —
   it is never gated. Excel *export* is always free and unlimited: it's the user's data.
2. **Gate growth and polish, not survival.** The free tier is something a tiny business
   can live in forever; the paid tier is what they reach for when the business grows.
3. **No accounts, no data collection.** The privacy-first story survives monetization
   intact — it *is* the marketing copy: "No account. No cloud. Your data never leaves
   your device."
4. **Lifetime pricing is structurally safe for us** — the app has no server costs, so a
   lifetime customer costs ~nothing to keep, unlike a typical SaaS.
5. **Underpricing is recoverable; overpricing at launch is not.** Prices can rise for
   new customers at any time; existing customers get grandfathered.

---

## 2. Tiers

### Free (forever, not a trial)
| Feature | Limit |
|---|---|
| Inventory items | **30** (setup-time cap — includes BOM raw materials) |
| Sales recording | **Unlimited** — never blocked |
| Excel **export** | **Unlimited, always free** (data is never hostage) |
| Excel **import** | 2 / month |
| Customer invoices (PDF) | 5 / month (resets monthly) |
| Reports | Basic |
| Shopify sync | — |

All counters that reset do so **monthly**, so the free tier stays useful forever rather
than being a lifetime-capped trial in disguise. Counters live on-device; a determined
user could wipe app data to reset them — accepted risk, not worth an arms race.

### Pro
Everything unlimited: items, invoices, imports, **Shopify sync**, advanced reports.

| Plan | Price | Notes |
|---|---|---|
| Monthly | **$8.99 / month** | The flexible option |
| Annual | **$49.99 / year** | **The hero price** — marketed as the default (~54% off monthly; still far below Craftybase $288/yr, Sortly $348/yr) |
| Lifetime | **$99.99 launch price** | "Founding Member" framing — stated intent to raise to ~$149 after launch (creates urgency, preserves pricing room) |

Store commission: 15% on both stores (small-business programs, under $1M/yr).
Net per unit ≈ $7.64 / $42.49 / $84.99.

### Testers (all 9, Android + iOS)
**Free lifetime Pro** at public launch, delivered via store promo/offer codes
(App Store offer codes + Google Play promo codes — redeems the lifetime IAP into their
store account at no charge; no special builds or hidden switches). They earned it, it
costs ~nothing, and they are the best word-of-mouth channel.

---

## 3. Licensing architecture — no accounts, no backend

**The stores are the licensing backend.** A purchase is recorded against the user's
Apple ID / Google account by the store itself. The app asks the billing API "does this
account own Pro?" and caches the answer locally for offline use. The user never sees a
sign-up screen — buying Pro is a Face ID / fingerprint tap.

- **Billing layer: RevenueCat** (free until meaningful revenue) wrapping StoreKit +
  Google Play Billing behind one SDK, with receipt validation handled for us.
  Evaluate direct store APIs as fallback, but RevenueCat is the default choice.
- **Offline behavior:** entitlement checked when network is available, cached in
  SecureStorage (the layer hardened in `60a9dfb`). Offline days at a craft fair keep
  Pro unlocked from cache.
- **New device, same platform:** "Restore purchases" — the entitlement follows the
  store account. (Inventory *data* moves via Excel export/backup, unchanged.)
- **Subscriptions:** renewals, cancellations, refunds all handled by the stores.

### One licence, three platforms (decided 2026-08-03)

**Buy once on a phone; use Pro on iPhone, Android and PC.** Purchases happen only on the two
platforms that have stores; the desktop app never takes payment, which keeps it outside store
payment rules entirely and means **no desktop payment stack to build** — no Paddle/Gumroad, no
licence-key issuing, no VAT handling.

| | Free tier | After unlocking |
|---|---|---|
| iPhone / Android | 30 items · 5 invoices/mo · 2 imports/mo | Unlimited + Shopify sync |
| **PC (WPF)** | **Same limits** | Same unlock, entered as a sync code |

- **PC is free-with-limits, not Pro-only.** A blank wall on desktop just gets uninstalled;
  the same free tier makes desktop another surface where users meet the limits and convert.
  They lose nothing by using it free, since they can't pay there anyway.
- Desktop reaches RevenueCat over its **REST API** (the mobile SDK isn't available there), so all
  three platforms resolve the same entitlement from the same sync code.
- **Entitlement is cached locally on desktop** and re-validated occasionally with a grace period.
  A network drop must never lock someone out mid-workday.
- **A lapsed subscription re-applies the limits but never gates data.** Viewing, editing and
  Excel export stay available forever — locking someone out of their own local file would be
  worse than any cloud app doing it.
- Desktop already enforces the free-tier gates as of `b15e0f1`, which is exactly this model;
  the only missing piece is the unlock path, which waits on RevenueCat.

### Cross-platform license (Android ⇄ iOS) — "sync code"
The stores never share purchases across ecosystems, so cross-platform needs a shared
entitlement record. Chosen approach — **RevenueCat custom app-user IDs, no server of
our own, no accounts**:

1. On the purchase device: Settings → "Use my license on another device" → app
   generates an unguessable code (e.g. `SF-K7Q2-M9X4-PDW3`) and logs into RevenueCat
   with it, attaching the entitlement.
2. On the other device (either platform): "Already purchased?" → enter code →
   entitlement present → Pro unlocks.

Marketed as: *"No account — just a sync code, like a gift card."* Both stores permit
honoring purchases made on the other platform as long as native IAP is also offered.

Accepted trade-offs: a code can be shared with a friend (Netflix-password-tier risk,
ignorable at our scale); a lost code falls back to store restore + regenerate.

Ship order: per-store entitlements at launch; sync-code screen at launch **or** first
update — it is a small increment on top of RevenueCat, not a rebuild.

### Desktop (WPF) — settled 2026-08-03
~~Needs its own licence keys via Paddle/Gumroad~~ — **no longer required.** Desktop takes no
payment; a phone purchase unlocks it via sync code (see "One licence, three platforms" above).
RevenueCat's REST API covers the lack of a Windows SDK. Mobile launch does not depend on any
of this.

---

## 4. Implementation work items (pre-launch)

1. **Entitlement service in `StockAndFlow.Core`** — single source of truth the ViewModels
   ask: `IsPro`, `CanAddInventoryItem()`, `CanGenerateInvoice()`, `CanImport()`.
   Free-tier monthly counters persisted locally.
2. **Feature gating** at the four gate points: add-item (31st item), invoice generation
   (6th this month), Excel import (3rd this month), Shopify settings entry.
   Each gate shows a friendly upsell, never an error.
3. **RevenueCat integration** in `StockAndFlow.Mobile` — products, purchase flow,
   restore purchases, entitlement cache in SecureStorage.
4. **Paywall screen** — tier comparison, three price options with annual highlighted,
   restore-purchases link, "your data never leaves your device" messaging.
5. **Store console setup** — create the 3 IAP products in App Store Connect and Play
   Console; generate 9 lifetime promo codes for testers.
6. **Sandbox testing** — Play Billing license testers + App Store sandbox accounts;
   verify purchase, restore, cancellation, and offline-cache paths.
7. **Sync-code screen** (may slip to first post-launch update).
8. **Store listing updates** — listings must declare IAP; privacy policy stays honest:
   no data collected (RevenueCat's anonymized receipt handling gets one line).

Rough order: 1 → 2 (fully testable with a fake provider, no store setup needed) →
5 → 3 → 4 → 6 → 7/8.

---

## 5. Explicitly rejected ideas (and why)

- **Lifetime caps on sales/exports** (early draft: 20 sales, 5 exports) — a sales cap
  blocks the app's core function mid-business-day ("I just sold a candle and my app
  says no"); an export cap holds user data hostage. Both are trust-killers and 1-star
  magnets. Replaced with the monthly-reset structure above.
- **Launching free, monetizing later** — anchors everyone on $0 and punishes the most
  loyal users when the switch flips.
- **Real user accounts + own backend** — solves cross-platform but makes us a data
  collector, adds server costs and auth liability, and destroys the privacy pitch that
  differentiates the product.
- **$39.99 annual** (early draft) — a 63% discount off monthly made annual cannibalize
  everything; $49.99 keeps the "screaming deal" optics with 25% more revenue.

---

## 6. Open questions (decide later, none block launch)

- ~~Does mobile lifetime unlock the future desktop version?~~ **Yes — decided 2026-08-03, see
  "One licence, three platforms".**
- Exact timing/size of the post-launch lifetime price raise ($99.99 → ~$149).
- Whether the 30-item cap needs a "counts only finished goods, not BOM materials"
  carve-out if free users feel squeezed (watch tester feedback).
- Free-trial mechanics (e.g., 14-day Pro trial via store-native trial on the annual
  subscription) — stores make this a checkbox; decide during store console setup.
