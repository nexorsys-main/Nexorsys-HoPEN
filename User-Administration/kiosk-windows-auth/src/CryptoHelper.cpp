#include "CryptoHelper.h"
#include <winternl.h>

#pragma comment(lib, "bcrypt.lib")

#define ITERATIONS 100000

HRESULT CryptoHelper::DeriveKey(LPCWSTR pin, LPCWSTR badgeUid, std::vector<BYTE>& outKey)
{
    BCRYPT_ALG_HANDLE hAlg = NULL;
    HRESULT hr = S_OK;

    if (!NT_SUCCESS(BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_SHA256_ALGORITHM, NULL, BCRYPT_ALG_HANDLE_HMAC_FLAG)))
        return E_FAIL;

    // The machine-bound secret must be provisioned outside the binary by the
    // installer/LSA/TPM integration. Refuse to derive a key without it.
    wchar_t machineSecret[512] = {};
    DWORD secretLength = GetEnvironmentVariableW(L"NEXORSYS_KIOSK_SECRET", machineSecret, ARRAYSIZE(machineSecret));
    if (secretLength == 0 || secretLength >= ARRAYSIZE(machineSecret)) return E_ACCESSDENIED;
    std::wstring saltStr = std::wstring(badgeUid) + machineSecret;
    
    outKey.resize(32); // 256 bits

    NTSTATUS status = BCryptDeriveKeyPBKDF2(
        hAlg,
        (PUCHAR)pin, (ULONG)(wcslen(pin) * sizeof(WCHAR)),
        (PUCHAR)saltStr.c_str(), (ULONG)(saltStr.length() * sizeof(WCHAR)),
        ITERATIONS,
        outKey.data(), (ULONG)outKey.size(),
        0);

    BCryptCloseAlgorithmProvider(hAlg, 0);

    return NT_SUCCESS(status) ? S_OK : E_FAIL;
}

HRESULT CryptoHelper::EncryptPassword(const std::vector<BYTE>& key, LPCWSTR plainPassword, std::vector<BYTE>& outCiphertext)
{
    BCRYPT_ALG_HANDLE hAlg = NULL;
    BCRYPT_KEY_HANDLE hKey = NULL;
    HRESULT hr = S_OK;

    if (!NT_SUCCESS(BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_AES_ALGORITHM, NULL, 0)))
        return E_FAIL;

    if (!NT_SUCCESS(BCryptSetProperty(hAlg, BCRYPT_CHAINING_MODE, (PUCHAR)BCRYPT_CHAIN_MODE_GCM, sizeof(BCRYPT_CHAIN_MODE_GCM), 0)))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return E_FAIL;
    }

    if (!NT_SUCCESS(BCryptGenerateSymmetricKey(hAlg, &hKey, NULL, 0, (PUCHAR)key.data(), (ULONG)key.size(), 0)))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return E_FAIL;
    }

    // GCM requires nonce (12 bytes) and auth tag (16 bytes)
    BYTE nonce[12] = { 0 }; // In production, generate randomly using BCryptGenRandom
    BCryptGenRandom(NULL, nonce, sizeof(nonce), BCRYPT_USE_SYSTEM_PREFERRED_RNG);

    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO authInfo;
    BCRYPT_INIT_AUTH_MODE_INFO(authInfo);
    
    BYTE mac[16];
    authInfo.pbNonce = nonce;
    authInfo.cbNonce = sizeof(nonce);
    authInfo.pbTag = mac;
    authInfo.cbTag = sizeof(mac);

    ULONG cbResult = 0;
    PUCHAR pPlain = (PUCHAR)plainPassword;
    ULONG cbPlain = (ULONG)(wcslen(plainPassword) * sizeof(WCHAR));

    if (!NT_SUCCESS(BCryptEncrypt(hKey, pPlain, cbPlain, &authInfo, NULL, 0, NULL, 0, &cbResult, 0)))
    {
        BCryptDestroyKey(hKey);
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return E_FAIL;
    }

    std::vector<BYTE> cipherOnly(cbResult);
    if (!NT_SUCCESS(BCryptEncrypt(hKey, pPlain, cbPlain, &authInfo, NULL, 0, cipherOnly.data(), cbResult, &cbResult, 0)))
    {
        BCryptDestroyKey(hKey);
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return E_FAIL;
    }

    // Format output: [Nonce (12)] + [MAC (16)] + [Ciphertext...]
    outCiphertext.insert(outCiphertext.end(), nonce, nonce + sizeof(nonce));
    outCiphertext.insert(outCiphertext.end(), mac, mac + sizeof(mac));
    outCiphertext.insert(outCiphertext.end(), cipherOnly.begin(), cipherOnly.end());

    BCryptDestroyKey(hKey);
    BCryptCloseAlgorithmProvider(hAlg, 0);
    return S_OK;
}

HRESULT CryptoHelper::DecryptPassword(const std::vector<BYTE>& key, const std::vector<BYTE>& ciphertext, std::wstring& outPlainPassword)
{
    if (ciphertext.size() < 28) return E_INVALIDARG; // Minimum size for nonce + mac

    BCRYPT_ALG_HANDLE hAlg = NULL;
    BCRYPT_KEY_HANDLE hKey = NULL;

    if (!NT_SUCCESS(BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_AES_ALGORITHM, NULL, 0)))
        return E_FAIL;

    if (!NT_SUCCESS(BCryptSetProperty(hAlg, BCRYPT_CHAINING_MODE, (PUCHAR)BCRYPT_CHAIN_MODE_GCM, sizeof(BCRYPT_CHAIN_MODE_GCM), 0)))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return E_FAIL;
    }

    if (!NT_SUCCESS(BCryptGenerateSymmetricKey(hAlg, &hKey, NULL, 0, (PUCHAR)key.data(), (ULONG)key.size(), 0)))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return E_FAIL;
    }

    BYTE nonce[12];
    BYTE mac[16];
    memcpy(nonce, ciphertext.data(), 12);
    memcpy(mac, ciphertext.data() + 12, 16);

    std::vector<BYTE> cipherOnly(ciphertext.begin() + 28, ciphertext.end());

    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO authInfo;
    BCRYPT_INIT_AUTH_MODE_INFO(authInfo);
    authInfo.pbNonce = nonce;
    authInfo.cbNonce = sizeof(nonce);
    authInfo.pbTag = mac;
    authInfo.cbTag = sizeof(mac);

    ULONG cbResult = 0;
    if (!NT_SUCCESS(BCryptDecrypt(hKey, cipherOnly.data(), (ULONG)cipherOnly.size(), &authInfo, NULL, 0, NULL, 0, &cbResult, 0)))
    {
        BCryptDestroyKey(hKey);
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return E_FAIL; // Decryption failed (wrong PIN or tampered blob)
    }

    std::vector<BYTE> plain(cbResult);
    if (!NT_SUCCESS(BCryptDecrypt(hKey, cipherOnly.data(), (ULONG)cipherOnly.size(), &authInfo, NULL, 0, plain.data(), cbResult, &cbResult, 0)))
    {
        BCryptDestroyKey(hKey);
        BCryptCloseAlgorithmProvider(hAlg, 0);
        return E_FAIL;
    }

    outPlainPassword.assign(reinterpret_cast<wchar_t*>(plain.data()), plain.size() / sizeof(wchar_t));

    BCryptDestroyKey(hKey);
    BCryptCloseAlgorithmProvider(hAlg, 0);
    return S_OK;
}
