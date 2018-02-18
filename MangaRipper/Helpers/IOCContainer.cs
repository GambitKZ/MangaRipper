using SimpleInjector;
using System;
using System.Linq;
using MangaRipper.Infrastructure;
using MangaRipper.Core.Interfaces;
using System.Reflection;
using MangaRipper.Core.Models;
using MangaRipper.Core;
using MangaRipper.Core.Outputers;
using System.IO;
using MangaRipper.Forms;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Registration;

namespace MangaRipper.Helpers
{
    public class IocContainer
    {
        [ImportMany(typeof(IMangaService))]
        IEnumerable<IMangaService> plugins;

        private Container container; 

        public Container GetContainer()
        {
            container = new Container();
            container.RegisterConditional(typeof(ILogger),
                   c => typeof(NLogLogger<>).MakeGenericType(c.Consumer.ImplementationType),
                   Lifestyle.Transient,
                   c => true
                   );            

            container.Register<IOutputFactory, OutputFactory>();

            var configPath = Path.Combine(Environment.CurrentDirectory, "MangaRipper.Configuration.json");
            container.Register<IConfiguration>(() => new Configuration(configPath));
            container.Register<IDownloader, Downloader>();
            container.Register<IXPathSelector, HtmlAtilityPackAdapter>();
            container.Register<IScriptEngine, JurassicScriptEngine>();
            container.Register<IRetry, Retry>();

            //var pluginPath = Path.Combine(Environment.CurrentDirectory, "Plugins");
            //var pluginAssemblies = new DirectoryInfo(pluginPath).GetFiles()
            //    .Where(file => file.Extension.ToLower() == ".dll" && file.Name.StartsWith("MangaRipper.Plugin."))
            //    .Select(file => Assembly.Load(AssemblyName.GetAssemblyName(file.FullName)));

            
            container.Register<FormMain>();

            container.RegisterDecorator<IXPathSelector, XPathSelectorLogging>();
            container.RegisterDecorator<IDownloader, DownloadLogging>();

            Compose();
            container.RegisterCollection<IMangaService>(plugins);

            return container;
        }

        private void Compose()
        {
            var dirCatalog = new DirectoryCatalog(Path.Combine(Environment.CurrentDirectory, "Plugins"));
            var catalog = new AggregateCatalog(dirCatalog);            
            var composContainer = new CompositionContainer(catalog);

            var registrationBuilder = new RegistrationBuilder();
            //registrationBuilder.ForTypesMatching<IMangaService>().Export<IDownloader>();

            registrationBuilder.ForTypesDerivedFrom<ILogger>().Export<ILogger>();
            registrationBuilder.ForTypesDerivedFrom<IDownloader>().Export<IDownloader>();
            registrationBuilder.ForTypesDerivedFrom<IXPathSelector>().Export<IXPathSelector>();
            registrationBuilder.ForTypesDerivedFrom<IScriptEngine>().Export<IScriptEngine>();

            registrationBuilder.ForTypesDerivedFrom<IMangaService>().ExportInterfaces();

            //composContainer.ComposeExportedValue<ILogger>(new NLogLogger<IocContainer>());
            //composContainer.ComposeExportedValue<IDownloader>(container.GetInstance<IDownloader>());
            //composContainer.ComposeExportedValue<IXPathSelector>(container.GetInstance<IXPathSelector>());
            //composContainer.ComposeExportedValue<IScriptEngine>(container.GetInstance<IScriptEngine>());
            composContainer.Compose(new CompositionBatch());
            composContainer.SatisfyImportsOnce(this, registrationBuilder);
            //ComposeParts(this, registrationBuilder);
        }
    }
}
