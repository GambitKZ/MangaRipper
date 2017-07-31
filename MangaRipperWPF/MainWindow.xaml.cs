using NLog;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using MangaRipper.Core.Helpers;
using System.Linq;
using System;
using MangaRipper.Core.Providers;
using System.Collections.Generic;
using MangaRipper.Core.Models;
using System.IO;
using System.Windows.Controls;
using System.Data;
using System.Windows.Documents;
using MangaRipper.Core.DataTypes;
using System.Collections;
using System.ComponentModel;

namespace MangaRipperWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ApplicationConfiguration _appConf = new ApplicationConfiguration();
        private BindingList<DownloadChapterTask> _downloadQueue;

        private string SaveDestination
        {
            get
            {
                return cbUseSeriesFolder.IsChecked.Value ? SeriesDestination : txtSaveTo.Text;
            }
        }

        private string SeriesDestination
        {
            get;
            set;
        }

        public void SetStatusText(string statusMessage)
        {
            Dispatcher.Invoke(() =>
            {
                txtMessage.Text = statusMessage;
            });
        }

        private void LoadBookmark()
        {
            var bookmarks = _appConf.LoadBookMarks();
            cbTitleUrl.Items.Clear();
            var sc = bookmarks;
            if (sc == null)
            {
                return;
            }
            foreach (var item in sc)
            {
                cbTitleUrl.Items.Add(item);
            }
        }

        public MainWindow()
        {
            Logger.Info("> Main()");
            InitializeComponent();
            var appDomain = AppDomain.CurrentDomain;

            // Initialize config and plugins
            FrameworkProvider.Init(Path.Combine(Environment.CurrentDirectory, "Plugins"),
                Path.Combine(Environment.CurrentDirectory, "MangaRipper.Configuration.json"));

            // Set State for Window
            var state = _appConf.LoadCommonSettings();
            txtSaveTo.Text = state.SaveTo;
            cbTitleUrl.Text = state.Url;
            cbSaveCbz.IsChecked = state.CbzChecked;
            checkBoxForPrefix.IsChecked = state.PrefixChecked;

            GetMangaWebSites();

            // Fill other components
            _downloadQueue = _appConf.LoadDownloadChapterTasks();
            DataContext = _downloadQueue;
            LoadBookmark();
            CheckForUpdate();

            Logger.Info("< Main()");
        }

        #region Actions when Form is Loaded

        private void FormMain_Loaded(object sender, RoutedEventArgs e)
        {
            CheckForUpdate();
        }

        private async void CheckForUpdate()
        {
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if (currentVersion == "1.0.0.0")
            {
                return;
            }

            var latestVersion = await UpdateNotification.GetLatestVersion();
            if (UpdateNotification.GetLatestBuildNumber(latestVersion) >
                UpdateNotification.GetLatestBuildNumber(currentVersion))
            {
                Logger.Info($"Local version: {currentVersion}. Remote version: {latestVersion}");

                if (MessageBox.Show(
                    $"There's a new version: ({latestVersion}) - Click OK to open download page.",
                    Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    Process.Start("https://github.com/NguyenDanPhuong/MangaRipper/releases");
                }
            }
        }

        #endregion        

        #region Work with Bookmarks

        private void btnAddBookmark_Click(object sender, RoutedEventArgs e)
        {
            var sc = _appConf.LoadBookMarks().ToList();
            if (!sc.Contains(cbTitleUrl.Text))
            {
                sc.Add(cbTitleUrl.Text);
                _appConf.SaveBookmarks(sc);
                LoadBookmark();
            }
        }

        private void btnRemoveBookmark_Click(object sender, RoutedEventArgs e)
        {
            var sc = _appConf.LoadBookMarks().ToList();
            sc.Remove(cbTitleUrl.Text);
            _appConf.SaveBookmarks(sc);
            LoadBookmark();
        }

        #endregion

        #region Get all chapters from Url

        private async void btnGetChapter_Click(object sender, RoutedEventArgs e)
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                MessageBox.Show("An Internet connection has not been detected.",
                    Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error("Aborting chapter retrieval, no Internet connection.");
                return;
            }
            btnGetChapter.IsEnabled = false;
            var titleUrl = cbTitleUrl.Text;

            await Task.Run(() => OnFindChapters(titleUrl));
        }

        private async Task OnFindChapters(string obj)
        {
            try
            {
                var worker = FrameworkProvider.GetWorker();
                var progressInt = new Progress<int>(progress => SetChaptersProgress(progress + @"%"));
                var chapters = await worker.FindChapterListAsync(obj, progressInt);
                SetChapters(chapters);
            }
            catch (OperationCanceledException ex)
            {
                SetStatusText(@"Download canceled! Reason: " + ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                SetStatusText(@"Download canceled! Reason: " + ex.Message);
                if (MessageBox.Show(ex.Message,
                    ex.Source,
                    MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    EnableDownload();
                };
            }
        }
        public void SetChapters(IEnumerable<Chapter> chapters)
        {
            EnableDownload();
            Dispatcher.Invoke(() =>
            {
                dgvChapter.ItemsSource = chapters.ToList();
            });
        }

        public void EnableDownload()
        {
            Dispatcher.Invoke(() =>
            {
                btnGetChapter.IsEnabled = true;
            });
        }

        public void SetChaptersProgress(string progress)
        {
            Dispatcher.Invoke(() =>
            {
                txtPercent.Text = progress;
            });
        }

        #endregion

        #region Additional operations on Window with All Existing Chapters

        private void dgvChapter_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string headername = e.Column.Header.ToString();

            //Change Header for Name and not show others
            if (headername == "Name")
            {
                e.Column.Header = "Chapter Name";
                e.Column.Width = new DataGridLength(2.0, DataGridLengthUnitType.Star);
            }
            else
            {
                e.Cancel = true;
            }
        }

        #endregion

        #region Window with List of WebSites 

        private void GetMangaWebSites()
        {
            try
            {
                foreach (var service in FrameworkProvider.GetMangaServices())
                {
                    var infor = service.GetInformation();
                    dgvSupportedSites.Items.Add(new { Name = infor.Name, Address = infor.Link, Language = infor.Language });
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
            }
        }

        private void WebSite_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)e.OriginalSource;
            Process.Start(link.NavigateUri.AbsoluteUri);
        }

        #endregion

        #region Adding Chapters to Queue

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var formats = GetOutputFormats().ToArray();
            if (!CanBeAdded(formats))
            {
                return;
            }

            AddToDownloadQueue(formats, dgvChapter.SelectedItems);
        }

        private void btnAddAll_Click(object sender, RoutedEventArgs e)
        {
            var formats = GetOutputFormats().ToArray();
            if (!CanBeAdded(formats))
            {
                return;
            }

            AddToDownloadQueue(formats, dgvChapter.Items);
        }

        private IEnumerable<OutputFormat> GetOutputFormats()
        {
            var outputFormats = new List<OutputFormat>();

            if (cbSaveFolder.IsChecked.Value)
            {
                outputFormats.Add(OutputFormat.Folder);
            }

            if (cbSaveCbz.IsChecked.Value)
            {
                outputFormats.Add(OutputFormat.CBZ);
            }

            return outputFormats;
        }

        private bool CanBeAdded(OutputFormat[] formats)
        {
            if (formats.Length == 0)
            {
                MessageBox.Show("Please select at least one output format (Folder, Cbz...)");
                return false;
            }
            if (!Directory.Exists(txtSaveTo.Text))
            {
                MessageBox.Show("Provided folder do not exists. Please, provide a correct path");
                return false;
            }

            return true;
        }

        private void AddToDownloadQueue(OutputFormat[] formats, IList chapters)
        {
            List<Chapter> chaptersContainer = new List<Chapter>();

            foreach (var row in chapters)
            {
                chaptersContainer.Add(row as Chapter);
            }

            var items = ApplicationConfiguration.DeepClone(chaptersContainer);
            items.Reverse();

            foreach (var item in items.Where(item => _downloadQueue.All(r => r.Chapter.Url != item.Url)))
            {
                _downloadQueue.Add(new DownloadChapterTask(item, SaveDestination, formats));
            }
        }

        #endregion

        #region Additional Operations on Chapters

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            List<DownloadChapterTask> chaptersForRemoval = new List<DownloadChapterTask>();

            foreach (var item in dgvQueueChapter.SelectedItems)
            {
                var chapter = (DownloadChapterTask)item;

                if (!chapter.IsBusy)
                {
                    chaptersForRemoval.Add(chapter);
                }
            }

            ChapterDeletion(chaptersForRemoval);
        }

        private void btnRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            var removeItems = _downloadQueue.Where(r => !r.IsBusy).ToList();

            ChapterDeletion(removeItems);
        }

        private void ChapterDeletion(ICollection<DownloadChapterTask> chaptersForRemoval)
        {
            if (chaptersForRemoval.Count > 0)
            {
                foreach (var chapter in chaptersForRemoval)
                {
                    _downloadQueue.Remove(chapter);
                }
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            FrameworkProvider.GetWorker().Cancel();
        }

        #endregion

        #region Manga Download Processes

        private async void btnDownload_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                btnDownload.IsEnabled = false;
                await StartDownload();
            }
            catch (OperationCanceledException ex)
            {
                SetStatusText(@"Download canceled! Reason: " + ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                MessageBox.Show(ex.Message, ex.Source, MessageBoxButton.OKCancel, MessageBoxImage.Information);
                SetStatusText(@"Download canceled! Reason: " + ex.Message);
            }
            finally
            {
                btnDownload.IsEnabled = true;
            }
        }

        private async Task StartDownload()
        {
            while (_downloadQueue.Count > 0)
            {
                var chapter = _downloadQueue.First();
                var worker = FrameworkProvider.GetWorker();

                await worker.RunDownloadTaskAsync(chapter, new Progress<int>(c =>
                {
                    UpdatePercent(chapter, c);
                }));

                Dispatcher.Invoke(() =>
                {
                    _downloadQueue.Remove(chapter);
                });
            }
        }

        private void UpdatePercent(DownloadChapterTask chapter, int percent)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var item in dgvQueueChapter.Items)
                {
                    var chapterTask = item as DownloadChapterTask;
                    if (chapter == chapterTask)
                    {

                        chapter.Percent = percent;

                    }
                }

                dgvQueueChapter.Items.Refresh();
            });
        }

        #endregion

        #region Save To Destination Logic

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(SaveDestination))
            {
                Process.Start(SaveDestination);
            }
            else
            {
                MessageBox.Show($"Directory \"{SaveDestination}\" doesn't exist.");
            }
        }

        private void btnChangeSaveTo_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog browse = new System.Windows.Forms.FolderBrowserDialog();

            System.Windows.Forms.DialogResult result = browse.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                txtSaveTo.Text = browse.SelectedPath;
            }
        }

        #endregion
    }
}
