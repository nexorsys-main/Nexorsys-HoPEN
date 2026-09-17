#include "SmartCardReader.h"
#include <strsafe.h>
#include <vector>
#include <string>
#include <wincrypt.h>

#pragma comment(lib, "winscard.lib")

SmartCardReader::SmartCardReader() : _hContext(0), _hThread(NULL), _callback(NULL), _pContext(NULL)
{
    _hStopEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
}

SmartCardReader::~SmartCardReader()
{
    StopMonitor();
    if (_hStopEvent) CloseHandle(_hStopEvent);
}

HRESULT SmartCardReader::StartMonitor(CardInsertedCallback callback, void* pContext)
{
    _callback = callback;
    _pContext = pContext;

    LONG lReturn = SCardEstablishContext(SCARD_SCOPE_SYSTEM, NULL, NULL, &_hContext);
    if (lReturn != SCARD_S_SUCCESS) return E_FAIL;

    ResetEvent(_hStopEvent);
    _hThread = CreateThread(NULL, 0, MonitorThread, this, 0, NULL);
    return _hThread ? S_OK : E_FAIL;
}

void SmartCardReader::StopMonitor()
{
    if (_hThread)
    {
        SetEvent(_hStopEvent);
        WaitForSingleObject(_hThread, INFINITE);
        CloseHandle(_hThread);
        _hThread = NULL;
    }
    if (_hContext)
    {
        SCardReleaseContext(_hContext);
        _hContext = 0;
    }
}

DWORD WINAPI SmartCardReader::MonitorThread(LPVOID lpParam)
{
    SmartCardReader* pThis = static_cast<SmartCardReader*>(lpParam);
    pThis->RunMonitor();
    return 0;
}

void SmartCardReader::RunMonitor()
{
    std::wstring lastUid = L"";

    while (WaitForSingleObject(_hStopEvent, 1000) != WAIT_OBJECT_0)
    {
        LPWSTR mszReaders = NULL;
        DWORD dwReaders = SCARD_AUTOALLOCATE;
        if (SCardListReaders(_hContext, NULL, (LPWSTR)&mszReaders, &dwReaders) == SCARD_S_SUCCESS)
        {
            bool cardFound = false;
            LPWSTR pReader = mszReaders;
            while (*pReader != L'\0')
            {
                SCARDHANDLE hCard = 0;
                DWORD dwActiveProtocol = 0;
                if (SCardConnect(_hContext, pReader, SCARD_SHARE_SHARED, SCARD_PROTOCOL_T0 | SCARD_PROTOCOL_T1, &hCard, &dwActiveProtocol) == SCARD_S_SUCCESS)
                {
                    std::wstring uidFinal = L"";

                    // Strategy 1: APDU to get UID for Mifare / NFC
                    BYTE pbSend[] = { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
                    BYTE pbRecv[256];
                    DWORD cbRecv = sizeof(pbRecv);
                    SCARD_IO_REQUEST pioSendPci;
                    pioSendPci.dwProtocol = dwActiveProtocol;
                    pioSendPci.cbPciLength = sizeof(pioSendPci);

                    if (SCardTransmit(hCard, &pioSendPci, pbSend, sizeof(pbSend), NULL, pbRecv, &cbRecv) == SCARD_S_SUCCESS)
                    {
                        if (cbRecv >= 2 && pbRecv[cbRecv - 2] == 0x90 && pbRecv[cbRecv - 1] == 0x00)
                        {
                            for (DWORD i = 0; i < cbRecv - 2; ++i)
                            {
                                wchar_t hex[3];
                                swprintf_s(hex, 3, L"%02X", pbRecv[i]);
                                uidFinal += hex;
                            }
                        }
                    }

                    SCardDisconnect(hCard, SCARD_LEAVE_CARD);

                    // Strategy 2: Windows CryptoAPI for Gemalto/CPS SmartCards
                    if (uidFinal.empty())
                    {
                        HCRYPTPROV hProv = 0;
                        std::wstring readerPath = L"\\\\.\\" + std::wstring(pReader) + L"\\";
                        
                        // CRYPT_SILENT prevents PIN prompts
                        if (CryptAcquireContextW(&hProv, readerPath.c_str(), L"Microsoft Base Smart Card Crypto Provider", PROV_RSA_FULL, CRYPT_SILENT))
                        {
                            DWORD len = 0;
                            if (CryptGetProvParam(hProv, 36 /* PP_UNIQUE_CONTAINER */, NULL, &len, 0) && len > 0)
                            {
                                BYTE* buf = new BYTE[len];
                                if (CryptGetProvParam(hProv, 36, buf, &len, 0))
                                {
                                    std::string name(reinterpret_cast<char*>(buf));
                                    if (!name.empty() && name.back() == '\0') name.pop_back();
                                    uidFinal.assign(name.begin(), name.end());
                                    uidFinal = L"CSP-" + uidFinal;
                                }
                                delete[] buf;
                            }
                            CryptReleaseContext(hProv, 0);
                        }
                    }

                    if (!uidFinal.empty())
                    {
                        if (uidFinal != lastUid)
                        {
                            lastUid = uidFinal;
                            if (_callback) _callback(uidFinal.c_str(), _pContext);
                        }
                        cardFound = true;
                    }
                }
                pReader += wcslen(pReader) + 1;
            }
            SCardFreeMemory(_hContext, mszReaders);
            
            if (!cardFound) lastUid = L"";
        }
    }
}
