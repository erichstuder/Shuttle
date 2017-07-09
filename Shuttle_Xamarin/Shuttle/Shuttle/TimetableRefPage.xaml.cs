using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace Shuttle
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class TimetableRefPage : ContentPage
	{
        public Dictionary<string, string> timetableRefDictionary = new Dictionary<string, string>();

        public TimetableRefPage ()
		{
			InitializeComponent ();

            timetableRefDictionary.Add("Flims Laax Falera Shuttle - Winter", "https://www.laax.com/fileadmin/Daten/Dokumente/PDF/FLF_Winter_17_def.pdf");
            timetableRefDictionary.Add("Flims Laax Falera Shuttle - Sommer", "https://www.flims.com/fileadmin/Daten/Dokumente/PDF_Flims/Flims_Laax_Falera_SO-ZW_Web_def_17-06-16.pdf");
            timetableRefDictionary.Add("Skibus Sagogn", "http://www.sagogn.ch/index.php?eID=tx_nawsecuredl&u=0&file=uploads/media/TFP_Sagogn_11.12.2016-17.04.2017_01.pdf&t=1499706936&hash=83c45466fac0c63bc825908bf350ccc1");
            timetableRefDictionary.Add("Arosa Bus", "https://www.gemeindearosa.ch/fileadmin/user_upload/customers/gemeindearosa/Dokumente/Allgemeine_News/Fahrplan_2016_2017als_PDF.pdf");

            ObservableCollection<MyTextItem> collection = new ObservableCollection<MyTextItem>();
            for (int n = 0; n < timetableRefDictionary.Count; n++)
            {
                collection.Add(new MyTextItem { Text = timetableRefDictionary.Keys.ElementAt(n)});
            }
            ListView_TimetableRefNames.ItemsSource = collection;

            var template = new DataTemplate(typeof(TextCell));
            template.SetValue(TextCell.TextColorProperty, Color.Black);
            template.SetBinding(TextCell.TextProperty, "Text");
            ListView_TimetableRefNames.ItemTemplate = template;
        }

        public class MyTextItem
        {
            public string Text { get; set; }
        }

        private void ListView_TimetableRefNames_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            string uri = null;
            
            timetableRefDictionary.TryGetValue(((MyTextItem)e.SelectedItem).Text, out uri);
            Device.OpenUri(new Uri(uri));
        }
    }
}