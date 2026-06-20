#!/usr/bin/env bash
set -euo pipefail
# ===========================================================================
#  Build SIGNED release artifacts. Run on macOS for iOS.
#
#  Android App Bundle (.aab) for Google Play
#  --------------------------------------------------------------------------
#  One-time keystore (keep safe + backed up):
#    keytool -genkeypair -v -keystore stockandflow.keystore -alias stockandflow \
#            -keyalg RSA -keysize 2048 -validity 10000
#  Required env: SF_KEYSTORE SF_KEYALIAS SF_STOREPASS SF_KEYPASS
# ===========================================================================
: "${SF_KEYSTORE:?set SF_KEYSTORE}"
: "${SF_KEYALIAS:?set SF_KEYALIAS}"
: "${SF_STOREPASS:?set SF_STOREPASS}"
: "${SF_KEYPASS:?set SF_KEYPASS}"

dotnet publish StockAndFlow.Mobile/StockAndFlow.Mobile.csproj -c Release -f net10.0-android \
  -p:AndroidPackageFormat=aab \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$SF_KEYSTORE" \
  -p:AndroidSigningKeyAlias="$SF_KEYALIAS" \
  -p:AndroidSigningStorePass="$SF_STOREPASS" \
  -p:AndroidSigningKeyPass="$SF_KEYPASS"

echo "Android AAB: StockAndFlow.Mobile/bin/Release/net10.0-android/publish/"

# ===========================================================================
#  iOS (.ipa) for the App Store -- macOS + Xcode only.
#  1) Add net10.0-ios back to <TargetFrameworks> in StockAndFlow.Mobile.csproj
#  2) Install certs + provisioning profile from your Apple Developer account.
#  3) Build:
#     dotnet publish StockAndFlow.Mobile/StockAndFlow.Mobile.csproj -c Release \
#       -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 -p:ArchiveOnBuild=true \
#       -p:CodesignKey="Apple Distribution: YOUR NAME (TEAMID)" \
#       -p:CodesignProvision="StockAndFlow App Store"
# ===========================================================================
