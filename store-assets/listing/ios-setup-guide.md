# iOS App Store Setup Guide
## Do these steps after creating your Apple Developer account

---

### STEP 1 — Create the App ID
1. Go to https://developer.apple.com/account
2. Click **Certificates, IDs & Profiles** → **Identifiers** → **+**
3. Select **App IDs** → **App** → Continue
4. Description: `Stock & Flow`
5. Bundle ID (Explicit): `dev.newspark.stockandflow`
6. Capabilities: check **Push Notifications** (optional for now)
7. Click **Continue** → **Register**

---

### STEP 2 — Create a Distribution Certificate
1. In **Certificates, IDs & Profiles** → **Certificates** → **+**
2. Select **Apple Distribution** → Continue
3. On your Mac (or any Mac), open Keychain Access → Certificate Assistant → Request a Certificate from a Certificate Authority
   - Email: your Apple ID email
   - Common Name: `NewSpark Distribution`
   - Save to disk
4. Upload the `.certSigningRequest` file → Continue → Download the certificate
5. Double-click to install it in your Keychain
6. In Keychain Access, find **Apple Distribution: NewSpark** → right-click → Export → save as `distribution.p12`
   - Set a password (remember it — you'll need it for GitHub)

---

### STEP 3 — Create an App Store Provisioning Profile
1. In **Certificates, IDs & Profiles** → **Profiles** → **+**
2. Select **App Store Connect** → Continue
3. App ID: select `dev.newspark.stockandflow`
4. Certificate: select your Distribution certificate
5. Profile Name: `StockAndFlow AppStore`
6. Download the `.mobileprovision` file

---

### STEP 4 — Create the App in App Store Connect
1. Go to https://appstoreconnect.apple.com
2. Click **Apps** → **+** → **New App**
3. Platforms: iOS
4. Name: `Stock & Flow`
5. Primary Language: English (U.S.)
6. Bundle ID: `dev.newspark.stockandflow`
7. SKU: `stockandflow-ios` (internal only, never shown to users)
8. User Access: Full Access
9. Click **Create**

---

### STEP 5 — Add GitHub Secrets
Go to https://github.com/NewSparkdev/StockAndFlow/settings/secrets/actions → **New repository secret**

Add these 5 secrets:

| Secret Name | Value |
|---|---|
| `APPLE_CERTIFICATE_BASE64` | Run: `base64 -i distribution.p12 \| pbcopy` on Mac, paste result |
| `APPLE_CERTIFICATE_PASSWORD` | The password you set when exporting the .p12 |
| `APPLE_PROVISIONING_PROFILE_BASE64` | Run: `base64 -i StockAndFlow\ AppStore.mobileprovision \| pbcopy`, paste result |
| `APPLE_PROVISIONING_PROFILE_NAME` | `StockAndFlow AppStore` |
| `APPLE_SIGNING_IDENTITY` | `Apple Distribution: NewSpark` |
| `KEYCHAIN_PASSWORD` | Any password, e.g. `TempKeychain2024!` |

---

### STEP 6 — Run the iOS Build
1. Go to https://github.com/NewSparkdev/StockAndFlow/actions
2. Click **iOS Release Build** → **Run workflow** → **Run workflow**
3. Wait ~10-15 minutes
4. Download the `stockandflow-ios-*.zip` artifact → unzip to get the `.ipa` file

---

### STEP 7 — Run the Screenshot Workflow
1. In GitHub Actions → click **iOS Simulator Screenshots** → **Run workflow**
2. Wait ~15 minutes
3. Download `ios-screenshots.zip` → these are your App Store screenshots

---

### STEP 8 — Upload IPA to App Store Connect
Option A (easiest): Download **Transporter** from the Mac App Store → drag your `.ipa` file in → click Deliver.

Option B: Use `xcrun altool` from any Mac terminal:
```bash
xcrun altool --upload-app -f YourApp.ipa -t ios \
  --apiKey YOUR_KEY --apiIssuer YOUR_ISSUER
```

---

### STEP 9 — Fill in App Store Connect Listing
Use the content in `store-assets/listing/app-store-listing.txt`

Upload the screenshots from the `ios-screenshots` artifact.

Screenshot sizes needed:
- **6.9" iPhone** (required): the workflow produces these automatically from iPhone 16 Pro Max simulator

---

### STEP 10 — Submit for Review
In App Store Connect:
1. Select version 1.0.0 → fill in all fields
2. Answer the content questionnaire (all NO)
3. Click **Add for Review** → **Submit to App Review**
4. Review typically takes 1–3 business days
