# Security Improvements - Credential Encryption

## Overview
Implemented Windows DPAPI (Data Protection API) encryption for Shopify credentials to address critical security vulnerability #1 from the code audit.

## What Changed

### 1. New SecureCredentialService (`Services/SecureCredentialService.cs`)
- Provides `Encrypt()` and `Decrypt()` methods using Windows DPAPI
- Encrypts data per-user (tied to Windows user account)
- Includes `IsEncrypted()` helper for backward compatibility
- No key management required - Windows handles it automatically

### 2. Updated AppSettings Model (`Models/AppSettings.cs`)
- Credentials now stored encrypted in JSON file
- Added `*Encrypted` properties for storage
- Plain-text accessors automatically encrypt/decrypt on access
- `MigrateToEncrypted()` method for backward compatibility

### 3. Automatic Migration (`App.xaml.cs`)
- Detects plain-text credentials on app startup
- Automatically encrypts and saves them
- Transparent to existing users

## How It Works

### Before (INSECURE):
```json
{
  "ShopifyStoreName": "my-store",
  "ShopifyApiKey": "sk_live_1234567890",
  "ShopifyAccessToken": "shpat_abcdef123456"
}
```

### After (SECURE):
```json
{
  "ShopifyStoreName": "AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA...",
  "ShopifyApiKey": "AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA...",
  "ShopifyAccessToken": "AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA..."
}
```

## Usage in Code

No code changes required for existing code! The properties work transparently:

```csharp
var settings = await dataService.GetSettingsAsync();

// These automatically decrypt when accessed
string? storeName = settings.ShopifyStoreName;
string? apiKey = settings.ShopifyApiKey;
string? token = settings.ShopifyAccessToken;

// These automatically encrypt when set
settings.ShopifyStoreName = "new-store";
settings.ShopifyApiKey = "new-key";
await dataService.SaveSettingsAsync(settings);
```

## Security Benefits

1. **Encrypted at Rest**: Credentials stored as encrypted blobs in settings.json
2. **User-Specific**: Can only be decrypted by the Windows user who encrypted them
3. **No Key Management**: Windows DPAPI handles key derivation and storage
4. **Backward Compatible**: Automatically migrates existing plain-text credentials
5. **Transparent**: No code changes needed in business logic

## Limitations & Considerations

### Windows User Account Dependency
- Credentials encrypted by one Windows user **cannot** be decrypted by another
- If you change your Windows password, credentials remain accessible
- If you migrate to a new user account, you'll need to re-enter credentials

### Not Protection Against
- **Local admin access**: Anyone with admin rights can still access (but this is true for any local storage)
- **Memory dumps**: Credentials are decrypted in memory when used
- **Malware on the machine**: If the machine is compromised, credentials can be accessed when app runs
- **Physical theft**: If someone steals the computer and has your Windows password

### Best For
- Preventing casual inspection of settings files
- Protecting against accidental credential exposure (commits to git, file sharing)
- Meeting basic compliance requirements for credential storage
- Desktop applications with single-user scenarios

## Testing the Implementation

1. **Build the project**:
   ```bash
   dotnet build
   ```

2. **Test with existing credentials**:
   - If you have plain-text credentials in `Data/settings.json`
   - Run the app
   - Check `Data/settings.json` - credentials should now be encrypted

3. **Test new credential entry**:
   - Enter new Shopify credentials in the app
   - Check `Data/settings.json` - should be stored encrypted
   - Restart app - credentials should still work (auto-decrypt)

4. **Verify functionality**:
   - Test Shopify sync with encrypted credentials
   - Verify all Shopify features work normally

## Migration for Existing Users

The migration is **automatic** and happens on next app startup:

1. App loads `Data/settings.json`
2. Detects credentials are plain-text
3. Encrypts them using DPAPI
4. Saves back to `settings.json`
5. App continues normally

No user action required!

## Future Enhancements

Consider implementing:
1. **Windows Credential Manager**: For even better OS-level integration
2. **Azure Key Vault**: For cloud-based credential storage (multi-machine scenarios)
3. **Hardware Security Module (HSM)**: For enterprise deployments
4. **Credential rotation**: Automated periodic credential updates
5. **Audit logging**: Track when credentials are accessed

## Files Modified

1. ✅ `StockAndFlow/Services/SecureCredentialService.cs` (NEW)
2. ✅ `StockAndFlow/Models/AppSettings.cs` (MODIFIED)
3. ✅ `StockAndFlow/App.xaml.cs` (MODIFIED)

## Build Status

✅ Build successful with 0 errors
⚠️ Existing warnings (unrelated to security changes)

## Compliance

This implementation addresses:
- ✅ OWASP Top 10 - A02:2021 Cryptographic Failures
- ✅ CWE-256: Plaintext Storage of a Password
- ✅ CWE-522: Insufficiently Protected Credentials
- ✅ PCI DSS Requirement 8.2.1 (credential encryption)

---

**Status**: ✅ COMPLETE - Critical Security Issue #1 Resolved
