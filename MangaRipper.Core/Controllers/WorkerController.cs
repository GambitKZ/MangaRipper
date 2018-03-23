﻿using MangaRipper.Core.Models;
using MangaRipper.Core.Providers;
using MangaRipper.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MangaRipper.Core.Outputers;

namespace MangaRipper.Core.Controllers
{
    /// <summary>
    /// Worker support download manga chapters on back ground thread.
    /// </summary>
    public class WorkerController
    {

        private ServiceManager serviceManager { get; }

        CancellationTokenSource cancelSource;
        readonly SemaphoreSlim semaphore;
        private readonly ILogger logger;
        private readonly IOutputFactory outputFactory;
        private readonly IDownloader downloader;

        private enum ImageExtensions { Jpeg, Jpg, Png, Gif };

        public WorkerController(ServiceManager sm, ILogger logger, IOutputFactory outputFactory, IDownloader downloader)
        {
            this.logger = logger;
            logger.Info("> Worker()");
            serviceManager = sm;
            this.outputFactory = outputFactory;
            this.downloader = downloader;
            cancelSource = new CancellationTokenSource();
            semaphore = new SemaphoreSlim(2);
        }

        /// <summary>
        /// Stop download
        /// </summary>
        public void Cancel()
        {
            cancelSource.Cancel();
        }

        /// <summary>
        /// Run task download a chapter
        /// </summary>
        /// <param name="task">Contain chapter and save to path</param>
        /// <param name="progress">Callback to report progress</param>
        /// <returns></returns>
        public async Task RunDownloadTaskAsync(DownloadChapterTask task, IProgress<int> progress)
        {
            logger.Info($"> DownloadChapter: {task.Chapter.Url} To: {task.SaveToFolder}");
            await Task.Run(async () =>
            {
                try
                {
                    cancelSource = new CancellationTokenSource();
                    await semaphore.WaitAsync();
                    task.IsBusy = true;
                    await DownloadChapter(task, progress);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to download chapter: {task.Chapter.Url}");
                    throw;
                }
                finally
                {
                    task.IsBusy = false;
                    semaphore.Release();
                }
            });
        }

        /// <summary>
        /// Find all chapters of a manga
        /// </summary>
        /// <param name="mangaPath">The URL of manga</param>
        /// <param name="progress">Progress report callback</param>
        /// <returns></returns>
        public async Task<IEnumerable<Chapter>> FindChapterListAsync(string mangaPath, IProgress<int> progress)
        {
            logger.Info($"> FindChapters: {mangaPath}");
            return await Task.Run(async () =>
            {
                try
                {
                    await semaphore.WaitAsync();
                    return await FindChaptersInternal(mangaPath, progress);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to find chapters: {mangaPath}");
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        private async Task DownloadChapter(DownloadChapterTask task, IProgress<int> progress)
        {
            var chapter = task.Chapter;
            progress.Report(0);
            var service = serviceManager.GetService(chapter.Url);
            var images = await service.FindImages(chapter, new Progress<int>(count =>
            {
                progress.Report(count / 2);
            }), cancelSource.Token);

            var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempFolder);

            // Should we use counter?
            var useCounter = task.Formats.Any(x => x == OutputFormat.Counter);
            await DownloadImages(images, tempFolder, useCounter, progress);

            foreach (var format in task.Formats)
            {
                var factory = outputFactory.CreateOutput(format);
                if (factory != null)
                {
                    factory.CreateOutput(tempFolder, task.SaveToFolder);
                }
            }

            progress.Report(100);
        }

        /// <summary>
        /// Download all images
        /// </summary>
        /// <param name="inputImages">Array with images' Url in string format</param>
        /// <param name="destination">Path on disc which is in Temp folder</param>
        /// <param name="useCounter">Should we replace the file names with the counter</param>
        /// <param name="progress">Progress percentage</param>
        /// <returns></returns>
        private async Task DownloadImages(IEnumerable<string> inputImages, string destination, bool useCounter, IProgress<int> progress)
        {
            var images = inputImages.ToArray();
            logger.Info($@"Download {images.Length} images into {destination}");

            var countImage = 0;
            foreach (var image in images)
            {
                cancelSource.Token.ThrowIfCancellationRequested();
                // Figure out if the "Counter" should be used
                var count = useCounter ? countImage : -1;
                await downloader.DownloadToFolder(image, destination, count, cancelSource.Token);                
                countImage++;
                int i = Convert.ToInt32((float)countImage / images.Count() * 100 / 2);
                progress.Report(50 + i);
            }
        }

        private async Task<IEnumerable<Chapter>> FindChaptersInternal(string mangaPath, IProgress<int> progress)
        {
            progress.Report(0);
            // let service find all chapters in manga
            var service = serviceManager.GetService(mangaPath);
            var chapters = await service.FindChapters(mangaPath, progress, cancelSource.Token);
            progress.Report(100);
            return chapters;
        }
    }
}
