#include "NexorsysCredential.h"
#include "CryptoHelper.h"
#include <new>
#include <shlwapi.h>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "credui.lib")
#pragma comment(lib, "winhttp.lib")

#include <winhttp.h>
#include <string>

#ifndef NT_SUCCESS
#define NT_SUCCESS(Status) (((NTSTATUS)(Status)) >= 0)
#endif

EXTERN_C const GUID CLSID_NexorsysCredentialProvider;


// The credential requires packing a KERB_INTERACTIVE_LOGON structure
// For simplicity in this implementation, we will simulate the serialization.

static const wchar_t* REG_KEY_BASE = L"SOFTWARE\\Innovera\\Nexorsys\\Credentials";

static bool WriteRegistryBlob(LPCWSTR uid, const std::vector<BYTE>& blob)
{
    HKEY hKey;
    if (RegCreateKeyExW(HKEY_LOCAL_MACHINE, REG_KEY_BASE, 0, NULL, 0, KEY_WRITE, NULL, &hKey, NULL) == ERROR_SUCCESS)
    {
        LSTATUS status = RegSetValueExW(hKey, uid, 0, REG_BINARY, blob.data(), (DWORD)blob.size());
        RegCloseKey(hKey);
        return status == ERROR_SUCCESS;
    }
    return false;
}

static bool ReadRegistryBlob(LPCWSTR uid, std::vector<BYTE>& blob)
{
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, REG_KEY_BASE, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        DWORD type = 0;
        DWORD size = 0;
        if (RegQueryValueExW(hKey, uid, NULL, &type, NULL, &size) == ERROR_SUCCESS && type == REG_BINARY)
        {
            blob.resize(size);
            if (RegQueryValueExW(hKey, uid, NULL, &type, blob.data(), &size) == ERROR_SUCCESS)
            {
                RegCloseKey(hKey);
                return true;
            }
        }
        RegCloseKey(hKey);
    }
    return false;
}

static bool DeleteRegistryBlob(LPCWSTR uid)
{
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, REG_KEY_BASE, 0, KEY_SET_VALUE, &hKey) == ERROR_SUCCESS)
    {
        LSTATUS status = RegDeleteValueW(hKey, uid);
        RegCloseKey(hKey);
        return status == ERROR_SUCCESS;
    }
    return false;
}

NexorsysCredential::NexorsysCredential() : _cRef(1), _pcpce(NULL), _bHasEnrolledBlob(false)
{
    ZeroMemory(_wszBadgeUid, sizeof(_wszBadgeUid));
    ZeroMemory(_wszUsername, sizeof(_wszUsername));
    ZeroMemory(_wszAdPassword, sizeof(_wszAdPassword));
    ZeroMemory(_wszPin, sizeof(_wszPin));
}

NexorsysCredential::~NexorsysCredential()
{
    if (_pcpce) _pcpce->Release();
}

HRESULT NexorsysCredential::QueryInterface(REFIID riid, void** ppv)
{
    if (riid == IID_IUnknown || riid == IID_ICredentialProviderCredential || riid == IID_ICredentialProviderCredential2)
    {
        *ppv = this;
        AddRef();
        return S_OK;
    }
    *ppv = NULL;
    return E_NOINTERFACE;
}

ULONG NexorsysCredential::AddRef() { return InterlockedIncrement(&_cRef); }

ULONG NexorsysCredential::Release()
{
    LONG cRef = InterlockedDecrement(&_cRef);
    if (!cRef) delete this;
    return cRef;
}

HRESULT NexorsysCredential::Initialize() { return S_OK; }

void NexorsysCredential::SetBadgeUid(LPCWSTR uid)
{
    StringCchCopyW(_wszBadgeUid, ARRAYSIZE(_wszBadgeUid), uid);
    
    std::vector<BYTE> dummy;
    _bHasEnrolledBlob = ReadRegistryBlob(uid, dummy);

    if (_pcpce)
    {
        _pcpce->SetFieldState(this, CPFI_USERNAME, _bHasEnrolledBlob ? CPFS_HIDDEN : CPFS_DISPLAY_IN_SELECTED_TILE);
        _pcpce->SetFieldState(this, CPFI_AD_PASSWORD, _bHasEnrolledBlob ? CPFS_HIDDEN : CPFS_DISPLAY_IN_SELECTED_TILE);
        _pcpce->SetFieldState(this, CPFI_FORGOT_PIN, _bHasEnrolledBlob ? CPFS_DISPLAY_IN_SELECTED_TILE : CPFS_HIDDEN);
        
        if (_bHasEnrolledBlob)
            _pcpce->SetFieldString(this, CPFI_STATUS_TEXT, L"Badge reconnu. Saisissez votre PIN.");
        else
            _pcpce->SetFieldString(this, CPFI_STATUS_TEXT, L"Badge non enr\x00F4l\x00E9. Saisissez votre identifiant, PIN et mot de passe AD.");
    }
}

HRESULT NexorsysCredential::Advise(ICredentialProviderCredentialEvents* pcpce)
{
    if (_pcpce) _pcpce->Release();
    _pcpce = pcpce;
    if (_pcpce) _pcpce->AddRef();
    return S_OK;
}

HRESULT NexorsysCredential::UnAdvise()
{
    if (_pcpce)
    {
        _pcpce->Release();
        _pcpce = NULL;
    }
    return S_OK;
}

HRESULT NexorsysCredential::SetSelected(BOOL* pbAutoLogon)
{
    *pbAutoLogon = FALSE;
    return S_OK;
}

HRESULT NexorsysCredential::SetDeselected() { return S_OK; }

HRESULT NexorsysCredential::SetDeserializedAuthenticationState(DWORD dwState) { return S_OK; }
HRESULT NexorsysCredential::LogOnString(PWSTR* ppwsz) { return E_NOTIMPL; }

HRESULT NexorsysCredential::GetStringValue(DWORD fieldID, PWSTR* ppwsz)
{
    *ppwsz = NULL;
    if (fieldID == CPFI_LARGE_TEXT) 
    {
        return SHStrDupW(L"Clinique Pin\x00E8\x0064\x0065 - NFC", ppwsz);
    }
    if (fieldID == CPFI_STATUS_TEXT) 
    {
        if (wcslen(_wszBadgeUid) == 0)
        {
            return SHStrDupW(L"En attente de badge NFC...", ppwsz);
        }
        else if (!_bHasEnrolledBlob)
        {
            return SHStrDupW(L"Badge non enr\x00F4l\x00E9. Saisissez votre identifiant, PIN et mot de passe AD.", ppwsz);
        }
        else
        {
            return SHStrDupW(L"Badge reconnu. Saisissez votre PIN.", ppwsz);
        }
    }
    if (fieldID == CPFI_FORGOT_PIN)
    {
        return SHStrDupW(L"Code PIN ou Identifiants oubli\x00E9s ?", ppwsz);
    }
    return E_NOTIMPL;
}

HRESULT NexorsysCredential::GetBitmapValue(DWORD fieldID, HBITMAP* phbmp)
{
    if (fieldID == CPFI_LOGO)
    {
        *phbmp = nullptr;
        return E_NOTIMPL;
    }
    return E_NOTIMPL;
}
HRESULT NexorsysCredential::GetCheckboxValue(DWORD fieldID, BOOL* pbChecked, PWSTR* ppwszLabel) { return E_NOTIMPL; }

HRESULT NexorsysCredential::GetFieldState(DWORD dwFieldID, CREDENTIAL_PROVIDER_FIELD_STATE* pcpfs, CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE* pcpfis)
{
    *pcpfis = CPFIS_NONE;

    if (dwFieldID == CPFI_USERNAME || dwFieldID == CPFI_AD_PASSWORD)
    {
        *pcpfs = _bHasEnrolledBlob ? CPFS_HIDDEN : CPFS_DISPLAY_IN_SELECTED_TILE;
    }
    else if (dwFieldID == CPFI_FORGOT_PIN)
    {
        *pcpfs = _bHasEnrolledBlob ? CPFS_DISPLAY_IN_SELECTED_TILE : CPFS_HIDDEN;
    }
    else
    {
        *pcpfs = CPFS_DISPLAY_IN_SELECTED_TILE;
    }
    
    return S_OK;
}

HRESULT NexorsysCredential::GetSubmitButtonValue(DWORD fieldID, DWORD* pdwAdjacentTo) 
{ 
    if (fieldID == CPFI_SUBMIT) 
    { 
        *pdwAdjacentTo = _bHasEnrolledBlob ? CPFI_PIN : CPFI_AD_PASSWORD; 
        return S_OK; 
    }
    return E_NOTIMPL; 
}

HRESULT NexorsysCredential::GetComboBoxValueCount(DWORD fieldID, DWORD* pcItems, DWORD* pdwSelectedItem) { return E_NOTIMPL; }
HRESULT NexorsysCredential::GetComboBoxValueAt(DWORD fieldID, DWORD dwItem, PWSTR* ppwszItem) { return E_NOTIMPL; }

HRESULT NexorsysCredential::SetStringValue(DWORD fieldID, LPCWSTR psz)
{
    if (fieldID == CPFI_PIN) 
    {
        StringCchCopyW(_wszPin, ARRAYSIZE(_wszPin), psz);
    }
    else if (fieldID == CPFI_USERNAME) 
    {
        std::wstring raw(psz);
        if (raw.find(L'\\') == std::wstring::npos && raw.find(L'@') == std::wstring::npos)
        {
            std::wstring formatted = L"NEXORSYS\\" + raw;
            StringCchCopyW(_wszUsername, ARRAYSIZE(_wszUsername), formatted.c_str());
        }
        else if (raw.find(L'@') != std::wstring::npos)
        {
            size_t atPos = raw.find(L'@');
            std::wstring formatted = L"NEXORSYS\\" + raw.substr(0, atPos);
            StringCchCopyW(_wszUsername, ARRAYSIZE(_wszUsername), formatted.c_str());
        }
        else
        {
            StringCchCopyW(_wszUsername, ARRAYSIZE(_wszUsername), psz);
        }
    }
    else if (fieldID == CPFI_AD_PASSWORD) 
    {
        StringCchCopyW(_wszAdPassword, ARRAYSIZE(_wszAdPassword), psz);
    }
    return S_OK;
}

HRESULT NexorsysCredential::SetCheckboxValue(DWORD fieldID, BOOL bChecked) { return E_NOTIMPL; }
HRESULT NexorsysCredential::SetComboBoxSelectedValue(DWORD fieldID, DWORD dwSelectedItem) { return E_NOTIMPL; }
#include <winhttp.h>
#pragma comment(lib, "winhttp.lib")

static bool RequestPinResetApi(LPCWSTR badgeUid)
{
	(void)badgeUid;
	return false; // Reset delivery requires an authenticated, enrolled Agent channel.
}

HRESULT NexorsysCredential::CommandLinkClicked(DWORD fieldID) 
{ 
    if (fieldID == CPFI_FORGOT_PIN)
    {
        if (RequestPinResetApi(_wszBadgeUid))
        {
            DeleteRegistryBlob(_wszBadgeUid);
            _bHasEnrolledBlob = false;
            
            if (_pcpce)
            {
                _pcpce->SetFieldState(this, CPFI_FORGOT_PIN, CPFS_HIDDEN);
                _pcpce->SetFieldState(this, CPFI_USERNAME, CPFS_DISPLAY_IN_SELECTED_TILE);
                _pcpce->SetFieldState(this, CPFI_AD_PASSWORD, CPFS_DISPLAY_IN_SELECTED_TILE);
                _pcpce->SetFieldString(this, CPFI_STATUS_TEXT, L"Notification envoy\x00E9e. R\x00E9-enr\x00F4lez avec votre nouveau PIN et mot de passe AD.");
            }
        }
        else
        {
            if (_pcpce)
            {
                _pcpce->SetFieldString(this, CPFI_STATUS_TEXT, L"Erreur lors de l'envoi de la notification.");
            }
        }
    }
    return S_OK; 
}

#include <ntsecapi.h>
#pragma comment(lib, "Secur32.lib")

static ULONG GetNegotiateAuthPackage()
{
    HANDLE hLsa;
    LSA_STRING name;
    name.Buffer = (PCHAR)"Negotiate";
    name.Length = (USHORT)strlen(name.Buffer);
    name.MaximumLength = name.Length + 1;
    ULONG authPackage = 0;
    if (NT_SUCCESS(LsaConnectUntrusted(&hLsa)))
    {
        LsaLookupAuthenticationPackage(hLsa, &name, &authPackage);
        LsaDeregisterLogonProcess(hLsa);
    }
    return authPackage;
}

HRESULT NexorsysCredential::_CreateSerialization(LPCWSTR username, LPCWSTR password, CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs)
{
    DWORD cbSize = 0;
    CredPackAuthenticationBufferW(0, const_cast<LPWSTR>(username), const_cast<LPWSTR>(password), NULL, &cbSize);
    
    if (cbSize == 0) return E_FAIL;
    
    BYTE* pbAuthBuffer = (BYTE*)CoTaskMemAlloc(cbSize);
    if (!pbAuthBuffer) return E_OUTOFMEMORY;
    
    if (!CredPackAuthenticationBufferW(0, const_cast<LPWSTR>(username), const_cast<LPWSTR>(password), pbAuthBuffer, &cbSize))
    {
        CoTaskMemFree(pbAuthBuffer);
        return E_FAIL;
    }
    
    pcpcs->clsidCredentialProvider = CLSID_NexorsysCredentialProvider; 
    pcpcs->ulAuthenticationPackage = GetNegotiateAuthPackage();
    pcpcs->cbSerialization = cbSize;
    pcpcs->rgbSerialization = pbAuthBuffer;
    
    return S_OK;
}

static bool ValidatePinWithApi(LPCWSTR badgeUid, LPCWSTR pin)
{
    (void)badgeUid;
    (void)pin;
    return false; // Fail closed until the Credential Provider has the real mTLS Agent contract.
}

HRESULT NexorsysCredential::GetSerialization(CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE* pcpgsr, CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs, PWSTR* ppwszOptionalStatusText, CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon)
{
    if (wcslen(_wszBadgeUid) == 0 || wcslen(_wszPin) == 0)
    {
        *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
        return S_OK;
    }

    // Call API to validate the PIN and Badge first (Enterprise Rule)
    bool isApiValid = ValidatePinWithApi(_wszBadgeUid, _wszPin);

    std::vector<BYTE> derivedKey;
    if (SUCCEEDED(CryptoHelper::DeriveKey(_wszPin, _wszBadgeUid, derivedKey)))
    {
        if (_bHasEnrolledBlob)
        {
            std::vector<BYTE> blob;
            if (ReadRegistryBlob(_wszBadgeUid, blob))
            {
                std::wstring plain;
                if (SUCCEEDED(CryptoHelper::DecryptPassword(derivedKey, blob, plain)))
                {
                    size_t delim = plain.find(L'\t');
                    if (delim != std::wstring::npos)
                    {
                        std::wstring username = plain.substr(0, delim);
                        std::wstring password = plain.substr(delim + 1);
                        
                        if (!isApiValid)
                        {
                            *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
                            SHStrDupW(L"Validation serveur \x00E9chou\x00E9e. V\x00E9rifiez votre connexion.", ppwszOptionalStatusText);
                            *pcpsiOptionalStatusIcon = CPSI_ERROR;
                            return S_OK;
                        }

                        *pcpgsr = CPGSR_RETURN_CREDENTIAL_FINISHED;
                        return _CreateSerialization(username.c_str(), password.c_str(), pcpcs);
                    }
                }
            }
        }
        else
        {
            if (wcslen(_wszUsername) > 0 && wcslen(_wszAdPassword) > 0)
            {
                if (!isApiValid)
                {
                    *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
                    SHStrDupW(L"Enr\x00F4lement refus\x00E9 par le serveur.", ppwszOptionalStatusText);
                    *pcpsiOptionalStatusIcon = CPSI_ERROR;
                    return S_OK;
                }

                std::wstring plain = std::wstring(_wszUsername) + L"\t" + _wszAdPassword;
                std::vector<BYTE> cipher;
                if (SUCCEEDED(CryptoHelper::EncryptPassword(derivedKey, plain.c_str(), cipher)))
                {
                    if (WriteRegistryBlob(_wszBadgeUid, cipher))
                    {
                        _bHasEnrolledBlob = true;
                        *pcpgsr = CPGSR_RETURN_CREDENTIAL_FINISHED;
                        return _CreateSerialization(_wszUsername, _wszAdPassword, pcpcs);
                    }
                }
            }
        }
    }

    *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
    SHStrDupW(L"Erreur d'authentification cryptographique.", ppwszOptionalStatusText);
    *pcpsiOptionalStatusIcon = CPSI_ERROR;
    
    return S_OK;
}

HRESULT NexorsysCredential::ReportResult(NTSTATUS ntsStatus, NTSTATUS ntsSubstatus, PWSTR* ppwszOptionalStatusText, CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon)
{
    if (!NT_SUCCESS(ntsStatus))
    {
        // Logon failed (e.g., AD password changed, or initial enrollment failed)
        // We delete the blob so the user can re-enroll or fix their credentials.
        DeleteRegistryBlob(_wszBadgeUid);
        _bHasEnrolledBlob = false;
        ZeroMemory(_wszPin, sizeof(_wszPin));
        ZeroMemory(_wszAdPassword, sizeof(_wszAdPassword));

        if (_pcpce)
        {
            _pcpce->SetFieldState(this, CPFI_USERNAME, CPFS_DISPLAY_IN_SELECTED_TILE);
            _pcpce->SetFieldState(this, CPFI_AD_PASSWORD, CPFS_DISPLAY_IN_SELECTED_TILE);
            _pcpce->SetFieldState(this, CPFI_FORGOT_PIN, CPFS_HIDDEN);
            _pcpce->SetFieldString(this, CPFI_STATUS_TEXT, L"Identifiants incorrects. Veuillez les saisir \x00E0 nouveau.");
        }
    }
    else
    {
        ZeroMemory(_wszPin, sizeof(_wszPin));
        ZeroMemory(_wszAdPassword, sizeof(_wszAdPassword));
    }
    return S_OK;
}

HRESULT NexorsysCredential::GetUserSid(PWSTR* ppwszUserSid)
{
    *ppwszUserSid = NULL;
    return E_NOTIMPL;
}
