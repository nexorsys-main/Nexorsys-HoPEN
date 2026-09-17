#pragma once
#include <windows.h>
#include <winscard.h>
#include <string>

// Callback function type when a card is inserted
typedef void(*CardInsertedCallback)(LPCWSTR badgeUid, void* pContext);

class SmartCardReader
{
public:
    SmartCardReader();
    ~SmartCardReader();

    HRESULT StartMonitor(CardInsertedCallback callback, void* pContext);
    void StopMonitor();

private:
    static DWORD WINAPI MonitorThread(LPVOID lpParam);
    void RunMonitor();

    SCARDCONTEXT _hContext;
    HANDLE _hThread;
    HANDLE _hStopEvent;
    
    CardInsertedCallback _callback;
    void* _pContext;
};
