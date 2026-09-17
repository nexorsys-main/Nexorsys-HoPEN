#include "NexorsysCredentialProvider.h"
#include "NexorsysCredential.h"
#include "SmartCardReader.h"
#include <new>
#include <shlwapi.h>

#pragma comment(lib, "shlwapi.lib")

// Global reader instance for this provider
static SmartCardReader* g_pReader = nullptr;

void OnCardInserted(LPCWSTR badgeUid, void* pContext)
{
    NexorsysCredentialProvider* pProvider = static_cast<NexorsysCredentialProvider*>(pContext);
    if (pProvider)
    {
        pProvider->UpdateBadgeUid(badgeUid);
    }
}

NexorsysCredentialProvider::NexorsysCredentialProvider() : _cRef(1), _pcpe(NULL), _cpus(CPUS_INVALID), _pCredential(NULL), _bBadgeDetected(false)
{
    if (g_pReader == nullptr)
    {
        g_pReader = new SmartCardReader();
        g_pReader->StartMonitor(OnCardInserted, this);
    }
}

NexorsysCredentialProvider::~NexorsysCredentialProvider()
{
    if (g_pReader != nullptr)
    {
        g_pReader->StopMonitor();
        delete g_pReader;
        g_pReader = nullptr;
    }

    if (_pcpe) _pcpe->Release();
    if (_pCredential) _pCredential->Release();
}

void NexorsysCredentialProvider::UpdateBadgeUid(LPCWSTR badgeUid)
{
    _bBadgeDetected = true;
    if (_pCredential)
    {
        _pCredential->SetBadgeUid(badgeUid);
    }

    SetThreadExecutionState(ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);

    INPUT input = {0};
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = VK_SHIFT;
    SendInput(1, &input, sizeof(INPUT));
    
    input.ki.dwFlags = KEYEVENTF_KEYUP;
    SendInput(1, &input, sizeof(INPUT));

    if (_pcpe)
    {
        _pcpe->CredentialsChanged(_upAdviseContext);
    }
}

HRESULT NexorsysCredentialProvider::QueryInterface(REFIID riid, void** ppv)
{
    if (riid == IID_IUnknown || riid == IID_ICredentialProvider)
    {
        *ppv = this;
        AddRef();
        return S_OK;
    }
    *ppv = NULL;
    return E_NOINTERFACE;
}

ULONG NexorsysCredentialProvider::AddRef() { return InterlockedIncrement(&_cRef); }

ULONG NexorsysCredentialProvider::Release()
{
    LONG cRef = InterlockedDecrement(&_cRef);
    if (!cRef) delete this;
    return cRef;
}

HRESULT NexorsysCredentialProvider::SetUsageScenario(CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus, DWORD dwFlags)
{
    switch (cpus)
    {
    case CPUS_LOGON:
    case CPUS_UNLOCK_WORKSTATION:
        _cpus = cpus;
        return S_OK;
    case CPUS_CREDUI:
    case CPUS_CHANGE_PASSWORD:
    default:
        return E_NOTIMPL;
    }
}

HRESULT NexorsysCredentialProvider::SetSerialization(const CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs)
{
    return E_NOTIMPL;
}

HRESULT NexorsysCredentialProvider::Advise(ICredentialProviderEvents* pcpe, UINT_PTR upAdviseContext)
{
    if (_pcpe) _pcpe->Release();
    _pcpe = pcpe;
    if (_pcpe) _pcpe->AddRef();
    _upAdviseContext = upAdviseContext;
    return S_OK;
}

HRESULT NexorsysCredentialProvider::UnAdvise()
{
    if (_pcpe)
    {
        _pcpe->Release();
        _pcpe = NULL;
    }
    return S_OK;
}

static const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR s_rgFieldDescriptors[] =
{
    { CPFI_LOGO, CPFT_TILE_IMAGE, L"Logo", GUID_NULL },
    { CPFI_LARGE_TEXT, CPFT_LARGE_TEXT, L"Pinède", GUID_NULL },
    { CPFI_USERNAME, CPFT_EDIT_TEXT, L"Nom d'utilisateur", GUID_NULL },
    { CPFI_PIN, CPFT_PASSWORD_TEXT, L"PIN", GUID_NULL },
    { CPFI_AD_PASSWORD, CPFT_PASSWORD_TEXT, L"Mot de passe AD", GUID_NULL },
    { CPFI_SUBMIT, CPFT_SUBMIT_BUTTON, L"Valider", GUID_NULL },
    { CPFI_FORGOT_PIN, CPFT_COMMAND_LINK, L"Code PIN ou Identifiants oubliés ?", GUID_NULL },
    { CPFI_STATUS_TEXT, CPFT_SMALL_TEXT, L"Status", GUID_NULL }
};

HRESULT NexorsysCredentialProvider::GetFieldDescriptorCount(DWORD* pdwCount)
{
    *pdwCount = ARRAYSIZE(s_rgFieldDescriptors);
    return S_OK;
}

HRESULT NexorsysCredentialProvider::GetFieldDescriptorAt(DWORD dwIndex, CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR** ppcpfd)
{
    if (dwIndex >= ARRAYSIZE(s_rgFieldDescriptors))
        return E_INVALIDARG;

    *ppcpfd = (CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR*)CoTaskMemAlloc(sizeof(CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR));
    if (*ppcpfd)
    {
        **ppcpfd = s_rgFieldDescriptors[dwIndex];
        (*ppcpfd)->pszLabel = nullptr;
        if (s_rgFieldDescriptors[dwIndex].pszLabel)
        {
            SHStrDupW(s_rgFieldDescriptors[dwIndex].pszLabel, &(*ppcpfd)->pszLabel);
        }
        return S_OK;
    }
    return E_OUTOFMEMORY;
}

HRESULT NexorsysCredentialProvider::GetCredentialCount(DWORD* pdwCount, DWORD* pdwDefault, BOOL* pbAutoLogonWithDefault)
{
    *pdwCount = 1; // 1 tile
    if (_bBadgeDetected)
    {
        *pdwDefault = 0;
        *pbAutoLogonWithDefault = TRUE;
    }
    else
    {
        *pdwDefault = CREDENTIAL_PROVIDER_NO_DEFAULT;
        *pbAutoLogonWithDefault = FALSE;
    }
    return S_OK;
}

HRESULT NexorsysCredentialProvider::GetCredentialAt(DWORD dwIndex, ICredentialProviderCredential** ppcpc)
{
    if (dwIndex != 0) return E_INVALIDARG;

    if (!_pCredential)
    {
        _pCredential = new (std::nothrow) NexorsysCredential();
        if (_pCredential)
        {
            _pCredential->Initialize();
        }
    }

    if (_pCredential)
    {
        HRESULT hr = _pCredential->QueryInterface(IID_ICredentialProviderCredential, reinterpret_cast<void**>(ppcpc));
        return hr;
    }
    return E_OUTOFMEMORY;
}
