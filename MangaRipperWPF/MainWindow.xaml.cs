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

        private void FormMain_Loaded(object sender, RoutedEventArgs e)
        {
            CheckForUpdate();
            FindChaptersClicked += OnFindChapters;
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

        public void SetChaptersProgress(string progress)
        {
            Dispatcher.Invoke(() =>
            {
                txtPercent.Text = progress;
            });
        }

        public void SetStatusText(string statusMessage)
        {
            Dispatcher.Invoke(() =>
            {
                txtMessage.Text = statusMessage;
            });
        }

        public void EnableDownload()
        {
            Dispatcher.Invoke(() =>
            {
                btnGetChapter.IsEnabled = true;
            });
        }

        public void SetChapters(IEnumerable<Chapter> chapters)
        {
            EnableDownload();
            Dispatcher.Invoke(() =>
            {
                dgvChapter.ItemsSource = chapters.ToList();
            });

            //dgvChapter.DataSource = chapters.ToList();
            //PrefixLogic();
            //PrepareSpecificDirectory();
        }

        public Func<string, Task> FindChaptersClicked { get; set; }

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

        private void WebSite_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)e.OriginalSource;
            Process.Start(link.NavigateUri.AbsoluteUri);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var formats = GetOutputFormats().ToArray();
            if (!CanBeAdded(formats))
            {
                return;
            }

            AddToDownloadQueue(formats, dgvChapter.SelectedItems);

            //foreach (var row in rows)
            //{
            //    dgvQueueChapter.Items.Add(row);
            //}
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

        private void btnAddAll_Click(object sender, RoutedEventArgs e)
        {
            var formats = GetOutputFormats().ToArray();
            if (!CanBeAdded(formats))
            {
                return;
            }

            AddToDownloadQueue(formats, dgvChapter.Items);
        }
        
    }
}
