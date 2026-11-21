using System;
using System.Collections.Generic;
using Translumo.Infrastructure;
using Translumo.Processing.Interfaces;
using Translumo.Processing.Overlay;
using Translumo.Utils;

namespace Translumo.Services
{
    public class ChatUITextMediator : IChatTextMediator
    {
        public event EventHandler<TranslatedEventArgs> TextRaised;
        public event EventHandler ClearTextsRaised;
        public event EventHandler<TranslatedWithOriginalEventArgs> TextWithOriginalRaised;
        public event EventHandler<OverlayBlocksEventArgs> OverlayBlocksRaised;

        public void SendText(string text, bool successful)
        {
            TextRaised?.RaiseOnUIThread(this, new TranslatedEventArgs(text, successful ? TextTypes.Translation : TextTypes.Error));
        }

        public void SendText(string text, TextTypes textType)
        {
            TextRaised?.RaiseOnUIThread(this, new TranslatedEventArgs(text, textType));
        }

        public void ClearTexts()
        {
            ClearTextsRaised?.RaiseOnUIThread(this);
        }

        public void SendText(string original, string translated)
        {
            TextWithOriginalRaised?.RaiseOnUIThread(this, new TranslatedWithOriginalEventArgs(original, translated, TextTypes.Translation));
        }

        public void SendOverlayBlocks(IReadOnlyList<OverlayTextBlock> blocks)
        {
            OverlayBlocksRaised?.RaiseOnUIThread(this, new OverlayBlocksEventArgs(blocks));
        }
    }

    public class OverlayBlocksEventArgs : EventArgs
    {
        public IReadOnlyList<OverlayTextBlock> Blocks { get; }

        public OverlayBlocksEventArgs(IReadOnlyList<OverlayTextBlock> blocks)
        {
            Blocks = blocks;
        }
    }
}
