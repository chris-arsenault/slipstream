using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Slipstream.Content;
using Slipstream.Models;
using Slipstream.Native;

namespace Slipstream.Services;

public class PasteEngine
{
    private ClipboardMonitor? _clipboardMonitor;
    private readonly KeyboardSequencer _keyboardSequencer;
    private readonly ContentHandlerRegistry _contentRegistry;

    public PasteEngine() : this(new KeyboardSequencer(new KeyboardSimulator()))
    {
    }

    public PasteEngine(KeyboardSequencer keyboardSequencer, ContentHandlerRegistry? contentRegistry = null)
    {
        _keyboardSequencer = keyboardSequencer;
        _contentRegistry = contentRegistry ?? new ContentHandlerRegistry();
    }

    public void SetClipboardMonitor(ClipboardMonitor monitor)
    {
        _clipboardMonitor = monitor;
    }

    /// <summary>
    /// Pastes content from a slot by setting clipboard and sending Ctrl+V.
    /// </summary>
    /// <param name="slot">The slot to paste from</param>
    public void PasteFromSlot(ClipboardSlot slot)
    {
        if (!slot.HasContent)
            return;

        var content = slot.GetContent();
        if (content == null)
            return;

        PasteContent(content);
    }

    /// <summary>
    /// Pastes content directly by setting clipboard and sending Ctrl+V.
    /// </summary>
    /// <param name="content">The content to paste</param>
    public void PasteContent(IClipboardContent content)
    {
        Console.WriteLine($"[PasteEngine] PasteContent: Type={content.TypeName}");

        // Tell clipboard monitor to ignore this change
        _clipboardMonitor?.SetOwnClipboardChange(true);

        // Build the data object using the content handler registry
        var dataObject = new System.Windows.DataObject();
        if (!_contentRegistry.WriteToClipboard(content, dataObject))
        {
            Console.WriteLine($"[PasteEngine] Failed to write content to clipboard");
            return;
        }

        // Try to set clipboard with retries
        bool success = false;
        for (int i = 0; i < 10 && !success; i++)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Clipboard.SetDataObject(dataObject, false); // false = don't flush
                });
                success = true;
                Console.WriteLine($"[PasteEngine] Clipboard set on attempt {i + 1}");
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x800401D0))
            {
                Console.WriteLine($"[PasteEngine] Clipboard busy, retry {i + 1}/10");
                Thread.Sleep(10);
            }
        }

        if (!success)
        {
            Console.WriteLine($"[PasteEngine] Failed to set clipboard after 10 attempts");
            return;
        }

        // Small delay for clipboard to be ready
        Thread.Sleep(20);

        // Send Ctrl+V (snapshots and restores modifier state automatically)
        _keyboardSequencer.SendPasteWithModifierRelease();

        Console.WriteLine($"[PasteEngine] Paste complete");
    }
}
