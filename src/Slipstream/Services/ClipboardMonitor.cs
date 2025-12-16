using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Slipstream.Models;
using Slipstream.Native;

namespace Slipstream.Services;

public class ClipboardMonitor : IDisposable
{
    private readonly HwndSource _hwndSource;
    private readonly IntPtr _hwnd;
    private bool _isOwnClipboardChange;
    private int? _pendingTargetSlot;

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public ClipboardMonitor(IntPtr hwnd, HwndSource hwndSource)
    {
        _hwnd = hwnd;
        _hwndSource = hwndSource;
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
            if (!_isOwnClipboardChange)
            {
                OnClipboardChanged();
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
            var (data, type) = GetClipboardContent();
            if (type != ClipboardType.Empty && data != null)
            {
                // Check if there's a pending target slot (from Ctrl+Alt+# hotkey)
                int? targetSlot = _pendingTargetSlot;
                _pendingTargetSlot = null; // Clear it after use

                ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(data, type, targetSlot));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error capturing clipboard: {ex.Message}");
        }
    }

    private (object? Data, ClipboardType Type) GetClipboardContent()
    {
        try
        {
            // Check for files first (highest priority)
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                var fileArray = new string[files.Count];
                files.CopyTo(fileArray, 0);
                return (fileArray, ClipboardType.FileList);
            }

            // Check for image
            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image != null)
                {
                    var imageData = BitmapSourceToBytes(image);
                    if (imageData != null)
                    {
                        return (imageData, ClipboardType.Image);
                    }
                }
            }

            // Check for HTML
            if (Clipboard.ContainsText(TextDataFormat.Html))
            {
                var html = Clipboard.GetText(TextDataFormat.Html);
                var plainText = Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? Clipboard.GetText(TextDataFormat.UnicodeText)
                    : Clipboard.GetText();
                return (new ValueTuple<string, string?>(plainText, html), ClipboardType.Html);
            }

            // Check for RTF
            if (Clipboard.ContainsText(TextDataFormat.Rtf))
            {
                var rtf = Clipboard.GetText(TextDataFormat.Rtf);
                var plainText = Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? Clipboard.GetText(TextDataFormat.UnicodeText)
                    : Clipboard.GetText();
                return (new ValueTuple<string, string?>(plainText, rtf), ClipboardType.RichText);
            }

            // Check for plain text
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return (text, ClipboardType.Text);
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

    public ClipboardChangedEventArgs(object data, ClipboardType type, int? targetSlotIndex = null)
    {
        Data = data;
        Type = type;
        TargetSlotIndex = targetSlotIndex;
    }
}
