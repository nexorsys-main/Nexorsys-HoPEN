# User Administration & Kiosk System - Final Configuration & Fixes Note

*Date: May 22, 2026*

This document summarizes all the critical bug fixes, configuration alignments, and system adjustments made to ensure the User Administration backend and the remote NFC Kiosks operate flawlessly together. 

By keeping this document in the project root, any future deployments or restarts of the system will maintain these fixes, and you will not need to troubleshoot these issues again.

## 1. Remote Kiosk Auto-Update Mechanism
**Issue:** The secondary Kiosk (on a different PC) was not updating its configuration when changes were made in the Admin panel. It was also getting blocked due to an old API key.
**Fix:** 
- The `/fleet/heartbeat` and `/fleet/config` endpoints in `FleetController.cs` were updated to accept a list of **legacy API keys**. 
- **How it works now:** If you change a setting in the Admin panel, the backend generates a new configuration hash. The remote Kiosk (polling every 60 seconds) detects the hash change, downloads the new `appsettings.json`, and **automatically restarts itself** with the latest settings and the correct active API key.

## 2. Database Constraints for CPS Cards
**Issue:** Scanning a physical CPS card crashed the server (HTTP 500) because the CPS identifier string was longer than 100 characters.
**Fix:**
- Modified the PostgreSQL database schema. The `badge_uid` and `nfc_uid` columns in the `kiosk_sessions` table were expanded from `VARCHAR(100)` to `VARCHAR(512)`.
- **Result:** The system can now gracefully handle extremely long identifiers from CPS cards without crashing.

## 3. PIN Validation and Normalization
**Issue:** Users could generate and save a PIN, but the Kiosk would reject it as "Invalid PIN". 
**Fix:**
- **Inconsistency resolved:** The Kiosk creates PINs by stripping out non-alphanumeric characters, but the validation logic was comparing it against strings that still contained symbols like dashes (`-`) or slashes (`/`).
- **Strict Security Check updated:** In `KioskService.cs` (`ValidatePinAsync`), the validation logic was updated to rigorously strip **all** characters except letters and numbers before comparing the scanned card to the PIN record. 
- **Result:** PINs now validate correctly regardless of how the card reader formats the initial string.

## 4. Multi-Reader Compatibility (WinSCard vs CPS-APDU)
**Issue:** The main PC and the second PC use different types of card readers. Scanning the exact same card produced two entirely different identifier strings:
  - **Main PC (CPS Reader):** `CPS-010UFR10UTARN-ET-GARONNE(82)...`
  - **Second PC (WinSCard Reader):** `KEY8025000001_0000003200229681_AUTH`
**Fix:**
- The database schema relies on the `user_devices` table to support **aliases**. 
- We manually registered the `KEY...` format strings for both **Natacha Negro** and **JeanClaude Foissac** as active `user_devices` pointing to their respective user profiles.
- **Future Proofing:** If a new user is added in the future and their card is scanned on the WinSCard reader, the system will read a `KEY...` format. To link it, you simply need to capture that `KEY...` string from the Kiosk logs and insert it into the `user_devices` table as an alias for that user, just as we did today.

---

### System Status: STABLE
All backend containers (`nexorsys_identity_api`, `nexorsys_identity_db`) have been successfully rebuilt with these code changes and are currently running the optimized code. The changes are permanently saved in the source code.
