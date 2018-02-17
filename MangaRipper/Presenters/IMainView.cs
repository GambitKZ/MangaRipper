﻿using System.Collections.Generic;
using System.Windows.Forms;
using MangaRipper.Core.Models;

namespace MangaRipper.Presenters
{
    public interface IMainView
    {
        void SetChapters(IEnumerable<Chapter> chapters);
        void SetChaptersProgress(string progress);
        void SetStatusText(string statusMessage);
        void ShowMessageBox(string caption, string text, MessageBoxButtons buttons, MessageBoxIcon icon);
        void EnableTheButtonsAfterError();
    }
}