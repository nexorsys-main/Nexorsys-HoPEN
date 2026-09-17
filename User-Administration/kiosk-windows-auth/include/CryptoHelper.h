#pragma once
#include <windows.h>
#include <bcrypt.h>
#include <string>
#include <vector>

class CryptoHelper
{
public:
    static HRESULT DeriveKey(LPCWSTR pin, LPCWSTR badgeUid, std::vector<BYTE>& outKey);
    static HRESULT EncryptPassword(const std::vector<BYTE>& key, LPCWSTR plainPassword, std::vector<BYTE>& outCiphertext);
    static HRESULT DecryptPassword(const std::vector<BYTE>& key, const std::vector<BYTE>& ciphertext, std::wstring& outPlainPassword);
};
