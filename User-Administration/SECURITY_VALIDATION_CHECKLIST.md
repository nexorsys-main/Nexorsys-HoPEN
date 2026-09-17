# NexorSys Security Validation Checklist

- [ ] Verify EV Code Signing applied to all EXE and DLL files.
- [ ] Verify `migrations-idempotent.sql` is run on the production database.
- [ ] Verify Windows Credential Provider functions correctly with Winlogon.
- [ ] Validate LDAPS internal customer certificate binding.
- [ ] Validate NFC smartcard readers correctly output hexadecimal UID via physical testing.
