﻿using MangaRipper.Core.Interfaces;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MangaRipper.Core.Models;

namespace MangaRipper.Core.Services
{
    class PluginService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private string PluginsPath { get; set; }
        private Configuration Config { get; set; }
        public PluginService(string pluginsPath, Configuration config)
        {
            PluginsPath = pluginsPath;
            Config = config;
        }

        /// <summary>
        /// Load plugins for WebSites
        /// </summary>
        /// <returns>List with all founded Services</returns>
        public IEnumerable<IMangaService> LoadWebPlugins()
        {
            LoadPluginAssemblies(PluginsPath);
            var services = CreateServices().ToArray();
            InjectConfiguration(services);
            return services;
        }

        private void LoadPluginAssemblies(string path)
        {
            if (!Directory.Exists(path))
            {
                var error = $"The plugins path: `{path}` is not exist!";
                Logger.Error(error);
                throw new DirectoryNotFoundException(error);
            }

            foreach (var fileOn in Directory.GetFiles(path))
            {
                FileInfo file = new FileInfo(fileOn);
                if (file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info($@"Load plugin from file: {fileOn}");
                    Assembly.LoadFrom(fileOn);
                }
            }
        }

        private IEnumerable<IMangaService> CreateServices()
        {
            var result = new List<IMangaService>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var pluginTypes = a.GetTypes().Where(t => t.IsSubclassOf(typeof(MangaService)));
                try
                {
                    foreach (var pluginType in pluginTypes)
                    {
                        var pluginclass = Activator.CreateInstance(pluginType) as IMangaService;
                        if (pluginclass != null)
                            result.Add(pluginclass);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
            return result;
        }

        private void InjectConfiguration(IEnumerable<IMangaService> services)
        {
            foreach (var mangaService in services)
            {
                InjectConfiguration(mangaService);
            }
        }

        private void InjectConfiguration(IMangaService service)
        {
            string lookupPrefix = $@"Plugin.{service.GetInformation().Name}.";
            var configItems = Config.FindConfigByPrefix(lookupPrefix);
            configItems = RemovePrefix(configItems, lookupPrefix);
            service.Configuration(configItems);
        }

        private IEnumerable<KeyValuePair<string, object>> RemovePrefix(IEnumerable<KeyValuePair<string, object>> configItems, string prefix)
        {
            return configItems.ToArray().Select(i => new KeyValuePair<string, object>(i.Key.Remove(0, prefix.Length), i.Value));
        }
    }
}
