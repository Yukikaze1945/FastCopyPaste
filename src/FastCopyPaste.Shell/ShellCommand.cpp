#include <windows.h>
#include <shobjidl_core.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <atomic>
#include <new>
#include <string>
#include <vector>

namespace
{
    // Must match the CLSID in packaging/AppxManifest.xml.
    constexpr CLSID PasteCommandClsid{
        0x6f0b7f46,
        0x8b55,
        0x4bc4,
        {0xaf, 0xd7, 0x9d, 0x73, 0xf1, 0x17, 0xc3, 0xc1}};

    HMODULE moduleHandle = nullptr;
    std::atomic<long> serverLocks = 0;
    std::atomic<long> activeObjects = 0;

    std::wstring QuoteArgument(const std::wstring& value)
    {
        std::wstring result = L"\"";
        unsigned int pendingBackslashes = 0;
        for (const wchar_t character : value)
        {
            if (character == L'\\')
            {
                ++pendingBackslashes;
                continue;
            }

            if (character == L'\"')
            {
                result.append(pendingBackslashes * 2U + 1U, L'\\');
                result.push_back(L'\"');
                pendingBackslashes = 0;
                continue;
            }

            result.append(pendingBackslashes, L'\\');
            pendingBackslashes = 0;
            result.push_back(character);
        }

        result.append(pendingBackslashes * 2U, L'\\');
        result.push_back(L'\"');
        return result;
    }

    HRESULT GetTargetDirectory(IShellItemArray* items, std::wstring& target)
    {
        if (items == nullptr)
        {
            return E_INVALIDARG;
        }

        DWORD count = 0;
        HRESULT result = items->GetCount(&count);
        if (FAILED(result) || count == 0)
        {
            return FAILED(result) ? result : E_INVALIDARG;
        }

        IShellItem* item = nullptr;
        result = items->GetItemAt(0, &item);
        if (FAILED(result))
        {
            return result;
        }

        PWSTR path = nullptr;
        result = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
        item->Release();
        if (FAILED(result))
        {
            return result;
        }

        const DWORD attributes = GetFileAttributesW(path);
        if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0)
        {
            CoTaskMemFree(path);
            return HRESULT_FROM_WIN32(ERROR_DIRECTORY);
        }

        target.assign(path);
        CoTaskMemFree(path);
        return S_OK;
    }

    HRESULT GetHostPath(std::wstring& hostPath, std::wstring* moduleDirectory = nullptr)
    {
        wchar_t modulePath[32768]{};
        const DWORD length = GetModuleFileNameW(moduleHandle, modulePath, ARRAYSIZE(modulePath));
        if (length == 0 || length == ARRAYSIZE(modulePath))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        if (!PathRemoveFileSpecW(modulePath))
        {
            return E_UNEXPECTED;
        }

        if (moduleDirectory != nullptr)
        {
            moduleDirectory->assign(modulePath);
        }

        hostPath.assign(modulePath);
        hostPath += L"\\FastCopyPaste.Host.exe";
        if (GetFileAttributesW(hostPath.c_str()) == INVALID_FILE_ATTRIBUTES)
        {
            return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        }

        return S_OK;
    }

    HRESULT LaunchHost(const std::wstring& target)
    {
        std::wstring hostPath;
        std::wstring moduleDirectory;
        const HRESULT pathResult = GetHostPath(hostPath, &moduleDirectory);
        if (FAILED(pathResult))
        {
            return pathResult;
        }

        std::wstring commandLine = QuoteArgument(hostPath) + L" --paste-target " + QuoteArgument(target);
        std::vector<wchar_t> mutableCommand(commandLine.begin(), commandLine.end());
        mutableCommand.push_back(L'\0');

        STARTUPINFOW startupInfo{};
        startupInfo.cb = sizeof(startupInfo);
        PROCESS_INFORMATION processInfo{};
        if (!CreateProcessW(
                hostPath.c_str(),
                mutableCommand.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_NO_WINDOW,
                nullptr,
                moduleDirectory.c_str(),
                &startupInfo,
                &processInfo))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        CloseHandle(processInfo.hThread);
        CloseHandle(processInfo.hProcess);
        return S_OK;
    }

    class PasteCommand final : public IExplorerCommand
    {
    public:
        PasteCommand()
        {
            ++activeObjects;
        }

        IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** value) override
        {
            if (value == nullptr)
            {
                return E_POINTER;
            }

            *value = nullptr;
            if (interfaceId == IID_IUnknown || interfaceId == __uuidof(IExplorerCommand))
            {
                *value = static_cast<IExplorerCommand*>(this);
                AddRef();
                return S_OK;
            }

            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override
        {
            return static_cast<ULONG>(InterlockedIncrement(&references_));
        }

        IFACEMETHODIMP_(ULONG) Release() override
        {
            const long result = InterlockedDecrement(&references_);
            if (result == 0)
            {
                delete this;
            }
            return static_cast<ULONG>(result);
        }

        IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
        {
            return title == nullptr ? E_POINTER : SHStrDupW(L"FastCopy 粘贴到这里", title);
        }

        IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
        {
            if (icon == nullptr)
            {
                return E_POINTER;
            }
            *icon = nullptr;
            std::wstring hostPath;
            const HRESULT result = GetHostPath(hostPath);
            if (FAILED(result))
            {
                return result;
            }

            hostPath += L",0";
            return SHStrDupW(hostPath.c_str(), icon);
        }

        IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tooltip) override
        {
            return tooltip == nullptr
                ? E_POINTER
                : SHStrDupW(L"使用 FastCopy 把剪贴板文件粘贴到此目录", tooltip);
        }

        IFACEMETHODIMP GetCanonicalName(GUID* commandName) override
        {
            if (commandName == nullptr)
            {
                return E_POINTER;
            }
            *commandName = PasteCommandClsid;
            return S_OK;
        }

        IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override
        {
            if (state == nullptr)
            {
                return E_POINTER;
            }
            *state = IsClipboardFormatAvailable(CF_HDROP) ? ECS_ENABLED : ECS_DISABLED;
            return S_OK;
        }

        IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
        {
            std::wstring target;
            const HRESULT result = GetTargetDirectory(items, target);
            return FAILED(result) ? result : LaunchHost(target);
        }

        IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
        {
            if (flags == nullptr)
            {
                return E_POINTER;
            }
            *flags = ECF_DEFAULT;
            return S_OK;
        }

        IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
        {
            if (commands == nullptr)
            {
                return E_POINTER;
            }
            *commands = nullptr;
            return E_NOTIMPL;
        }

    private:
        ~PasteCommand()
        {
            --activeObjects;
        }
        long references_ = 1;
    };

    class CommandFactory final : public IClassFactory
    {
    public:
        CommandFactory()
        {
            ++activeObjects;
        }

        IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** value) override
        {
            if (value == nullptr)
            {
                return E_POINTER;
            }
            *value = nullptr;
            if (interfaceId == IID_IUnknown || interfaceId == IID_IClassFactory)
            {
                *value = static_cast<IClassFactory*>(this);
                AddRef();
                return S_OK;
            }
            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override
        {
            return static_cast<ULONG>(InterlockedIncrement(&references_));
        }

        IFACEMETHODIMP_(ULONG) Release() override
        {
            const long result = InterlockedDecrement(&references_);
            if (result == 0)
            {
                delete this;
            }
            return static_cast<ULONG>(result);
        }

        IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID interfaceId, void** value) override
        {
            if (outer != nullptr)
            {
                return CLASS_E_NOAGGREGATION;
            }

            auto* command = new (std::nothrow) PasteCommand();
            if (command == nullptr)
            {
                return E_OUTOFMEMORY;
            }
            const HRESULT result = command->QueryInterface(interfaceId, value);
            command->Release();
            return result;
        }

        IFACEMETHODIMP LockServer(BOOL lock) override
        {
            if (lock)
            {
                ++serverLocks;
            }
            else
            {
                --serverLocks;
            }
            return S_OK;
        }

    private:
        ~CommandFactory()
        {
            --activeObjects;
        }
        long references_ = 1;
    };
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, void*)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        moduleHandle = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    return serverLocks.load() == 0 && activeObjects.load() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID classId, REFIID interfaceId, void** value)
{
    if (classId != PasteCommandClsid)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto* factory = new (std::nothrow) CommandFactory();
    if (factory == nullptr)
    {
        return E_OUTOFMEMORY;
    }
    const HRESULT result = factory->QueryInterface(interfaceId, value);
    factory->Release();
    return result;
}
