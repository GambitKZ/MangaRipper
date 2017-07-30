namespace MangaRipper.Core.Models
{
    public class CommonSettings
    {
        public string SaveTo { get; set; }
        public string Url { get; set; }
        public bool CbzChecked { get; set; }
        public bool PrefixChecked { get; set; }

        /// <summary>
        /// Gets or sets the base portion of the series-specific directory.
        /// </summary>
        public string BaseSeriesDestination { get; set; }
    }
}
