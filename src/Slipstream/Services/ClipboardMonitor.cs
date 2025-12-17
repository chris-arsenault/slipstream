using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Slipstream.Content;
using Slipstream.Models;
using Slipstream.Native;

namespace Slipstream.Services;

public class ClipboardMonitor : IDisposable
{
    private readonly HwndSource _hwndSource;
    private readonly IntPtr _hwnd;
    private readonly ContentHandlerRegistry _contentRegistry;
    private bool _isOwnClipboardChange;
    private int? _pendingTargetSlot;
    private DateTime _ownClipboardChangeTime = DateTime.MinValue;
    private const int OwnClipboardCooldownMs = 500; // Ignore clipboard changes for 500ms after our own paste

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public ClipboardMonitor(IntPtr hwnd, HwndSource hwndSource, ContentHandlerRegistry? contentRegistry = null)
    {
        _hwnd = hwnd;
        _hwndSource = hwndSource;
        _contentRegistry = contentRegistry ?? new ContentHandlerRegistry();
        _hwndSource.AddHook(WndProc);
    }

    public void Start()
    {
        Win32.AddClipboardFormatListener(_hwnd);
    }

    public void Stop()
    {
        Win32.RemoveClipboardFormatListener(_hwnd);
    }

    public void SetOwnClipboardChange(bool isOwn)
    {
        _isOwnClipboardChange = isOwn;
        if (isOwn)
        {
            _ownClipboardChangeTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Sets the target slot for the next clipboard capture.
    /// The next WM_CLIPBOARDUPDATE will capture to this slot instead of round-robin.
    /// </summary>
    public void SetPendingTargetSlot(int slotIndex)
    {
        _pendingTargetSlot = slotIndex;
    }

    public void CaptureCurrentToSlot(int slotIndex)
    {
        // Force capture current clipboard to specific slot
        var (data, type) = GetClipboardContent();
        if (type != ClipboardType.Empty && data != null)
        {
            ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(data, type, slotIndex));
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_CLIPBOARDUPDATE)
        {
            // Check if we're within the cooldown period after our own clipboard change
            var timeSinceOwnChange = (DateTime.UtcNow - _ownClipboardChangeTime).TotalMilliseconds;
            bool inCooldown = timeSinceOwnChange < OwnClipboardCooldownMs;

            if (!_isOwnClipboardChange && !inCooldown)
            {
                OnClipboardChanged();
            }
            else if (inCooldown)
            {
                Console.WriteLine($"[ClipboardMonitor] Ignoring clipboard change during cooldown ({timeSinceOwnChange:F0}ms since paste)");
            }
            _isOwnClipboardChange = false;
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnClipboardChanged()
    {
        try
        {
            // Get the source process name before reading content
            var sourceProcess = GetClipboardOwnerProcessName();

            var (data, type) = GetClipboardContent();
            if (type != ClipboardType.Empty && data != null)
            {
                // Check if there's a pending target slot (from Ctrl+Alt+# hotkey)
                int? targetSlot = _pendingTargetSlot;
                _pendingTargetSlot = null; // Clear it after use

                ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(data, type, targetSlot, sourceProcess));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error capturing clipboard: {ex.Message}");
        }
    }

    private static string? GetClipboardOwnerProcessName()
    {
        try
        {
            var ownerHwnd = Win32.GetClipboardOwner();
            if (ownerHwnd == IntPtr.Zero)
                return null;

            Win32.GetWindowThreadProcessId(ownerHwnd, out uint processId);
            if (processId == 0)
                return null;

            using var process = Process.GetProcessById((int)processId);
            return process?.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private (object? Data, ClipboardType Type) GetClipboardContent()
    {
        try
        {
            // Use the new content handler registry for detection
            var dataObject = Clipboard.GetDataObject();
            if (dataObject != null)
            {
                var content = _contentRegistry.DetectContent(dataObject);
                if (content != null)
                {
                    // Convert IClipboardContent back to legacy format for compatibility
                    return ConvertToLegacyFormat(content);
                }
            }
        }
        catch (ExternalException)
        {
            // Clipboard is in use by another process
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading clipboard: {ex.Message}");
        }

        return (null, ClipboardType.Empty);
    }

    /// <summary>
    /// Gets clipboard content using the new IClipboardContent interface.
    /// </summary>
    public IClipboardContent? GetClipboardContentNew()
    {
        try
        {
            var dataObject = Clipboard.GetDataObject();
            if (dataObject != null)
            {
                return _contentRegistry.DetectContent(dataObject);
            }
        }
        catch (ExternalException)
        {
            // Clipboard is in use by another process
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading clipboard: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Converts IClipboardContent to legacy (data, type) tuple for backward compatibility.
    /// </summary>
    private static (object? Data, ClipboardType Type) ConvertToLegacyFormat(IClipboardContent content)
    {
        return content switch
        {
            TextContent text => (text.Text, ClipboardType.Text),
            RichTextContent rtf => (new ValueTuple<string, string?>(rtf.PlainText, rtf.RichText), ClipboardType.RichText),
            HtmlContent html => (new ValueTuple<string, string?>(html.PlainText, html.Html), ClipboardType.Html),
            ImageContent image => (image.ImageData, ClipboardType.Image),
            FileListContent files => (files.FilePaths, ClipboardType.FileList),
            _ => (null, ClipboardType.Empty)
        };
    }

    private static byte[]? BitmapSourceToBytes(BitmapSource source)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        Stop();
        _hwndSource.RemoveHook(WndProc);
    }
}

public class ClipboardChangedEventArgs : EventArgs
{
    public object Data { get; }
    public ClipboardType Type { get; }
    public int? TargetSlotIndex { get; }
    public string? SourceProcessName { get; }

    public ClipboardChangedEventArgs(object data, ClipboardType type, int? targetSlotIndex = null, string? sourceProcessName = null)
    {
        Data = data;
        Type = type;
        TargetSlotIndex = targetSlotIndex;
        SourceProcessName = sourceProcessName;
    }
}
