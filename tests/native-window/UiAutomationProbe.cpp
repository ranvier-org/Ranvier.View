#define NOMINMAX
#include <windows.h>
#include <UIAutomation.h>
#include <wrl/client.h>
#include <cstdio>
#include <cstdlib>
#include <stdexcept>
using Microsoft::WRL::ComPtr;
static void require(bool value, const char* reason) { if (!value) throw std::runtime_error(reason); }
static void ok(HRESULT result, const char* reason) { require(SUCCEEDED(result), reason); }
static ComPtr<IUIAutomationElement> buttonNamed(IUIAutomation* automation, IUIAutomationElement* root, const wchar_t* name)
{
    VARIANT value; VariantInit(&value); value.vt = VT_BSTR; value.bstrVal = SysAllocString(name);
    ComPtr<IUIAutomationCondition> condition; auto result = automation->CreatePropertyCondition(UIA_NamePropertyId, value, &condition);
    VariantClear(&value); ok(result, "name condition");
    ComPtr<IUIAutomationElement> element; ok(root->FindFirst(TreeScope_Descendants, condition.Get(), &element), "find button");
    return element;
}
int main(int argc, char** argv) try {
    require(argc == 2, "expected native HWND argument");
    auto hwnd = reinterpret_cast<HWND>(std::strtoull(argv[1], nullptr, 10));
    require(IsWindow(hwnd), "SDL HWND is unavailable");
    DWORD process = 0; GetWindowThreadProcessId(hwnd, &process);
    require(process && process != GetCurrentProcessId(), "UIA probe must run in an independent process");
    ok(CoInitializeEx(nullptr, COINIT_MULTITHREADED), "initialize UI Automation client");
    {
        ComPtr<IUIAutomation> automation; ok(CoCreateInstance(CLSID_CUIAutomation8, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&automation)), "create UI Automation client");
        ComPtr<IUIAutomationElement> root; ok(automation->ElementFromHandle(hwnd, &root), "SDL accessibility root");
        auto button = buttonNamed(automation.Get(), root.Get(), L"Clicked 0 times"); require(button.Get(), "counter button missing from OS accessibility tree");
        CONTROLTYPEID type; ok(button->get_CurrentControlType(&type), "button control type"); require(type == UIA_ButtonControlTypeId, "counter is not exposed as a button");
        RECT bounds; ok(button->get_CurrentBoundingRectangle(&bounds), "button screen bounds"); require(bounds.right > bounds.left && bounds.bottom > bounds.top, "empty button screen bounds");
        ComPtr<IUIAutomationInvokePattern> invoke; ok(button->GetCurrentPatternAs(UIA_InvokePatternId, IID_PPV_ARGS(&invoke)), "button Invoke pattern");
        ok(invoke->Invoke(), "Invoke counter button");
        ComPtr<IUIAutomationElement> changed;
        auto deadline = GetTickCount64() + 10000;
        while (!changed && GetTickCount64() < deadline) { changed = buttonNamed(automation.Get(), root.Get(), L"Clicked 1 times"); if (!changed) Sleep(10); }
        require(changed.Get(), "Invoke did not update the counter's accessible name");
        require(PostMessageW(hwnd, WM_CLOSE, 0, 0), "request SDL window teardown");
        HRESULT disconnected = S_OK; deadline = GetTickCount64() + 10000;
        while (SUCCEEDED(disconnected) && GetTickCount64() < deadline) {
            BSTR name = nullptr; disconnected = changed->get_CurrentName(&name); SysFreeString(name);
            if (SUCCEEDED(disconnected)) Sleep(10);
        }
        require(disconnected == UIA_E_ELEMENTNOTAVAILABLE, "retained UIA element survived SDL/host teardown");
        std::puts("PASS independent UIA client: SDL tree, button bounds, Invoke, reactive name update and teardown");
    }
    CoUninitialize(); return 0;
} catch (const std::exception& error) { std::fprintf(stderr, "FAIL UIA client: %s\n", error.what()); return 1; }
