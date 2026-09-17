#pragma once

#include <windows.h>
#include <credentialprovider.h>
#include <wincred.h>
#include <strsafe.h>

#define MAX_PIN_LENGTH 32
#define MAX_BADGE_UID_LENGTH 64

// Field definitions for the Logon UI Tile
enum CP_FIELD_ID
{
    CPFI_LOGO = 0,         // Reserved tile-image field; no legacy bitmap is embedded
    CPFI_LARGE_TEXT = 1,   // Display Name
    CPFI_USERNAME = 2,     // AD Username Input (Only shown on enrollment)
    CPFI_PIN = 3,          // PIN Input
    CPFI_AD_PASSWORD = 4,  // AD Password Input (Only shown on enrollment)
    CPFI_SUBMIT = 5,       // Submit Button
    CPFI_FORGOT_PIN = 6,   // Forgot PIN Command Link
    CPFI_STATUS_TEXT = 7,  // Status Message
    CPFI_NUM_FIELDS = 8
};

// Global helper for DLL reference counting
extern LONG g_cRef;
extern HINSTANCE g_hinst;

void DllAddRef();
void DllRelease();
