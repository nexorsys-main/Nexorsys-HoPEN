#include "common.h"
#include "guid.h"
#include "NexorsysCredentialProvider.h"
#include <new>

LONG g_cRef = 0;
HINSTANCE g_hinst = NULL;

void DllAddRef() { InterlockedIncrement(&g_cRef); }
void DllRelease() { InterlockedDecrement(&g_cRef); }

class CClassFactory : public IClassFactory
{
public:
    CClassFactory() : _cRef(1) {}

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv)
    {
        if (riid == IID_IUnknown || riid == IID_IClassFactory)
        {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = NULL;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() { return InterlockedIncrement(&_cRef); }
    IFACEMETHODIMP_(ULONG) Release()
    {
        LONG cRef = InterlockedDecrement(&_cRef);
        if (!cRef) delete this;
        return cRef;
    }

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
    {
        if (pUnkOuter) return CLASS_E_NOAGGREGATION;
        
        NexorsysCredentialProvider* pProvider = new (std::nothrow) NexorsysCredentialProvider();
        if (!pProvider) return E_OUTOFMEMORY;

        HRESULT hr = pProvider->QueryInterface(riid, ppv);
        pProvider->Release();
        return hr;
    }
    IFACEMETHODIMP LockServer(BOOL fLock)
    {
        if (fLock) DllAddRef();
        else DllRelease();
        return S_OK;
    }

private:
    ~CClassFactory() {}
    LONG _cRef;
};

STDAPI DllGetClassObject(REFIID rclsid, REFIID riid, void** ppv)
{
    if (rclsid != CLSID_NexorsysCredentialProvider) return CLASS_E_CLASSNOTAVAILABLE;
    
    CClassFactory* pFactory = new (std::nothrow) CClassFactory();
    if (!pFactory) return E_OUTOFMEMORY;
    
    HRESULT hr = pFactory->QueryInterface(riid, ppv);
    pFactory->Release();
    return hr;
}

STDAPI DllCanUnloadNow()
{
    return (g_cRef > 0) ? S_FALSE : S_OK;
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    switch (fdwReason)
    {
    case DLL_PROCESS_ATTACH:
        g_hinst = hinstDLL;
        DisableThreadLibraryCalls(hinstDLL);
        break;
    }
    return TRUE;
}

STDAPI DllRegisterServer()
{
    // Registration is handled externally via the PowerShell deployment scripts
    return S_OK;
}

STDAPI DllUnregisterServer()
{
    // Unregistration is handled externally
    return S_OK;
}
