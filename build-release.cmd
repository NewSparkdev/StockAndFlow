@echo off
setlocal
REM ===========================================================================
REM  Build a SIGNED Android App Bundle (.aab) for Google Play.
REM
REM  One-time: create a keystore (keep it safe + backed up; losing it means you
REM  can never update the app on Play):
REM    keytool -genkeypair -v -keystore stockandflow.keystore -alias stockandflow ^
REM            -keyalg RSA -keysize 2048 -validity 10000
REM
REM  Then set these environment variables (NEVER commit them):
REM    SF_KEYSTORE  = full path to stockandflow.keystore
REM    SF_KEYALIAS  = stockandflow
REM    SF_STOREPASS = <keystore password>
REM    SF_KEYPASS   = <key password>
REM ===========================================================================
if "%SF_KEYSTORE%"=="" echo ERROR: set SF_KEYSTORE / SF_KEYALIAS / SF_STOREPASS / SF_KEYPASS first.& exit /b 1

dotnet publish StockAndFlow.Mobile\StockAndFlow.Mobile.csproj -c Release -f net10.0-android ^
  -p:AndroidPackageFormat=aab ^
  -p:AndroidKeyStore=true ^
  -p:AndroidSigningKeyStore="%SF_KEYSTORE%" ^
  -p:AndroidSigningKeyAlias=%SF_KEYALIAS% ^
  -p:AndroidSigningStorePass=%SF_STOREPASS% ^
  -p:AndroidSigningKeyPass=%SF_KEYPASS%

echo.
echo Done. Upload the .aab from:
echo   StockAndFlow.Mobile\bin\Release\net10.0-android\publish\
endlocal
