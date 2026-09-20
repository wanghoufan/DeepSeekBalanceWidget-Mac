using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// A native NSStatusItem with a text title. Avalonia's cross-platform tray API
/// supports an icon and tooltip, but macOS needs NSStatusItem directly to show a
/// live value in the menu bar like Weather does.
/// </summary>
public sealed class MacMenuBarBalance : IDisposable
{
    private const double VariableStatusItemLength = -1d;
    private readonly IntPtr _statusBar;
    private readonly IntPtr _statusItem;
    private readonly IntPtr _button;
    private readonly IntPtr _target;
    private bool _disposed;

    private MacMenuBarBalance(IntPtr statusBar, IntPtr statusItem, IntPtr button, IntPtr target)
    {
        _statusBar = statusBar;
        _statusItem = statusItem;
        _button = button;
        _target = target;
    }

    public static MacMenuBarBalance? Create(Action onClick)
    {
        ArgumentNullException.ThrowIfNull(onClick);
        if (!OperatingSystem.IsMacOS()) return null;

        IntPtr statusBar = SendIntPtr(GetClass("NSStatusBar"), GetSelector("systemStatusBar"));
        IntPtr statusItem = SendIntPtrDouble(
            statusBar, GetSelector("statusItemWithLength:"), VariableStatusItemLength);
        IntPtr button = SendIntPtr(statusItem, GetSelector("button"));
        if (statusBar == IntPtr.Zero || statusItem == IntPtr.Zero || button == IntPtr.Zero)
            return null;

        // statusItemWithLength: 返回的对象不被 NSStatusBar 持有，必须显式 retain，
        // 否则 autorelease pool 清空后对象被释放，后续 objc_msgSend 会 SIGSEGV。
        SendVoid(statusItem, GetSelector("retain"));
        return CreateWithClickTarget(statusBar, statusItem, button, onClick);
    }

    private static MacMenuBarBalance? CreateWithClickTarget(
        IntPtr statusBar, IntPtr statusItem, IntPtr button, Action onClick)
    {
        EnsureClickTargetClass();
        IntPtr target = SendIntPtr(GetClass(ClickTargetClassName), GetSelector("new"));
        if (target == IntPtr.Zero) return null;

        ClickHandlers[target] = onClick;
        SendVoidIntPtr(button, GetSelector("setTarget:"), target);
        SendVoidIntPtr(button, GetSelector("setAction:"), GetSelector(ClickSelectorName));
        return new MacMenuBarBalance(statusBar, statusItem, button, target);
    }

    public void Update(string title, string tooltip)
    {
        if (_disposed) return;
        SendVoidIntPtr(_button, GetSelector("setTitle:"), CreateString(title));
        SendVoidIntPtr(_button, GetSelector("setToolTip:"), CreateString(tooltip));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SendVoidIntPtr(_button, GetSelector("setTarget:"), IntPtr.Zero);
        SendVoidIntPtr(_button, GetSelector("setAction:"), IntPtr.Zero);
        SendVoidIntPtr(_statusBar, GetSelector("removeStatusItem:"), _statusItem);
        SendVoid(_statusItem, GetSelector("release"));
        ClickHandlers.TryRemove(_target, out _);
        SendVoid(_target, GetSelector("release"));
    }

    private const string ClickTargetClassName = "DeepSeekBalanceWidgetStatusItemTarget";
    private const string ClickSelectorName = "deepSeekBalanceWidgetStatusItemClicked:";
    private static readonly ConcurrentDictionary<IntPtr, Action> ClickHandlers = new();
    private static readonly StatusItemClickDelegate ClickImplementation = HandleClick;
    private static readonly object ClickTargetClassLock = new();
    private static bool _clickTargetClassReady;

    private static void EnsureClickTargetClass()
    {
        if (_clickTargetClassReady) return;
        lock (ClickTargetClassLock)
        {
            if (_clickTargetClassReady) return;

            IntPtr targetClass = GetClass(ClickTargetClassName);
            if (targetClass == IntPtr.Zero)
            {
                targetClass = AllocateClassPair(GetClass("NSObject"), ClickTargetClassName, IntPtr.Zero);
                if (targetClass == IntPtr.Zero)
                    throw new InvalidOperationException("Unable to create the macOS menu-bar click target.");

                AddMethod(
                    targetClass,
                    GetSelector(ClickSelectorName),
                    Marshal.GetFunctionPointerForDelegate(ClickImplementation),
                    "v@:@");
                RegisterClassPair(targetClass);
            }

            _clickTargetClassReady = true;
        }
    }

    private static void HandleClick(IntPtr target, IntPtr selector, IntPtr sender)
    {
        if (!ClickHandlers.TryGetValue(target, out var onClick)) return;
        try { onClick(); }
        catch { /* Never let an exception cross the Objective-C callback boundary. */ }
    }

    private static IntPtr CreateString(string value)
    {
        IntPtr utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return SendIntPtrIntPtr(
                GetClass("NSString"), GetSelector("stringWithUTF8String:"), utf8);
        }
        finally { Marshal.FreeCoTaskMem(utf8); }
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr GetSelector(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtrIntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtrDouble(IntPtr receiver, IntPtr selector, double argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendVoidIntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_allocateClassPair")]
    private static extern IntPtr AllocateClassPair(IntPtr superclass, string name, IntPtr extraBytes);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "class_addMethod")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AddMethod(IntPtr targetClass, IntPtr selector, IntPtr implementation, string types);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_registerClassPair")]
    private static extern void RegisterClassPair(IntPtr targetClass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StatusItemClickDelegate(IntPtr target, IntPtr selector, IntPtr sender);
}
