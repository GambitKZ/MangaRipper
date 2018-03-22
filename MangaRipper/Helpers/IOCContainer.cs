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
            //var registrationBuilder = new RegistrationBuilder();
            ////registrationBuilder.ForTypesDerivedFrom<IDownloader>().Export<IDownloader>();
            //registrationBuilder.ForType<IDownloader>().Export<IDownloader>();
            //registrationBuilder.ForType<IXPathSelector>().Export<IXPathSelector>();
            //registrationBuilder.ForType<IScriptEngine>().Export<IScriptEngine>();
            //registrationBuilder.ForTypesDerivedFrom<IMangaService>().Export();

            //var dirCatalog = new DirectoryCatalog(Path.Combine(Environment.CurrentDirectory, "Plugins"), registrationBuilder);

            var dirCatalog = new DirectoryCatalog(Path.Combine(Environment.CurrentDirectory, "Plugins"));
            

            //var catalog = new AggregateCatalog(dirCatalog);
            var composContainer = new CompositionContainer(dirCatalog);
            composContainer.ComposeExportedValue("Downloader", container.GetInstance<IDownloader>());
            composContainer.ComposeExportedValue("Selector", container.GetInstance<IXPathSelector>());
            composContainer.ComposeExportedValue("Engine", container.GetInstance<IScriptEngine>());
            composContainer.ComposeParts(this);

            //composContainer.ComposeExportedValue("Logger", container.GetInstance<ILogger>());


            //registrationBuilder.ForTypesMatching<IMangaService>().Export<IDownloader>();

            //registrationBuilder.ForTypesDerivedFrom<ILogger>().Export<ILogger>();


            //registrationBuilder.ForTypesDerivedFrom<IMangaService>().ExportInterfaces();

            //composContainer.ComposeExportedValue<ILogger>(new NLogLogger<IocContainer>());
            //composContainer.ComposeExportedValue<IDownloader>(container.GetInstance<IDownloader>());
            //composContainer.ComposeExportedValue<IXPathSelector>(container.GetInstance<IXPathSelector>());
            //composContainer.ComposeExportedValue<IScriptEngine>(container.GetInstance<IScriptEngine>());
            //composContainer.Compose(new CompositionBatch());
            //composContainer.SatisfyImportsOnce(this);
            //composContainer.ComposeExportedValue(catalog);
            //ComposeParts(this, registrationBuilder);
            //composContainer.ComposeParts(this);


            // Export value - https://stackoverflow.com/questions/2008133/mef-constructor-injection#2008306
            // Error: The container can't be changed after the first call to GetInstance, GetAllInstances and Verify
            //composContainer.ComposeExportedValue("downloader", container.GetInstance<IDownloader>());
            //composContainer.ComposeExportedValue("selector", container.GetInstance<IXPathSelector>());
            //composContainer.ComposeExportedValue("engine", container.GetInstance<IScriptEngine>());
            //composContainer.ComposeParts(this);
        }
    }
}
