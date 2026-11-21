using System.Collections.Generic;
using Translumo.Infrastructure;
using Translumo.Processing.Overlay;

namespace Translumo.Processing.Interfaces
{
    public interface IChatTextMediator
    {
        void SendText(string text, bool successful);

        void SendText(string text, TextTypes textType);

        void ClearTexts();

        // New: send original + translated
        void SendText(string original, string translated);

        /// <summary>
        /// Replaces the blocks shown by the on-screen overlay. An empty list clears the overlay.
        /// </summary>
        void SendOverlayBlocks(IReadOnlyList<OverlayTextBlock> blocks);
    }
}
