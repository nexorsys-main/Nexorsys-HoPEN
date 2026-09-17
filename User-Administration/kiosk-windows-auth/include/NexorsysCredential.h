#pragma once
#include "common.h"

class NexorsysCredential : public ICredentialProviderCredential2
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // ICredentialProviderCredential
    IFACEMETHODIMP Advise(ICredentialProviderCredentialEvents* pcpce);
    IFACEMETHODIMP UnAdvise();
    IFACEMETHODIMP SetSelected(BOOL* pbAutoLogon);
    IFACEMETHODIMP SetDeselected();
    IFACEMETHODIMP SetDeserializedAuthenticationState(DWORD dwState);
    IFACEMETHODIMP LogOnString(PWSTR* ppwsz);
    IFACEMETHODIMP GetStringValue(DWORD fieldID, PWSTR* ppwsz);
    IFACEMETHODIMP GetBitmapValue(DWORD fieldID, HBITMAP* phbmp);
    IFACEMETHODIMP GetCheckboxValue(DWORD fieldID, BOOL* pbChecked, PWSTR* ppwszLabel);
    IFACEMETHODIMP GetFieldState(DWORD dwFieldID, CREDENTIAL_PROVIDER_FIELD_STATE* pcpfs, CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE* pcpfis);
    IFACEMETHODIMP GetSubmitButtonValue(DWORD fieldID, DWORD* pdwAdjacentTo);
    IFACEMETHODIMP GetComboBoxValueCount(DWORD fieldID, DWORD* pcItems, DWORD* pdwSelectedItem);
    IFACEMETHODIMP GetComboBoxValueAt(DWORD fieldID, DWORD dwItem, PWSTR* ppwszItem);
    IFACEMETHODIMP SetStringValue(DWORD fieldID, LPCWSTR psz);
    IFACEMETHODIMP SetCheckboxValue(DWORD fieldID, BOOL bChecked);
    IFACEMETHODIMP SetComboBoxSelectedValue(DWORD fieldID, DWORD dwSelectedItem);
    IFACEMETHODIMP CommandLinkClicked(DWORD fieldID);
    IFACEMETHODIMP GetSerialization(CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE* pcpgsr, CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs, PWSTR* ppwszOptionalStatusText, CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon);
    IFACEMETHODIMP ReportResult(NTSTATUS ntsStatus, NTSTATUS ntsSubstatus, PWSTR* ppwszOptionalStatusText, CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon);

    // ICredentialProviderCredential2
    IFACEMETHODIMP GetUserSid(PWSTR* ppwszUserSid);

    NexorsysCredential();
    ~NexorsysCredential();

    HRESULT Initialize();
    void SetBadgeUid(LPCWSTR uid);

private:
    LONG _cRef;
    ICredentialProviderCredentialEvents* _pcpce;
    
    wchar_t _wszBadgeUid[MAX_BADGE_UID_LENGTH];
    wchar_t _wszUsername[256];
    wchar_t _wszAdPassword[256];
    wchar_t _wszPin[MAX_PIN_LENGTH];
    bool _bHasEnrolledBlob;
    
    HRESULT _CreateSerialization(LPCWSTR username, LPCWSTR password, CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs);
};
